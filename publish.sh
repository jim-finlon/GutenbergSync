#!/bin/bash
# Publish script for GutenbergSync

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"
CLI_PROJECT="$PROJECT_DIR/src/GutenbergSync.Cli/GutenbergSync.Cli.csproj"
PUBLISH_DIR="$PROJECT_DIR/publish"

echo "Publishing GutenbergSync..."

# Default to self-contained for Linux x64
RUNTIME="${RUNTIME:-linux-x64}"
SELF_CONTAINED="${SELF_CONTAINED:-true}"

if [ "$SELF_CONTAINED" = "true" ]; then
    echo "Building self-contained deployment for $RUNTIME..."
    dotnet publish "$CLI_PROJECT" \
        -c Release \
        -o "$PUBLISH_DIR" \
        --self-contained true \
        -r "$RUNTIME" \
        -p:PublishSingleFile=false
else
    echo "Building framework-dependent deployment..."
    dotnet publish "$CLI_PROJECT" \
        -c Release \
        -o "$PUBLISH_DIR" \
        --self-contained false
fi

echo ""
echo "✓ Build complete!"
echo "  Executable: $PUBLISH_DIR/gutenberg-sync"
echo "  Size: $(du -sh "$PUBLISH_DIR" | cut -f1)"
echo ""
echo "Run with: $PUBLISH_DIR/gutenberg-sync --help"

