#!/usr/bin/env bash
# build/release.sh — one-command local release pipeline
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISH="$ROOT/publish"
MAC_RIDS="${MAC_RIDS:-osx-arm64}"

# --- Preflight ---
echo "▸ Preflight"

# Clean working tree
if ! git -C "$ROOT" diff-index --quiet HEAD --; then
    echo "✗ Working tree has uncommitted changes. Commit or stash first."
    exit 1
fi

# Must be on main
BRANCH="$(git -C "$ROOT" rev-parse --abbrev-ref HEAD)"
[ "$BRANCH" = "main" ] || { echo "✗ Not on main (currently '$BRANCH')"; exit 1; }

# Load .env
ENV_FILE="$ROOT/.env"
[ -f "$ENV_FILE" ] || { echo "✗ $ENV_FILE missing. Copy build/release-env.sample → .env and fill in."; exit 1; }
# shellcheck source=/dev/null
source "$ENV_FILE"

# Check required tools
command -v dotnet    >/dev/null || { echo "✗ dotnet not in PATH"; exit 1; }
command -v gh        >/dev/null || { echo "✗ gh (GitHub CLI) not in PATH"; exit 1; }
command -v xcrun     >/dev/null || { echo "✗ xcrun not in PATH (need Xcode CLI tools)"; exit 1; }
command -v vpk       >/dev/null || { echo "✗ vpk not in PATH. Run: dotnet tool install -g vpk"; exit 1; }
command -v zip       >/dev/null || { echo "✗ zip not in PATH"; exit 1; }
gh auth status >/dev/null 2>&1 || { echo "✗ gh not authenticated. Run 'gh auth login'."; exit 1; }

# --- Version ---
VERSION="$(dotnet msbuild "$ROOT/Directory.Build.props" -getProperty:Version -nologo | tr -d '[:space:]')"
[ -n "$VERSION" ] || { echo "✗ Could not read Version from Directory.Build.props"; exit 1; }
echo "▸ Version: $VERSION"

TAG="v$VERSION"
if git -C "$ROOT" ls-remote origin "refs/tags/$TAG" | grep -q "$TAG"; then
    echo "✗ Tag $TAG already exists on origin. Bump Directory.Build.props Version."
    exit 1
fi

# --- Tests ---
echo "▸ Running tests"
dotnet test "$ROOT/Orbital.sln" -c Release --nologo --verbosity minimal

# --- macOS build + sign + notarize ---
mkdir -p "$PUBLISH"
MAC_ARTIFACTS=()

for RID in $MAC_RIDS; do
    echo "▸ Building macOS bundle for $RID"
    OUT="$PUBLISH/$RID"
    APP="$OUT/Orbital.app"
    rm -rf "$APP"

    dotnet publish "$ROOT/src/Orbital.App/Orbital.App.csproj" \
        -c Release -r "$RID" --self-contained true \
        -p:PublishSingleFile=false \
        -o "$OUT/payload" --nologo --verbosity minimal

    mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
    cp -R "$OUT/payload/"* "$APP/Contents/MacOS/"

    # Info.plist with version substituted
    sed "s/0\.1\.0/$VERSION/g" "$ROOT/build/macos/Info.plist.template" > "$APP/Contents/Info.plist"

    # App icon
    cp "$ROOT/build/branding/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
    # Reference it in Info.plist
    /usr/libexec/PlistBuddy -c "Add :CFBundleIconFile string AppIcon" "$APP/Contents/Info.plist" 2>/dev/null || \
    /usr/libexec/PlistBuddy -c "Set  :CFBundleIconFile AppIcon" "$APP/Contents/Info.plist"

    chmod +x "$APP/Contents/MacOS/Orbital.App"

    # Sign + notarize
    ZIP="$OUT/Orbital-$RID-$VERSION.zip"
    "$ROOT/build/mac/sign.sh" "$APP" "$ZIP"

    MAC_ARTIFACTS+=("$ZIP")
done

