# crypto-transfer
Secured content transfer

```bash
dotnet run -- --mode=Server --file-ext=.zip --chunk-size=100000000 --chunk-map=chunkMap.json
```

```bash
dotnet run -- --mode=Downloader --server-url=http://localhost:8080 --threads=3 --chunk-map=chunkMap.json
```

```bash
dotnet run -- --mode=Combiner --chunk-map=chunkMap.json
```
