#Requires -Version 5.1
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$SelfContained = $true
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$playerRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $playerRoot 'MediaPlayer\MediaPlayer.csproj'
$propsFile = Join-Path $playerRoot 'Directory.Build.props'
$artifactsRoot = Join-Path $playerRoot 'artifacts'

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

$propsContent = Get-Content -Path $propsFile -Raw -Encoding UTF8
if ($propsContent -match '<VersionPrefix>\s*([^<]+)\s*</VersionPrefix>') {
    $version = $matches[1].Trim()
} elseif ($propsContent -match '<Version>\s*([^<]+)\s*</Version>') {
    $version = $matches[1].Trim()
} else {
    throw "Version not found in $propsFile"
}

Write-Host "Packaging FPlayer $version ($Configuration)..."

Get-Process -Name 'FPlayer','MediaPlayer' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$publishDir = Join-Path $artifactsRoot "FPlayer-$version-win-x64"
$zipPath = Join-Path $artifactsRoot "FPlayer-$version-win-x64.zip"

if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', 'win-x64',
    '-o', $publishDir
)

if ($SelfContained) {
    $publishArgs += '--self-contained', 'true'
} else {
    $publishArgs += '--self-contained', 'false'
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed'
}

$runtimeNote = if ($SelfContained) {
    'Self-contained build — .NET Runtime is not required.'
} else {
    'Requires .NET 8 Desktop Runtime.'
}

$readme = @"
FPlayer $version (Windows x64)
==============================

1. Extract all files to one folder.
2. Run FPlayer.exe.

Requirements: Windows 10 19041+ / Windows 11.
$runtimeNote

Settings: %AppData%\MediaUtilities\player\
"@

Set-Content -Path (Join-Path $publishDir 'README.txt') -Value $readme -Encoding UTF8

Start-Sleep -Seconds 2

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host ''
Write-Host "Done."
Write-Host "Folder: $publishDir"
Write-Host "Zip:    $zipPath"
