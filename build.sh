#!/bin/bash
set -e

PROJECT="SpotyDownloaderMP/SpotyDownloaderMP.csproj"
OUTPUT="dist"

echo "==> Limpiando dist anterior..."
rm -rf "$OUTPUT"
mkdir -p "$OUTPUT"

echo ""
echo "==> Compilando para Linux (x64)..."
dotnet publish "$PROJECT" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o "$OUTPUT/linux"

echo ""
echo "==> Compilando para Windows (x64)..."
dotnet publish "$PROJECT" \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  -o "$OUTPUT/windows"

echo ""
echo "==> Empaquetando..."
cd "$OUTPUT"
tar -czf "SpotyDownloaderMP-linux-x64.tar.gz" -C linux .
zip -qr "SpotyDownloaderMP-windows-x64.zip" windows/
cd ..

echo ""
echo "Build completado:"
echo "  Linux:   $OUTPUT/SpotyDownloaderMP-linux-x64.tar.gz"
echo "  Windows: $OUTPUT/SpotyDownloaderMP-windows-x64.zip"
echo ""
du -sh "$OUTPUT"/*.tar.gz "$OUTPUT"/*.zip
