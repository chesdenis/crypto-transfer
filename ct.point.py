import os
import sys
import json
import math
import base64
import hashlib
import argparse
from typing import Dict, Any
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

MAP_CHUNK_SIZE = 64 * 1024 * 1024
FILE_BUF_SIZE = 1024 * 1024
HASH_BUF_SIZE = 1024 * 1024


class CtCryptoService:
    def __init__(self, key_b64: str):
        self.key_b64 = key_b64
        try:
            self.key = base64.b64decode(key_b64)
        except Exception as e:
            print(f"Error decoding encryption key from Base64: {e}")
            sys.exit(1)

        if len(self.key) not in (16, 24, 32):
            print(f"Invalid AES key length: {len(self.key)} bytes")
            sys.exit(1)

    def encrypt_bytes_ctr(self, data: bytes) -> bytes:
        iv = os.urandom(16)
        cipher = Cipher(algorithms.AES(self.key), modes.CTR(iv))
        enc = cipher.encryptor()
        return iv + enc.update(data) + enc.finalize()

    def decrypt_bytes_ctr(self, data: bytes) -> bytes:
        iv = data[:16]
        payload = data[16:]
        cipher = Cipher(algorithms.AES(self.key), modes.CTR(iv))
        dec = cipher.decryptor()
        return dec.update(payload) + dec.finalize()

    def encrypt_object(self, obj: Any) -> str:
        raw = json.dumps(obj, separators=(",", ":")).encode("utf-8")
        encrypted = self.encrypt_bytes_ctr(raw)
        return base64.b64encode(encrypted).decode("ascii")

    def decrypt_object(self, data_b64: str) -> Any:
        encrypted = base64.b64decode(data_b64)
        raw = self.decrypt_bytes_ctr(encrypted)
        return json.loads(raw.decode("utf-8"))


class CtFileProviderService:
    def __init__(self, crypto: CtCryptoService):
        self.crypto = crypto

    def build_map(self, file_path: str, chunk_size: int = MAP_CHUNK_SIZE) -> Dict[str, Any]:
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"File not found: {file_path}")

        file_size = os.path.getsize(file_path)
        total_parts = math.ceil(file_size / chunk_size)
        parts = {}

        for i in range(total_parts):
            offset = i * chunk_size
            length = min(chunk_size, file_size - offset)

            part_key = {
                "FilePath": file_path,
                "Index": i,
                "Total": total_parts
            }
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

    def stream_encrypted_part(self, wfile, file_path: str, start: int, end: int):
        iv = os.urandom(16)
        cipher = Cipher(algorithms.AES(self.crypto.key), modes.CTR(iv))
        enc = cipher.encryptor()

        wfile.write(iv)

        remaining = end - start
        with open(file_path, "rb") as f:
            f.seek(start)
            while remaining > 0:
                chunk = f.read(min(FILE_BUF_SIZE, remaining))
                if not chunk:
                    break
                remaining -= len(chunk)
                wfile.write(enc.update(chunk))

        tail = enc.finalize()
        if tail:
            wfile.write(tail)

    def build_hash(self, file_path: str, start: int, end: int) -> str:
        sha256 = hashlib.sha256()
        remaining = end - start

        with open(file_path, "rb") as f:
            f.seek(start)
            while remaining > 0:
                chunk = f.read(min(HASH_BUF_SIZE, remaining))
                if not chunk:
                    break
                remaining -= len(chunk)
                sha256.update(chunk)

        return base64.b64encode(sha256.digest()).decode("ascii")


class CtPointHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        pass

    def do_GET(self):
        if self.path == "/ping":
            self.send_response(200)
            self.send_header("Content-Type", "text/plain")
            self.send_header("Content-Length", "4")
            self.end_headers()
            self.wfile.write(b"pong")
            return
        self.send_error(404)

    def do_POST(self):
        try:
            if self.path == "/download":
                self.handle_download_fast()
                return

            content_length = int(self.headers.get("Content-Length", "0"))
            post_data = self.rfile.read(content_length)

            body_json = json.loads(post_data.decode("utf-8"))
            data = self.server.crypto.decrypt_object(body_json)

            if self.path == "/initiate":
                self.handle_initiate(data)
            elif self.path == "/check":
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
                self.send_error(400, f"Invalid file index: {file_index}")
                return

        if not file_path:
            self.send_error(400, "Neither FilePath nor FileIndex provided")
            return

        print(f"Initiating transfer for file: {file_path}")
        result = self.server.file_provider.build_map(file_path)
        response_data = json.dumps(result, separators=(",", ":")).encode("utf-8")

        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(response_data)))
        self.end_headers()
        self.wfile.write(response_data)

    def handle_download_fast(self):
        content_length = int(self.headers.get("Content-Length", "0"))
        post_data = self.rfile.read(content_length)

        # download request is plain JSON for speed:
        # {"FilePath":"...","Start":0,"End":12345}
        data = json.loads(post_data.decode("utf-8"))

        file_path = data["FilePath"]
        start = int(data["Start"])
        end = int(data["End"])

        print(f"Downloading part: {file_path} [{start}:{end}]")

        total_len = 16 + (end - start)  # IV + encrypted payload

        self.send_response(200)
        self.send_header("Content-Type", "application/octet-stream")
        self.send_header("Content-Length", str(total_len))
        self.end_headers()

        self.server.file_provider.stream_encrypted_part(
            self.wfile,
            file_path,
            start,
            end
        )

    def handle_check(self, data):
        file_path = data.get("FilePath")
        start = data.get("Start")
        end = data.get("End")

        result = self.server.file_provider.build_hash(file_path, start, end).encode("ascii")

        self.send_response(200)
        self.send_header("Content-Type", "text/plain")
        self.send_header("Content-Length", str(len(result)))
        self.end_headers()
        self.wfile.write(result)


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
        files = [
            f for f in all_items
            if os.path.isfile(os.path.join(directory, f)) and f.endswith(f".{clean_ext}")
        ]

    files.sort()
    return [os.path.abspath(os.path.join(directory, f)) for f in files]


def human_size(size_bytes: int) -> str:
    size_val = float(size_bytes)
    for unit in ["B", "KB", "MB", "GB", "TB"]:
        if size_val < 1024.0:
            return f"{size_val:.2f} {unit}"
        size_val /= 1024.0
    return f"{size_val:.2f} PB"


def main():
    parser = argparse.ArgumentParser(description="Fast Crypto Transfer Point (Server)")
    parser.add_argument("--dir-to-share", default=".", help="Directory to share")
    parser.add_argument("--file-ext", default="*.*", help="File extension filter")
    parser.add_argument("--key", required=True, help="Base64 encoded AES key")
    parser.add_argument("--port", type=int, default=8080, help="Port to listen on")
    args = parser.parse_args()

    crypto = CtCryptoService(args.key)
    file_provider = CtFileProviderService(crypto)
    shared_files = get_files_to_share(args.dir_to_share, args.file_ext)

    print(f"Starting server on port {args.port}")
    print(f"Sharing directory: {os.path.abspath(args.dir_to_share)}")
    print(f"File filter: {args.file_ext}")

    print("\n--- Available Files ---")
    if not shared_files:
        print("No files found.")
    else:
        for i, f_path in enumerate(shared_files):
            print(f"[{i}] {os.path.basename(f_path)} ({human_size(os.path.getsize(f_path))})")
    print("------------------------\n")

    server = ThreadingHTTPServer(("0.0.0.0", args.port), CtPointHandler)
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