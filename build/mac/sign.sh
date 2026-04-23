#!/usr/bin/env bash
# build/mac/sign.sh — codesign + notarize a .app bundle
# Args: $1 = path to .app bundle, $2 = output zip path
set -euo pipefail

APP="$1"
ZIP="$2"
[ -d "$APP" ] || { echo "App bundle not found: $APP"; exit 1; }

: "${CODESIGN_IDENTITY:?Set CODESIGN_IDENTITY in .env (e.g. 'Developer ID Application: Your Name (XXXXXXXXXX)')}"
: "${APPLE_ID:?Set APPLE_ID in .env}"
: "${APPLE_TEAM_ID:?Set APPLE_TEAM_ID in .env}"
: "${APPLE_PASSWORD:?Set APPLE_PASSWORD in .env (app-specific password)}"

ENTITLEMENTS="$(cd "$(dirname "$0")" && pwd)/entitlements.plist"

echo "▸ Signing $APP"
# Deep sign every Mach-O inside. --force overwrites any ad-hoc sigs from dotnet publish.
codesign --deep --force --options runtime --timestamp \
    --entitlements "$ENTITLEMENTS" \
    --sign "$CODESIGN_IDENTITY" \
    "$APP"

echo "▸ Verifying signature"
codesign --verify --deep --strict --verbose=2 "$APP"

echo "▸ Zipping for notarization"
ditto -c -k --keepParent "$APP" "$ZIP"

echo "▸ Submitting to Apple notary"
xcrun notarytool submit "$ZIP" \
    --apple-id "$APPLE_ID" \
    --team-id "$APPLE_TEAM_ID" \
    --password "$APPLE_PASSWORD" \
    --wait

echo "▸ Stapling ticket"
xcrun stapler staple "$APP"

echo "▸ Re-zipping stapled bundle"
rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"

echo "✓ Signed + notarized: $ZIP"
