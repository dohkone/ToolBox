$ErrorActionPreference = 'Stop'

$projectRoot = 'D:\new_project'
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$publishDir = Join-Path $projectRoot ("dist\EcomTool_Studio_Portable_" + $timestamp)
$runtimePythonSource = 'C:\Users\Administrator\AppData\Local\Programs\Python\Python310'
$runtimeNodeSource = 'C:\Program Files\nodejs'
$playwrightBrowsersSource = Join-Path $projectRoot 'runtime\playwright-browsers'
$templateLibrarySource = Join-Path $projectRoot 'tools\python\template-random-generate\data\文生图模板库_Codex.xlsx'

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

if (Test-Path -LiteralPath $templateLibrarySource) {
    Copy-Item -LiteralPath $templateLibrarySource -Destination (Join-Path $workspaceRoot 'temp\文生图模板库_Codex.xlsx') -Force
}

$readmePath = Join-Path $publishDir 'INSTALL.md'
if (Test-Path -LiteralPath (Join-Path $projectRoot 'INSTALL.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL.md') -Destination $readmePath -Force
}

Write-Output "Portable package created: $publishDir"