echo "▸ macOS artifacts:"
printf '  %s\n' ${MAC_ARTIFACTS[@]+"${MAC_ARTIFACTS[@]}"}

# --- Windows cross-compile ---
echo "▸ Building Windows x64"
WIN_OUT="$PUBLISH/win-x64"
rm -rf "$WIN_OUT"

dotnet publish "$ROOT/src/Orbital.App/Orbital.App.csproj" \
    -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$WIN_OUT/payload" --nologo --verbosity minimal

WIN_ZIP="$WIN_OUT/Orbital-win-x64-$VERSION.zip"
rm -f "$WIN_ZIP"
(cd "$WIN_OUT" && zip -r "$(basename "$WIN_ZIP")" payload > /dev/null)

echo "▸ Windows artifact: $WIN_ZIP"

# --- Velopack pack ---
echo "▸ Packing Velopack update artifacts"
VPK_OUT="$PUBLISH/velopack"
rm -rf "$VPK_OUT"
mkdir -p "$VPK_OUT"

# macOS (one entry per RID)
for RID in $MAC_RIDS; do
    vpk_platform="osx"
    echo "  ▸ vpk pack $RID"
    vpk pack \
        --packId dev.orbital.app \
        --packVersion "$VERSION" \
        --packDir "$PUBLISH/$RID/payload" \
        --mainExe Orbital.App \
        --outputDir "$VPK_OUT/$RID" \
        --runtime "$vpk_platform-$(echo "$RID" | sed 's/osx-//')"
done

# Windows
echo "  ▸ vpk pack win-x64"
vpk pack \
    --packId dev.orbital.app \
    --packVersion "$VERSION" \
    --packDir "$PUBLISH/win-x64/payload" \
    --mainExe Orbital.App.exe \
    --outputDir "$VPK_OUT/win-x64" \
    --runtime win-x64

echo "▸ Velopack packages:"
find "$VPK_OUT" -type f \( -name '*.nupkg' -o -name '*Setup*' -o -name 'RELEASES*' -o -name '*.zip' \)

# --- GitHub release ---
echo "▸ Composing release notes"
NOTES_FILE="$(mktemp)"
trap 'rm -f "$NOTES_FILE"' EXIT

PREV_TAG="$(git -C "$ROOT" describe --tags --abbrev=0 2>/dev/null || true)"
{
    echo "## Orbital $VERSION"
    echo
    if [ -n "$PREV_TAG" ]; then
        echo "Changes since $PREV_TAG:"
        echo
        git -C "$ROOT" log --pretty=format:'- %s' "$PREV_TAG..HEAD"
    else
        echo "Initial public release."
    fi
    echo
} > "$NOTES_FILE"

# Push the tag BEFORE creating the GitHub release so a mid-script failure
# leaves a consistent state: either the tag exists on origin (and the release
# may or may not exist), or neither exists. If we created the release first
# and the push failed, the re-run's tag-existence check at the top would fail
# to detect the orphaned release.
echo "▸ Pushing tag $TAG"
git -C "$ROOT" tag "$TAG" 2>/dev/null || true
git -C "$ROOT" push origin "$TAG"

echo "▸ Creating GitHub release $TAG"
gh release create "$TAG" \
    --title "Orbital $VERSION" \
    --notes-file "$NOTES_FILE"

echo "▸ Uploading artifacts"
for f in ${MAC_ARTIFACTS[@]+"${MAC_ARTIFACTS[@]}"}; do
    gh release upload "$TAG" "$f"
done
gh release upload "$TAG" "$WIN_ZIP"

# All Velopack artifacts (nupkg, RELEASES, Setup, Portable.zip)
find "$VPK_OUT" -maxdepth 2 -type f \( -name '*.nupkg' -o -name '*Setup*' -o -name 'RELEASES*' -o -name '*Portable.zip' \) \
    -exec gh release upload "$TAG" {} \;

URL="$(gh release view "$TAG" --json url -q .url)"
echo "✓ Release complete: $URL"
