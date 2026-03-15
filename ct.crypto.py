import os
import json
import base64
import argparse
import sys
from typing import Any
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives import padding
from cryptography.hazmat.backends import default_backend

class CtCryptoService:
    @staticmethod
    def decode_key(key_b64: str) -> bytes:
        try:
            key = base64.b64decode(key_b64)
            if len(key) not in [16, 24, 32]:
                print(f"Warning: Key length is {len(key)} bytes. AES requires 16, 24, or 32 bytes.")
            return key
        except Exception as e:
            raise ValueError(f"Error decoding encryption key from Base64: {e}")

    @staticmethod
    def encrypt_bytes(data: bytes, key: bytes) -> bytes:
        iv = os.urandom(16)
        cipher = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
        encryptor = cipher.encryptor()
        padder = padding.PKCS7(128).padder()
        padded_data = padder.update(data) + padder.finalize()
        encrypted_data = encryptor.update(padded_data) + encryptor.finalize()
        return iv + encrypted_data

    @staticmethod
    def decrypt_bytes(data: bytes, key: bytes) -> bytes:
        if len(data) < 16:
            raise ValueError("Data too short for decryption (missing IV)")
        iv = data[:16]
        encrypted_data = data[16:]
        cipher = Cipher(algorithms.AES(key), modes.CBC(iv), backend=default_backend())
        decryptor = cipher.decryptor()
        decrypted_padded_data = decryptor.update(encrypted_data) + decryptor.finalize()
        unpadder = padding.PKCS7(128).unpadder()
        return unpadder.update(decrypted_padded_data) + unpadder.finalize()

class CtCryptoHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        # Silence default logging to keep terminal clean
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
        content_length = int(self.headers.get('Content-Length', 0))
        input_data = self.rfile.read(content_length)

        try:
            # All POST requests now expect a JSON body: {"Key": "...", "Content": "..."}
            # Content should be Base64-encoded to safely transport binary data within JSON.
            body = json.loads(input_data.decode('utf-8'))
            key_b64 = body.get("Key")
            content_b64 = body.get("Content")
            
            if not key_b64 or not content_b64:
                self.send_error(400, "Missing 'Key' or 'Content' in request payload.")
                return

            key = CtCryptoService.decode_key(key_b64)
            data_bytes = base64.b64decode(content_b64)

            if self.path == '/encrypt':
                # Input: JSON {"Key": "...", "Content": "base64_plaintext"}
                # Output: Base64 string of IV + ciphertext (encrypted traffic format)
                encrypted = CtCryptoService.encrypt_bytes(data_bytes, key)
                result = base64.b64encode(encrypted)
                self.send_response(200)
                self.send_header('Content-type', 'text/plain')
                self.end_headers()
                self.wfile.write(result)
                print("Processed encryption request")
                
            elif self.path == '/decrypt':
                # Input: JSON {"Key": "...", "Content": "base64_ciphertext"}
                # Output: Raw plaintext bytes
                decrypted = CtCryptoService.decrypt_bytes(data_bytes, key)
                self.send_response(200)
                self.send_header('Content-type', 'application/octet-stream')
                self.end_headers()
                self.wfile.write(decrypted)
                print("Processed decryption request")
                
            else:
                self.send_error(404)
        except Exception as e:
            print(f"Error processing {self.path}: {e}")
            self.send_error(500, str(e))

def run_server():
    parser = argparse.ArgumentParser(description="Crypto Offloading Service")
    parser.add_argument("--port", type=int, default=8081, help="Port to listen on (default: 8081)")
    args = parser.parse_args()

    server_address = ('', args.port)
    # Using ThreadingHTTPServer for lightweight concurrent handling of multiple parts
    httpd = ThreadingHTTPServer(server_address, CtCryptoHandler)
    
    print(f"Crypto Offloading Service started on port {args.port}")
    print(f"Clients must provide the encryption key in the request payload.")
    httpd.serve_forever()

if __name__ == "__main__":
    run_server()
