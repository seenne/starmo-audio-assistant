param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$SelfContained = $true
)

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dotnetLocal = Join-Path $root '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $dotnetLocal) { $dotnetLocal } else { 'dotnet' }

$project = Join-Path $root 'src\StarAudioAssistant.App\StarAudioAssistant.App.csproj'
if (-not (Test-Path $project)) {
    throw "Project file not found: $project"
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$packageName = "StarAudioAssistant-portable-$Runtime-$timestamp"
$publishDir = Join-Path $root "dist\publish\$packageName"
$packageDir = Join-Path $root 'dist\packages'
$zipPath = Join-Path $packageDir "$packageName.zip"

New-Item -ItemType Directory -Force -Path $publishDir, $packageDir | Out-Null

$selfContainedFlag = if ($SelfContained.IsPresent -and $SelfContained) { 'true' } else { 'false' }

Write-Host "Publishing app..."
& $dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedFlag `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    -o $publishDir

$launcherPath = Join-Path $publishDir 'Start-StarAudioAssistant.cmd'
@"
@echo off
setlocal
set APP_DIR=%~dp0
"%APP_DIR%StarAudioAssistant.App.exe"
"@ | Set-Content -Path $launcherPath -Encoding ASCII

$notesPath = Join-Path $publishDir 'PORTABLE-README.txt'
@"
Star Audio Assistant Portable Package

Run:
- Double-click Start-StarAudioAssistant.cmd
- or run StarAudioAssistant.App.exe directly

Config file location:
- %AppData%\StarAudioAssistant\config.json

Build timestamp: $timestamp
Runtime: $Runtime
Configuration: $Configuration
SelfContained: $selfContainedFlag
"@ | Set-Content -Path $notesPath -Encoding UTF8

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Write-Host "Creating zip package..."
$archiveSucceeded = $false
$maxAttempts = 8
for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
    try {
        Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force
        $archiveSucceeded = $true
        break
    }
    catch {
        if ($attempt -ge $maxAttempts) {
            throw
        }

        Write-Host "Archive attempt $attempt failed (file lock). Retrying..."
        Start-Sleep -Milliseconds 700
    }
}

if (-not $archiveSucceeded) {
    throw "Failed to create package archive after $maxAttempts attempts."
}

Write-Host "Package created: $zipPath"
Write-Output $zipPath
