#!/bin/bash

# Peak Unity SDK Documentation Build Script
# This script generates Markdown documentation using DocFX
#
# Usage: ./docs-build.sh [output-dir]
#   output-dir: Directory to output generated documentation (required)
#
# Example:
#   ./docs-build.sh ../../../apps/peak-public-docs/docs/sdks-and-tools/peak-sdk-unity/api

set -e

echo "Peak Unity SDK Documentation Build"
echo "=================================="

# Check for required argument
if [ -z "$1" ]; then
    echo "❌ Error: Output directory is required"
    echo ""
    echo "Usage: $0 <output-dir>"
    echo ""
    echo "Example:"
    echo "  $0 ../../../apps/peak-public-docs/docs/sdks-and-tools/peak-sdk-unity/api"
    exit 1
fi

# Save original directory and convert output path to absolute
ORIGINAL_DIR="$(pwd)"
if [[ "$1" = /* ]]; then
    # Already absolute path
    OUTPUT_DIR="$1"
else
    # Convert relative path to absolute
    OUTPUT_DIR="$ORIGINAL_DIR/$1"
fi

# Get the script directory and navigate to docs directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCS_DIR="$(dirname "$SCRIPT_DIR")"
cd "$DOCS_DIR"

# Check if .NET SDK is available
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK is required but not installed."
    echo "   Please install .NET SDK 6.0 or later from https://dotnet.microsoft.com/download"
    exit 1
fi

# Restore .NET tools
echo "📦 Restoring .NET tools..."
dotnet tool restore --tool-manifest .config/dotnet-tools.json

# Create output directory if it doesn't exist
echo "📁 Output directory: $OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Create temporary docfx.json with the specified output directory
TEMP_DOCFX="docfx.temp.json"
sed "s|__OUTPUT_DIR__|$OUTPUT_DIR|" docfx.json > "$TEMP_DOCFX"

# Generate metadata and Markdown files
echo "📝 Generating documentation metadata..."
dotnet docfx metadata "$TEMP_DOCFX"

# Clean up temporary file
rm -f "$TEMP_DOCFX"

# Remove toc.yml as it's not referenced by markdown files
if [ -f "$OUTPUT_DIR/toc.yml" ]; then
    echo "🗑️  Removing unused toc.yml file..."
    rm "$OUTPUT_DIR/toc.yml"
fi

# Fix MDX compatibility issues
echo "🔧 Fixing MDX compatibility issues..."
OUTPUT_DIR="$OUTPUT_DIR" ./scripts/fix-mdx-compatibility.sh

echo "✅ Documentation generation completed!"
echo ""
echo "📁 Generated files: $OUTPUT_DIR"
