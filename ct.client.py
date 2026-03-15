import os
import json
import base64
import hashlib
import argparse
import sys
import urllib.request
import urllib.error
import threading
from typing import Dict, Any, List
from concurrent.futures import ThreadPoolExecutor
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.backends import default_backend

class CtCryptoService:
    def __init__(self, key_b64: str):
        try:
            self.key = base64.b64decode(key_b64)
            if len(self.key) not in [16, 24, 32]:
                print(f"Warning: Key length is {len(self.key)} bytes. AES requires 16, 24, or 32 bytes.")
        except Exception as e:
            print(f"Error decoding encryption key from Base64: {e}")
            sys.exit(1)

    def decrypt_bytes(self, data: bytes) -> bytes:
        iv = data[:16]
        encrypted_data = data[16:]
        cipher = Cipher(algorithms.AES(self.key), modes.CBC(iv), backend=default_backend())
        decryptor = cipher.decryptor()
        decrypted_padded_data = decryptor.update(encrypted_data) + decryptor.finalize()
        unpadder = padding.PKCS7(128).unpadder()
        return unpadder.update(decrypted_padded_data) + unpadder.finalize()

    def encrypt_bytes(self, data: bytes) -> bytes:
        iv = os.urandom(16)
        cipher = Cipher(algorithms.AES(self.key), modes.CBC(iv), backend=default_backend())
        encryptor = cipher.encryptor()
        padder = padding.PKCS7(128).padder()
        padded_data = padder.update(data) + padder.finalize()
        encrypted_data = encryptor.update(padded_data) + encryptor.finalize()
        return iv + encrypted_data

    def decrypt_object(self, data_b64: str) -> Any:
        encrypted_bytes = base64.b64decode(data_b64)
        decrypted_bytes = self.decrypt_bytes(encrypted_bytes)
        return json.loads(decrypted_bytes.decode('utf-8'))

    def encrypt_object(self, obj: Any) -> str:
        json_bytes = json.dumps(obj, separators=(',', ':')).encode('utf-8')
        encrypted = self.encrypt_bytes(json_bytes)
        return base64.b64encode(encrypted).decode('utf-8')

def compute_hash_local(file_path: str, start: int, end: int) -> str:
    sha256 = hashlib.sha256()
    if not os.path.exists(file_path):
        return ""
    try:
        with open(file_path, "rb") as f:
            f.seek(start)
            remaining = end - start
            while remaining > 0:
                chunk = f.read(min(remaining, 64 * 1024))
                if not chunk:
                    break
                sha256.update(chunk)
                remaining -= len(chunk)
        return base64.b64encode(sha256.digest()).decode('utf-8')
    except Exception:
        return ""

def post_json(url, payload_obj, crypto):
    encrypted_payload = crypto.encrypt_object(payload_obj)
    data = json.dumps(encrypted_payload).encode('utf-8')
    req = urllib.request.Request(url, data=data, headers={'Content-Type': 'application/json'}, method='POST')
    with urllib.request.urlopen(req) as f:
        return f.read().decode('utf-8')

def download_part(server_url, crypto, part_key_info, part_value_info, local_file_path, progress_state, lock):
    # Check hash
    check_request = {
        "FilePath": part_key_info['FilePath'],
        "Start": part_value_info['Offset'],
        "End": part_value_info['Offset'] + part_value_info['Length']
    }
    
    try:
        expected_hash = post_json(f"{server_url}/check", check_request, crypto)
        
        current_hash = compute_hash_local(local_file_path, part_value_info['Offset'], part_value_info['Offset'] + part_value_info['Length'])
        
        if expected_hash == current_hash:
            with lock:
                progress_state['completed'] += 1
                percent = (progress_state['completed'] / progress_state['total']) * 100
                print(f"[{percent:6.2f}%] Part {part_key_info['Index']} matches hash, skipping.")
            return

        # Download
        download_request = {
            "FilePath": part_key_info['FilePath'],
            "Start": part_value_info['Offset'],
            "End": part_value_info['Offset'] + part_value_info['Length']
        }
        
        encrypted_part_b64 = post_json(f"{server_url}/download", download_request, crypto)
        encrypted_part_bytes = base64.b64decode(encrypted_part_b64)
        decrypted_part = crypto.decrypt_bytes(encrypted_part_bytes)
        
        with open(local_file_path, "r+b") as f:
            f.seek(part_value_info['Offset'])
            f.write(decrypted_part)
        
        with lock:
            progress_state['completed'] += 1
            percent = (progress_state['completed'] / progress_state['total']) * 100
            print(f"[{percent:6.2f}%] Part {part_key_info['Index']} downloaded and saved.")
    except Exception as e:
        print(f"Error processing part {part_key_info['Index']}: {e}")

def run_client():
    parser = argparse.ArgumentParser(description="Crypto Transfer Client")
    parser.add_argument("--server-url", required=True, help="Server URL (e.g., http://localhost:8080)")
    parser.add_argument("--target-file", help="Full path of the file on the server")
    parser.add_argument("--file-index", type=int, help="Index of the file on the server")
    parser.add_argument("--key", required=True, help="Base64 encoded encryption key")
    parser.add_argument("--threads", type=int, default=4, help="Number of parallel download threads")
    args = parser.parse_args()

    if args.target_file is None and args.file_index is None:
        parser.error("either --target-file or --file-index must be provided")

    crypto = CtCryptoService(args.key)
    
    try:
        # Initiate
        init_data = {}
        if args.target_file:
            init_data["FilePath"] = args.target_file
            print(f"Initiating transfer for: {args.target_file}")
        else:
            init_data["FileIndex"] = args.file_index
            print(f"Initiating transfer for file index: {args.file_index}")

        encrypted_init = crypto.encrypt_object(init_data)
        data = json.dumps(encrypted_init).encode('utf-8')
        req = urllib.request.Request(f"{args.server_url}/initiate", data=data, headers={'Content-Type': 'application/json'}, method='POST')
        
        with urllib.request.urlopen(req) as f:
            file_map = json.loads(f.read().decode('utf-8'))
        
        file_path = file_map['FilePath']
        file_length = file_map['FileLength']
        parts_enc = file_map['Parts']
        
        local_file_name = os.path.basename(file_path)
        print(f"Downloading: {local_file_name}")
        print(f"Total Size: {file_length} bytes")
        print(f"Total Parts: {len(parts_enc)}")
        
        if not os.path.exists(local_file_name):
            with open(local_file_name, "wb") as f:
                if file_length > 0:
                    f.truncate(file_length)
        
        progress_state = {'completed': 0, 'total': len(parts_enc)}
        lock = threading.Lock()
        
        # Sort parts by index for better display
        decrypted_parts = []
        for k_enc, v_enc in parts_enc.items():
            k = crypto.decrypt_object(k_enc)
            v = crypto.decrypt_object(v_enc)
            decrypted_parts.append((k, v))
        
        decrypted_parts.sort(key=lambda x: x[0]['Index'])

        with ThreadPoolExecutor(max_workers=args.threads) as executor:
            for k, v in decrypted_parts:
                executor.submit(download_part, args.server_url, crypto, k, v, local_file_name, progress_state, lock)
        
        print("Transfer completed.")
    except Exception as e:
        print(f"An error occurred: {e}")

if __name__ == "__main__":
    run_client()
