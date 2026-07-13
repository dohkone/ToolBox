param(
    [string]$ProjectRoot = 'D:\new_project',
    [string]$Version = (Get-Date -Format 'yyyy.MM.dd.HHmm'),
    [string]$InnoSetupCompiler = '',
    [switch]$SkipPortableBuild
)

$ErrorActionPreference = 'Stop'

function Find-InnoSetupCompiler {
    param([string]$PreferredPath)

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        if (Test-Path -LiteralPath $PreferredPath) {
            return (Resolve-Path -LiteralPath $PreferredPath).Path
        }

        throw "Inno Setup compiler not found: $PreferredPath"
    }

    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidates = @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        'C:\Program Files (x86)\Inno Setup 5\ISCC.exe',
        'C:\Program Files\Inno Setup 5\ISCC.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Inno Setup is not installed. Please install Inno Setup 6, or pass -InnoSetupCompiler path."
}

function Get-LatestPortablePackage {
    param([string]$DistRoot)

    Get-ChildItem -LiteralPath $DistRoot -Directory -Filter 'EcomTool_Studio_Portable_*' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$distRoot = Join-Path $ProjectRoot 'dist'
$portableScript = Join-Path $ProjectRoot 'scripts\build_portable_package.ps1'
$installerScript = Join-Path $ProjectRoot 'installer\EcomToolStudio.iss'

if (-not (Test-Path -LiteralPath $portableScript)) {
    throw "Portable build script not found: $portableScript"
}

if (-not (Test-Path -LiteralPath $installerScript)) {
    throw "Inno Setup script not found: $installerScript"
}

if (-not $SkipPortableBuild) {
    Write-Output "Building portable package first..."
    & $portableScript
    if ($LASTEXITCODE -ne 0) {
        throw "Portable package build failed."
    }
}

$portablePackage = Get-LatestPortablePackage -DistRoot $distRoot
if (-not $portablePackage) {
    throw "No portable package found under: $distRoot"
}

$sourceDir = $portablePackage.FullName
$outputDir = Join-Path $distRoot 'installer'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$iscc = Find-InnoSetupCompiler -PreferredPath $InnoSetupCompiler

$env:ECOMTOOL_APP_VERSION = $Version
$env:ECOMTOOL_INSTALL_SOURCE = $sourceDir
$env:ECOMTOOL_INSTALL_OUTPUT = $outputDir

Write-Output "Using source package: $sourceDir"
Write-Output "Using Inno Setup compiler: $iscc"
Write-Output "Installer output directory: $outputDir"

& $iscc $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed."
}

$installerPath = Join-Path $outputDir ("EcomTool_Studio_Setup_{0}.exe" -f $Version)
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Write-Output "Installer created: $installerPath"
