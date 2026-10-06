# Builds the Thunderstore package: dist/<team>-<name>-<version>.zip
# Checks the package against https://wiki.thunderstore.io/mods/creating-a-package and
# refuses to build a version that is not newer than the one already in the store.
param([string]$Team = "liekos47")

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $repo "CW_Jesse.BetterNetworking"
$bin = Join-Path $proj "bin\Release\net472"

dotnet build (Join-Path $proj "CW_Jesse.BetterNetworking.csproj") -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$manifest = Get-Content (Join-Path $proj "manifest.json") -Raw | ConvertFrom-Json
$name = $manifest.name
$version = $manifest.version_number
$dll = Join-Path $bin "BetterNetworkingPC.dll"

if ($name -notmatch '^[a-zA-Z0-9_]{1,128}$') { throw "manifest name '$name': only a-z A-Z 0-9 _ allowed" }
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "manifest version '$version' is not Major.Minor.Patch" }
if ($manifest.description.Length -gt 250) { throw "manifest description is $($manifest.description.Length) characters, max 250" }
if ((Get-Item $dll).VersionInfo.FileVersion -ne $version) { throw "DLL is $((Get-Item $dll).VersionInfo.FileVersion) but manifest says $version" }

Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Image]::FromFile((Join-Path $proj "icon.png"))
$iconSize = "$($icon.Width)x$($icon.Height)"
$icon.Dispose()
if ($iconSize -ne "256x256") { throw "icon.png is $iconSize, must be 256x256" }

try {
    $published = (Invoke-RestMethod "https://thunderstore.io/api/experimental/package/$Team/$name/").latest.version_number
} catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
    $published = $null
}
if ($published -and [version]$version -le [version]$published) {
    throw "Thunderstore already has $Team-$name $published; bump the version above it (manifest.json, the .csproj and the BepInPlugin attribute)"
}

$stage = Join-Path $repo "dist\stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item $dll, (Join-Path $proj "icon.png"), (Join-Path $proj "manifest.json"), (Join-Path $proj "CHANGELOG.md"), (Join-Path $repo "README.md"), (Join-Path $repo "LICENSE") $stage

$zip = Join-Path $repo "dist\$Team-$name-$version.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip
Remove-Item $stage -Recurse -Force

Write-Host "Store version : $(if ($published) { $published } else { 'not published yet' })"
Write-Host "This version  : $version"
Write-Host "Package       : $zip"
Write-Host "sha256        : $((Get-FileHash $zip).Hash.ToLower())"
