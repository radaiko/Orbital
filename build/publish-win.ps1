# build/publish-win.ps1
param(
  [string]$Configuration = "Release",
  [string]$OutputDir = "publish/win-x64"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root
try {
    dotnet publish src/Orbital.App/Orbital.App.csproj `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $OutputDir

    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    $zip = "$OutputDir/../Orbital-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Compress-Archive -Path "$OutputDir/*" -DestinationPath $zip
    Write-Host "Published to $zip"
}
finally {
    Pop-Location
}
