#!/usr/bin/env bash
# build/build.sh — dispatch to the right platform script
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"

case "$(uname -s)" in
    Darwin*) "$DIR/publish-mac.sh" ;;
    Linux*)  echo "Linux build: run 'dotnet publish -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64' manually"; exit 1 ;;
    MINGW*|MSYS*|CYGWIN*)
        echo "Run build/publish-win.ps1 from PowerShell instead"; exit 1 ;;
    *) echo "Unknown platform"; exit 1 ;;
esac
