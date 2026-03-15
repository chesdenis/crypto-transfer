# Crypto Transfer Python Implementation

This implementation provides a lightweight, asynchronous file transfer solution using Python 3.10+, suitable for routers (like Keenetic) and Raspberry Pi. It follows the same AES-CBC encryption and chunked delivery logic as the original C# implementation.

## Prerequisites

- **Python 3.10 or higher**
- **pip** (Python package installer)

## Installation

Install the required dependency using pip:

```bash
pip install cryptography
```

### Platform-Specific Instructions

#### OpenWrt (Routers like Keenetic with Entware)

On many routers, storage and RAM are limited. Using `opkg` is the most efficient way to install Python and the encryption library.

```bash
# Update package list
opkg update

# Install Python 3 and basic tools
opkg install python3 python3-pip

# Install required encryption library
opkg install python3-cryptography
```

*Note: If `python3-cryptography` is not available via opkg, use `pip install cryptography`, but ensure you have enough space on your `/opt` or external drive. Networking is handled via built-in libraries, so no additional packages are needed for it.*

#### Raspberry Pi (Raspberry Pi OS / Debian)

On Raspberry Pi, it's best to use a virtual environment to avoid conflicts with system packages.

```bash
# Update system
sudo apt update
sudo apt install python3-venv python3-pip -y

# Create and activate a virtual environment
python3 -m venv ct-env
source ct-env/bin/activate

# Install dependencies
pip install cryptography
```

## Generating a New Encryption Key

To generate a secure 32-byte (AES-256) Base64-encoded encryption key, run the following command in your terminal:

```bash
python3 -c "import os, base64; print(base64.b64encode(os.urandom(32)).decode())"
```

Copy the output and use it for the `--key` argument in both the server and client.

## Usage

### 1. Server (ct.point.py)

The server shares files from a specified directory. It uses a base64 encoded encryption key (AES-256 requires 32 bytes after decoding). Upon startup, it displays a list of available files with their indices.

```bash
python ct.point.py --dir-to-share <DIRECTORY_PATH> --key <BASE64_KEY> [--port <PORT>] [--file-ext <FILTER>] [--crypto-url <CRYPTO_SERVICE_URL>]
```

**Arguments:**
- `--dir-to-share`: (Required) The path to the directory containing files you want to share.
- `--key`: (Required) A Base64-encoded AES encryption key. This must match the key used by the client.
- `--port`: (Optional, default: 8080) The port for the server to listen on.
- `--file-ext`: (Optional, default: *.*) A file extension filter (e.g., `*.iso`).
- `--crypto-url`: (Optional) The URL of the `ct.crypto.py` service to offload encryption/decryption tasks (e.g., `http://192.168.1.100:8081`).

**Example:**
```bash
python ct.point.py --dir-to-share /mnt/data/files --key <BASE64_KEY>
```

### 2. Client (ct.client.py)

The client downloads a specific file from the server, verifying each part's hash and decrypting it on the fly. You can specify the file by its absolute path or by its index (as displayed by the server).

```bash
# Using absolute path
python ct.client.py --server-url <SERVER_URL> --target-file <FULL_PATH_ON_SERVER> --key <BASE64_KEY> [--threads <COUNT>]

# Using file index
python ct.client.py --server-url <SERVER_URL> --file-index <INDEX> --key <BASE64_KEY> [--threads <COUNT>]
```

**Arguments:**
- `--server-url`: (Required) The URL of the `ct.point.py` server (e.g., `http://192.168.1.1:8080`).
- `--target-file`: (Optional*) The **absolute** file path of the file on the server's filesystem as specified in `--dir-to-share`.
- `--file-index`: (Optional*) The numeric index of the file as displayed by the server upon startup.
- `--key`: (Required) The same Base64-encoded AES encryption key used by the server.
- `--threads`: (Optional, default: 4) The number of parallel download threads to use.

*\*Either `--target-file` or `--file-index` must be provided.*

**Example:**
```bash
python ct.client.py --server-url http://192.168.1.1:8080 --file-index 0 --key "SG...0" --threads 8
```

### 3. Crypto Offloading (ct.crypto.py)

This service allows offloading CPU-intensive encryption and decryption tasks to a more powerful node on the network. It requires the encryption key and content to be provided in each request payload.

```bash
python ct.crypto.py [--port <PORT>]
```

**Arguments:**
- `--port`: (Optional, default: 8081) The port for the service to listen on.

**Endpoints:**

- `POST /encrypt`:
    - **Payload**: `{"Key": "<BASE64_KEY>", "Content": "<BASE64_PLAINTEXT>"}`
    - **Returns**: Base64-encoded encrypted data (IV + ciphertext).
- `POST /decrypt`:
    - **Payload**: `{"Key": "<BASE64_KEY>", "Content": "<BASE64_CIPHERTEXT>"}`
    - **Returns**: Raw plaintext bytes.
- `GET /ping`: Health check. Returns `pong`.

**Offloading Example:**
A router running `ct.point.py` can delegate the encryption of file chunks to `ct.crypto.py` running on a more capable machine. Both must use the same encryption key for the transfer to be successful.

## Implementation Notes

- **Resumable Downloads**: The client automatically checks if a file part is already present and matches the server's hash, allowing for resumption of interrupted downloads.
- **Security**: All communication (except `/ping`) is encrypted using AES-CBC with a random IV prepended to the ciphertext. PKCS7 padding is used.
- **Lightweight**: Uses standard Python libraries (`http.server` and `urllib.request`) for networking, ensuring high portability and minimal dependencies on routers and embedded systems.
