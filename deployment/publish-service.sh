#!/usr/bin/env bash
# Build and publish FolderToApi.Service for Windows (win-x64).
# Use this on Linux/macOS when PowerShell is not available.
# Equivalent to: .\Publish-Service.ps1

set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
PROJECT="$REPO_ROOT/FolderToApi.Service/FolderToApi.Service.csproj"
OUTPUT="$SCRIPT_DIR/publish"

echo "========================================"
echo "FolderToApiService Publish (Linux/macOS)"
echo "========================================"
echo ""

if ! command -v dotnet &>/dev/null; then
  echo "Error: .NET SDK not found. Install .NET 10.0 SDK or later from https://dot.net"
  exit 1
fi

echo "Project:  $PROJECT"
echo "Output:   $OUTPUT"
echo "Runtime:  win-x64"
echo ""

rm -rf "$OUTPUT"
mkdir -p "$OUTPUT"

dotnet publish "$PROJECT" \
  -c Release \
  -r win-x64 \
  -o "$OUTPUT" \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  --self-contained true

# Ensure config files exist in output
for f in appsettings.json appsettings.Development.json appsettings.Production.json; do
  src="$REPO_ROOT/FolderToApi.Service/$f"
  if [ -f "$src" ] && [ ! -f "$OUTPUT/$f" ]; then
    cp "$src" "$OUTPUT/$f"
    echo "Copied $f to output"
  fi
done

echo ""
echo "Publish completed: $OUTPUT"
echo "Next: copy the publish folder to Windows and run Install-Service.ps1 as Administrator."
