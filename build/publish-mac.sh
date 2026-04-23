#!/usr/bin/env bash
# build/publish-mac.sh
set -euo pipefail

CONFIG="${CONFIG:-Release}"
RID="${RID:-osx-arm64}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/publish/$RID"
BUNDLE="$OUT/Orbital.app"

rm -rf "$BUNDLE"

dotnet publish "$ROOT/src/Orbital.App/Orbital.App.csproj" \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -o "$OUT/payload"

mkdir -p "$BUNDLE/Contents/MacOS"
mkdir -p "$BUNDLE/Contents/Resources"

cp -R "$OUT/payload/"* "$BUNDLE/Contents/MacOS/"
cp "$ROOT/build/macos/Info.plist.template" "$BUNDLE/Contents/Info.plist"

# Optional icon
if [ -f "$ROOT/build/macos/AppIcon.icns" ]; then
  cp "$ROOT/build/macos/AppIcon.icns" "$BUNDLE/Contents/Resources/AppIcon.icns"
fi

# Make the main binary executable
chmod +x "$BUNDLE/Contents/MacOS/Orbital.App"

ZIP="$OUT/Orbital-macOS-${RID#osx-}.zip"
rm -f "$ZIP"
( cd "$OUT" && zip -r "$(basename "$ZIP")" "Orbital.app" > /dev/null )

echo "Published: $ZIP"
