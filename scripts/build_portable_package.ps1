$ErrorActionPreference = 'Stop'

$projectRoot = 'D:\new_project'
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$publishDir = Join-Path $projectRoot ("dist\\EcomTool_Studio_Portable_" + $timestamp)
$runtimePythonSource = 'C:\Users\Administrator\AppData\Local\Programs\Python\Python310'
$runtimeNodeSource = 'C:\Program Files\nodejs'
$playwrightBrowsersSource = Join-Path $projectRoot 'runtime\playwright-browsers'
$templateLibrarySource = 'D:\temu_auto\temp\文生图模板库_Codex.xlsx'

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

New-Item -ItemType Directory -Force -Path $runtimePythonTarget | Out-Null
New-Item -ItemType Directory -Force -Path $runtimeNodeTarget | Out-Null
New-Item -ItemType Directory -Force -Path $playwrightBrowsersTarget | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'review') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'backup') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'excel') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'assert') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'temp') | Out-Null

Copy-Item -LiteralPath (Join-Path $runtimePythonSource '*') -Destination $runtimePythonTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $runtimeNodeSource '*') -Destination $runtimeNodeTarget -Recurse -Force
Get-ChildItem -LiteralPath $playwrightBrowsersSource -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $playwrightBrowsersTarget -Recurse -Force
}

$bundledChromiumExe = Join-Path $playwrightBrowsersTarget 'chromium-1228\chrome-win64\chrome.exe'
if (-not (Test-Path $bundledChromiumExe)) {
    throw "Bundled Playwright Chromium was not copied correctly: $bundledChromiumExe"
}

if (Test-Path $templateLibrarySource) {
    Copy-Item -LiteralPath $templateLibrarySource -Destination (Join-Path $workspaceRoot 'temp\文生图模板库_Codex.xlsx') -Force
}

$readmePath = Join-Path $publishDir 'INSTALL.md'
if (Test-Path (Join-Path $projectRoot 'INSTALL.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL.md') -Destination $readmePath -Force
}

Write-Output "Portable package created: $publishDir"
