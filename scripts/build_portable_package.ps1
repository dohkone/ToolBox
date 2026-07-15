$ErrorActionPreference = 'Stop'

$projectRoot = 'D:\new_project'
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$publishDir = Join-Path $projectRoot ("dist\EcomTool_Portable_" + $timestamp)
$runtimePythonSource = 'C:\Users\Administrator\AppData\Local\Programs\Python\Python310'
$runtimeNodeSource = 'C:\Program Files\nodejs'
$playwrightBrowsersSource = Join-Path $projectRoot 'runtime\playwright-browsers'
$templateLibraryFileName = [string]::Concat([char[]](0x6587, 0x751F, 0x56FE, 0x6A21, 0x677F, 0x5E93, 0x005F, 0x0043, 0x006F, 0x0064, 0x0065, 0x0078, 0x002E, 0x0078, 0x006C, 0x0073, 0x0078))
$templateLibrarySource = Join-Path $projectRoot (Join-Path 'tools\python\template-random-generate\data' $templateLibraryFileName)

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        throw "Required runtime source not found: $Source"
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Test-BundledPythonImport {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PythonExe,
        [Parameter(Mandatory = $true)]
        [string]$ImportName
    )

    $previousNoUserSite = $env:PYTHONNOUSERSITE
    $env:PYTHONNOUSERSITE = '1'
    try {
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $PythonExe
        $startInfo.Arguments = "-c `"import $ImportName`""
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true

        $process = [System.Diagnostics.Process]::Start($startInfo)
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        $process.Dispose()

        return $exitCode -eq 0
    }
    finally {
        $env:PYTHONNOUSERSITE = $previousNoUserSite
    }
}

function Ensure-BundledPythonPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PythonExe,
        [Parameter(Mandatory = $true)]
        [string]$PackageName,
        [Parameter(Mandatory = $true)]
        [string]$ImportName
    )

    if (Test-BundledPythonImport -PythonExe $PythonExe -ImportName $ImportName) {
        return
    }

    Write-Output "Installing bundled Python package: $PackageName"
    $previousNoUserSite = $env:PYTHONNOUSERSITE
    $env:PYTHONNOUSERSITE = '1'
    try {
        & $PythonExe -m ensurepip --upgrade
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to bootstrap pip for bundled Python."
        }

        & $PythonExe -m pip install --disable-pip-version-check --no-warn-script-location --force-reinstall $PackageName
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to install bundled Python package: $PackageName"
        }
    }
    finally {
        $env:PYTHONNOUSERSITE = $previousNoUserSite
    }

    if (-not (Test-BundledPythonImport -PythonExe $PythonExe -ImportName $ImportName)) {
        throw "Bundled Python package cannot be imported after install: $ImportName"
    }
}

$env:PLAYWRIGHT_BROWSERS_PATH = $playwrightBrowsersSource
Push-Location (Join-Path $projectRoot 'tools\node\miaoshou-playwright')
try {
    cmd /c npx playwright install chromium
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install bundled Playwright Chromium."
    }
}
finally {
    Pop-Location
}

dotnet publish (Join-Path $projectRoot 'src\ImageKeeper.App\ImageKeeper.App.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir

$runtimePythonTarget = Join-Path $publishDir 'runtime\python'
$runtimeNodeTarget = Join-Path $publishDir 'runtime\node'
$playwrightBrowsersTarget = Join-Path $publishDir 'runtime\playwright-browsers'
$workspaceRoot = Join-Path $publishDir 'data\workspace'

New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'review') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'backup') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'excel') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'assert') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'temp') | Out-Null

Copy-DirectoryContents -Source $runtimePythonSource -Destination $runtimePythonTarget
Copy-DirectoryContents -Source $runtimeNodeSource -Destination $runtimeNodeTarget
Copy-DirectoryContents -Source $playwrightBrowsersSource -Destination $playwrightBrowsersTarget

$bundledChromiumExe = Join-Path $playwrightBrowsersTarget 'chromium-1228\chrome-win64\chrome.exe'
if (-not (Test-Path -LiteralPath $bundledChromiumExe)) {
    throw "Bundled Playwright Chromium was not copied correctly: $bundledChromiumExe"
}

$bundledNodeExe = Join-Path $runtimeNodeTarget 'node.exe'
if (-not (Test-Path -LiteralPath $bundledNodeExe)) {
    throw "Bundled Node.js was not copied correctly: $bundledNodeExe"
}

$bundledPythonExe = Join-Path $runtimePythonTarget 'python.exe'
if (-not (Test-Path -LiteralPath $bundledPythonExe)) {
    throw "Bundled Python was not copied correctly: $bundledPythonExe"
}

Ensure-BundledPythonPackage -PythonExe $bundledPythonExe -PackageName 'openpyxl==3.1.5' -ImportName 'openpyxl'
Ensure-BundledPythonPackage -PythonExe $bundledPythonExe -PackageName 'Pillow==11.3.0' -ImportName 'PIL'

if (Test-Path -LiteralPath $templateLibrarySource) {
    Copy-Item -LiteralPath $templateLibrarySource -Destination (Join-Path (Join-Path $workspaceRoot 'temp') $templateLibraryFileName) -Force
}

$readmePath = Join-Path $publishDir 'INSTALL.md'
if (Test-Path -LiteralPath (Join-Path $projectRoot 'INSTALL.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL.md') -Destination $readmePath -Force
}

Write-Output "Portable package created: $publishDir"
