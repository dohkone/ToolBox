param(
    [string]$ProjectRoot = 'D:\new_project',
    [string]$Version = (Get-Date -Format 'yyyy.MM.dd.HHmm'),
    [string]$InnoSetupCompiler = '',
    [string]$ReleaseBaseUrl = 'https://github.com/dohkone/ToolBox/releases/download',
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

    Get-ChildItem -LiteralPath $DistRoot -Directory -Filter 'EcomTool_Portable_*' |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
}

function Find-DotNetFrameworkCompiler {
    $candidates = @(
        'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
        'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Desktop .NET Framework C# compiler not found."
}

function ConvertTo-CSharpStringContent {
    param([string]$Value)
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$distRoot = Join-Path $ProjectRoot 'dist'
$portableScript = Join-Path $ProjectRoot 'scripts\build_portable_package.ps1'
$installerScript = Join-Path $ProjectRoot 'installer\EcomToolStudio.iss'
$updaterScript = Join-Path $ProjectRoot 'installer\EcomToolUpdater.iss'
$tinyUpdaterTemplate = Join-Path $ProjectRoot 'installer\EcomToolTinyUpdater.cs'

if (-not (Test-Path -LiteralPath $portableScript)) {
    throw "Portable build script not found: $portableScript"
}

if (-not (Test-Path -LiteralPath $installerScript)) {
    throw "Inno Setup script not found: $installerScript"
}

if (-not (Test-Path -LiteralPath $updaterScript)) {
    throw "Updater Inno Setup script not found: $updaterScript"
}

if (-not (Test-Path -LiteralPath $tinyUpdaterTemplate)) {
    throw "Tiny updater template not found: $tinyUpdaterTemplate"
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

$installerPath = Join-Path $outputDir ("EcomTool_Setup_{0}.exe" -f $Version)
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "Installer was not created: $installerPath"
}

Write-Output "Installer created: $installerPath"

$installerFileName = Split-Path -Leaf $installerPath
$installerSize = (Get-Item -LiteralPath $installerPath).Length
$fullInstallerUrl = ('{0}/v{1}/{2}' -f $ReleaseBaseUrl.TrimEnd('/'), $Version, $installerFileName)

$env:ECOMTOOL_FULL_INSTALLER_URL = $fullInstallerUrl
$env:ECOMTOOL_FULL_INSTALLER_NAME = $installerFileName
$env:ECOMTOOL_FULL_INSTALLER_SIZE = [string]$installerSize

Write-Output "Building updater bootstrapper..."
Write-Output "Full installer URL: $fullInstallerUrl"
Write-Output "Full installer size: $installerSize"

& $iscc $updaterScript
if ($LASTEXITCODE -ne 0) {
    throw "Updater build failed."
}

$updaterPath = Join-Path $outputDir ("EcomTool_Update_{0}.exe" -f $Version)
if (-not (Test-Path -LiteralPath $updaterPath)) {
    throw "Updater was not created: $updaterPath"
}

Write-Output "Updater created: $updaterPath"

$legacySortedUpdaterPath = Join-Path $outputDir ("EcomTool_0_Update_{0}.exe" -f $Version)
$tinyUpdaterSource = Get-Content -LiteralPath $tinyUpdaterTemplate -Raw -Encoding UTF8
$tinyUpdaterSource = $tinyUpdaterSource.Replace('__FULL_INSTALLER_URL__', (ConvertTo-CSharpStringContent $fullInstallerUrl))
$tinyUpdaterSource = $tinyUpdaterSource.Replace('__FULL_INSTALLER_NAME__', (ConvertTo-CSharpStringContent $installerFileName))
$tinyUpdaterSource = $tinyUpdaterSource.Replace('__FULL_INSTALLER_SIZE__', [string]$installerSize)
$tinyUpdaterSourcePath = Join-Path ([System.IO.Path]::GetTempPath()) ("EcomToolTinyUpdater_{0}.cs" -f $Version)
Set-Content -LiteralPath $tinyUpdaterSourcePath -Value $tinyUpdaterSource -Encoding UTF8

$csc = Find-DotNetFrameworkCompiler
$iconPath = Join-Path $ProjectRoot 'logo\new_logo.ico'
Write-Output "Building tiny updater bootstrapper..."
Write-Output "Using C# compiler: $csc"
& $csc /nologo /target:winexe /optimize+ /out:$legacySortedUpdaterPath /win32icon:$iconPath /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $tinyUpdaterSourcePath
if ($LASTEXITCODE -ne 0) {
    throw "Tiny updater build failed."
}

Write-Output "Legacy-compatible tiny updater created: $legacySortedUpdaterPath"
