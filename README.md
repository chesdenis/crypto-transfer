# crypto-transfer
Secured content transfer

```bash
dotnet run -- --mode=Server --dir-to-share=/Users/dchesnokov/Desktop/large-files-samples --file-ext=iso --chunk-size=100000000 --chunk-map=/Users/dchesnokov/Desktop/large-files-samples/chunkMap.json --enc-key=abcde
```

```bash
dotnet run -- --mode=Downloader --server-url=http://localhost:8080 --threads=3 --chunk-map=/Users/dchesnokov/Desktop/large-files-samples/chunkMap.json --output=/Users/dchesnokov/Desktop/large-files-output
```

```bash
dotnet run -- --mode=Combiner --chunk-map=/Users/dchesnokov/Desktop/large-files-samples/chunkMap.json --output=/Users/dchesnokov/Desktop/large-files-output --enc-key=abcde
```
