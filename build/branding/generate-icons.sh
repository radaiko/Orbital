#!/usr/bin/env bash
# build/branding/generate-icons.sh — rebuild all icon artifacts from icon.svg
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SRC="$SCRIPT_DIR/icon.svg"

need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing: $1"; echo "  $2"; exit 1; }; }
need rsvg-convert "brew install librsvg"
need iconutil     "ships with macOS"
need magick       "brew install imagemagick"

[ -f "$SRC" ] || { echo "Source SVG missing: $SRC"; exit 1; }

# 1. Landing-page copies
mkdir -p "$ROOT/docs"
cp "$SRC" "$ROOT/docs/icon.svg"

# 2. PNG set for landing page, store listings, macOS iconset
SIZES=(16 32 64 128 256 512 1024)
for s in "${SIZES[@]}"; do
    rsvg-convert -w "$s" -h "$s" "$SRC" -o "$SCRIPT_DIR/icon-$s.png"
done

# 3. macOS iconset + icns
ICONSET="$SCRIPT_DIR/AppIcon.iconset"
rm -rf "$ICONSET"
mkdir -p "$ICONSET"
cp "$SCRIPT_DIR/icon-16.png"   "$ICONSET/icon_16x16.png"
cp "$SCRIPT_DIR/icon-32.png"   "$ICONSET/icon_16x16@2x.png"
cp "$SCRIPT_DIR/icon-32.png"   "$ICONSET/icon_32x32.png"
cp "$SCRIPT_DIR/icon-64.png"   "$ICONSET/icon_32x32@2x.png"
cp "$SCRIPT_DIR/icon-128.png"  "$ICONSET/icon_128x128.png"
cp "$SCRIPT_DIR/icon-256.png"  "$ICONSET/icon_128x128@2x.png"
cp "$SCRIPT_DIR/icon-256.png"  "$ICONSET/icon_256x256.png"
cp "$SCRIPT_DIR/icon-512.png"  "$ICONSET/icon_256x256@2x.png"
cp "$SCRIPT_DIR/icon-512.png"  "$ICONSET/icon_512x512.png"
cp "$SCRIPT_DIR/icon-1024.png" "$ICONSET/icon_512x512@2x.png"
iconutil -c icns -o "$SCRIPT_DIR/AppIcon.icns" "$ICONSET"
rm -rf "$ICONSET"

# 4. Windows ICO (16, 32, 48, 256)
magick -background none \
    "$SCRIPT_DIR/icon-16.png" \
    "$SCRIPT_DIR/icon-32.png" \
    "$SCRIPT_DIR/icon-64.png" \
    "$SCRIPT_DIR/icon-256.png" \
    "$SCRIPT_DIR/AppIcon.ico"

# 5. Tray icon — rendered from tray-icon.svg, which has bolder geometry
# that survives 32×32 rasterization. icon.svg's strokes disappear at tray size.
mkdir -p "$ROOT/src/Orbital.App/Assets"
rsvg-convert -w 32 -h 32 "$SCRIPT_DIR/tray-icon.svg" -o "$ROOT/src/Orbital.App/Assets/tray-icon.png"

# 6. Only keep the sizes we care about long-term in branding/
KEEP=(128 256 512 1024)
for s in 16 32 64; do
    rm -f "$SCRIPT_DIR/icon-$s.png"
done

echo "Generated:"
echo "  $SCRIPT_DIR/AppIcon.icns"
echo "  $SCRIPT_DIR/AppIcon.ico"
for s in "${KEEP[@]}"; do
    echo "  $SCRIPT_DIR/icon-$s.png"
done
echo "  $ROOT/src/Orbital.App/Assets/tray-icon.png"
echo "  $ROOT/docs/icon.svg"
