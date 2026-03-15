import os
import json
import base64
import hashlib
import math
import argparse
import sys
import urllib.request
from typing import Dict, Any
from http.server import HTTPServer, BaseHTTPRequestHandler, ThreadingHTTPServer
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.backends import default_backend

class CtCryptoService:
    def __init__(self, key_b64: str, crypto_url: str = None):
        self.key_b64 = key_b64
        self.crypto_url = crypto_url
        try:
            self.key = base64.b64decode(key_b64)
            if len(self.key) not in [16, 24, 32]:
                print(f"Warning: Key length is {len(self.key)} bytes. AES requires 16, 24, or 32 bytes.")
        except Exception as e:
            print(f"Error decoding encryption key from Base64: {e}")
            sys.exit(1)

    def encrypt_bytes(self, data: bytes) -> bytes:
        if self.crypto_url:
            try:
                content_b64 = base64.b64encode(data).decode('utf-8')
                payload = {"Key": self.key_b64, "Content": content_b64}
                req_data = json.dumps(payload).encode('utf-8')
                req = urllib.request.Request(f"{self.crypto_url.rstrip('/')}/encrypt", data=req_data, headers={'Content-Type': 'application/json'}, method='POST')
                with urllib.request.urlopen(req) as f:
                    resp_b64 = f.read()
                    return base64.b64decode(resp_b64)
            except Exception as e:
                print(f"Offloading encryption failed: {e}. Falling back to local encryption.")

        iv = os.urandom(16)
        cipher = Cipher(algorithms.AES(self.key), modes.CBC(iv), backend=default_backend())
        encryptor = cipher.encryptor()
        padder = padding.PKCS7(128).padder()
        padded_data = padder.update(data) + padder.finalize()
        encrypted_data = encryptor.update(padded_data) + encryptor.finalize()
        return iv + encrypted_data

    def decrypt_bytes(self, data: bytes) -> bytes:
        if self.crypto_url:
            try:
                content_b64 = base64.b64encode(data).decode('utf-8')
                payload = {"Key": self.key_b64, "Content": content_b64}
                req_data = json.dumps(payload).encode('utf-8')
                req = urllib.request.Request(f"{self.crypto_url.rstrip('/')}/decrypt", data=req_data, headers={'Content-Type': 'application/json'}, method='POST')
                with urllib.request.urlopen(req) as f:
                    return f.read()
            except Exception as e:
                print(f"Offloading decryption failed: {e}. Falling back to local decryption.")

        iv = data[:16]
        encrypted_data = data[16:]
        cipher = Cipher(algorithms.AES(self.key), modes.CBC(iv), backend=default_backend())
        decryptor = cipher.decryptor()
        decrypted_padded_data = decryptor.update(encrypted_data) + decryptor.finalize()
        unpadder = padding.PKCS7(128).unpadder()
        return unpadder.update(decrypted_padded_data) + unpadder.finalize()

    def encrypt_object(self, obj: Any) -> str:
        json_bytes = json.dumps(obj, separators=(',', ':')).encode('utf-8')
        encrypted = self.encrypt_bytes(json_bytes)
        return base64.b64encode(encrypted).decode('utf-8')

    def decrypt_object(self, data_b64: str) -> Any:
        encrypted_bytes = base64.b64decode(data_b64)
        decrypted_bytes = self.decrypt_bytes(encrypted_bytes)
        return json.loads(decrypted_bytes.decode('utf-8'))

class CtFileProviderService:
    def __init__(self, crypto: CtCryptoService):
        self.crypto = crypto

    def build_map(self, file_path: str, chunk_size: int = 64 * 1024 * 1024) -> Dict[str, Any]:
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")
        
        file_size = os.path.getsize(file_path)
        total_parts = math.ceil(file_size / chunk_size)
        parts = {}

        for i in range(total_parts):
            offset = i * chunk_size
            length = min(chunk_size, file_size - offset)
            
            part_key = {"FilePath": file_path, "Index": i, "Total": total_parts}
            part_value = {
                "FilePath": file_path,
                "Offset": offset,
                "Length": length,
                "Index": i,
                "Total": total_parts
            }
            
            encrypted_key = self.crypto.encrypt_object(part_key)
            encrypted_value = self.crypto.encrypt_object(part_value)
            parts[encrypted_key] = encrypted_value
        
        return {
            "FilePath": file_path,
            "FileLength": file_size,
            "Parts": parts
        }

    def build_part(self, file_path: str, start: int, end: int) -> str:
        with open(file_path, "rb") as f:
            f.seek(start)
            data = f.read(end - start)
        
        encrypted_data = self.crypto.encrypt_bytes(data)
        return base64.b64encode(encrypted_data).decode('utf-8')

    def build_hash(self, file_path: str, start: int, end: int) -> str:
        sha256 = hashlib.sha256()
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

class CtPointHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Silence default logging to keep it clean, but feel free to add custom logging here
        pass

    def do_GET(self):
        if self.path == '/ping':
            self.send_response(200)
            self.send_header('Content-type', 'text/plain')
            self.end_headers()
            self.wfile.write(b"pong")
        else:
            self.send_error(404)

    def do_POST(self):
        crypto = self.server.crypto
        file_provider = self.server.file_provider
        
        try:
            content_length = int(self.headers.get('Content-Length', 0))
            post_data = self.rfile.read(content_length)
            
            # Decrypt and read body
            body_json = json.loads(post_data.decode('utf-8'))
            encrypted_bytes = base64.b64decode(body_json)
            decrypted_bytes = crypto.decrypt_bytes(encrypted_bytes)
            data = json.loads(decrypted_bytes.decode('utf-8'))

            if self.path == '/initiate':
                self.handle_initiate(data)
            elif self.path == '/download':
                self.handle_download(data)
            elif self.path == '/check':
                self.handle_check(data)
            else:
                self.send_error(404)
        except Exception as e:
            print(f"Error processing {self.path}: {e}")
            self.send_error(400, str(e))

    def handle_initiate(self, data):
        file_path = data.get("FilePath")
        file_index = data.get("FileIndex")
        
        if file_index is not None:
            shared_files = self.server.shared_files
            if 0 <= file_index < len(shared_files):
                file_path = shared_files[file_index]
                print(f"Resolved file index {file_index} to path: {file_path}")
            else:
                print(f"Invalid file index: {file_index}")
                self.send_error(400, f"Invalid file index: {file_index}")
                return

        if not file_path:
            print("Neither FilePath nor FileIndex provided")
            self.send_error(400, "Neither FilePath nor FileIndex provided")
            return
        
        print(f"Initiating transfer for file: {file_path}")
        try:
            result = self.server.file_provider.build_map(file_path)
            response_data = json.dumps(result).encode('utf-8')
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            self.wfile.write(response_data)
        except Exception as e:
            print(f"Error building map: {e}")
            self.send_error(500)

    def handle_download(self, data):
        file_path = data.get("FilePath")
        start = data.get("Start")
        end = data.get("End")
        
        print(f"Downloading part: {file_path} [{start}:{end}]")
        try:
            result = self.server.file_provider.build_part(file_path, start, end)
            self.send_response(200)
            self.send_header('Content-type', 'text/plain')
            self.end_headers()
            self.wfile.write(result.encode('utf-8'))
        except Exception as e:
            print(f"Error building part: {e}")
            self.send_error(500)

    def handle_check(self, data):
        file_path = data.get("FilePath")
        start = data.get("Start")
        end = data.get("End")
        
        try:
            result = self.server.file_provider.build_hash(file_path, start, end)
            self.send_response(200)
            self.send_header('Content-type', 'text/plain')
            self.end_headers()
            self.wfile.write(result.encode('utf-8'))
        except Exception as e:
            print(f"Error computing hash: {e}")
            self.send_error(500)

def get_files_to_share(directory, extension_filter):
    try:
        all_items = os.listdir(directory)
    except Exception as e:
        print(f"Error listing directory {directory}: {e}")
        return []

    if extension_filter == "*.*":
        files = [f for f in all_items if os.path.isfile(os.path.join(directory, f))]
    else:
        clean_ext = extension_filter.replace("*.", "")
        files = [f for f in all_items if os.path.isfile(os.path.join(directory, f)) and f.endswith(f".{clean_ext}")]

    files.sort()
    # Use absolute paths to avoid ambiguity
    return [os.path.abspath(os.path.join(directory, f)) for f in files]

def main():
    parser = argparse.ArgumentParser(description="Crypto Transfer Point (Server)")
    parser.add_argument("--dir-to-share", default=".", help="Directory to share")
    parser.add_argument("--file-ext", default="*.*", help="File extension filter")
    parser.add_argument("--key", required=True, help="Base64 encoded encryption key")
    parser.add_argument("--port", type=int, default=8080, help="Port to listen on")
    parser.add_argument("--crypto-url", help="URL of the ct.crypto.py offloading service")
    args = parser.parse_args()

    crypto = CtCryptoService(args.key, args.crypto_url)
    file_provider = CtFileProviderService(crypto)
    shared_files = get_files_to_share(args.dir_to_share, args.file_ext)

    print(f"Starting ct.point server on port {args.port}")
    print(f"Sharing directory: {os.path.abspath(args.dir_to_share)}")
    print(f"File filter: {args.file_ext}")
    
    print("\n--- Available Files ---")
    if not shared_files:
        print("No files found.")
    else:
        for i, f_path in enumerate(shared_files):
            size_bytes = os.path.getsize(f_path)
            # Human readable size
            size_val = size_bytes
            for unit in ['B', 'KB', 'MB', 'GB', 'TB']:
                if size_val < 1024.0:
                    size_str = f"{size_val:.2f} {unit}"
                    break
                size_val /= 1024.0
            else:
                size_str = f"{size_val:.2f} PB"
            
            print(f"[{i}] {os.path.basename(f_path)} ({size_str})")
    print("------------------------\n")

    server = ThreadingHTTPServer(('0.0.0.0', args.port), CtPointHandler)
    server.crypto = crypto
    server.file_provider = file_provider
    server.shared_files = shared_files
    
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down server.")
        server.server_close()

if __name__ == "__main__":
    main()
