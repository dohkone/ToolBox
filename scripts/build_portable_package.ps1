$ErrorActionPreference = 'Stop'

$projectRoot = 'D:\new_project'
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$publishDir = Join-Path $projectRoot ("dist\\ImageKeeper_Portable_" + $timestamp)
$runtimePythonSource = 'C:\Users\Administrator\AppData\Local\Programs\Python\Python310'
$runtimeNodeSource = 'C:\Program Files\nodejs'
$templateLibrarySource = 'D:\temu_auto\temp\文生图模板库_Codex.xlsx'

dotnet publish (Join-Path $projectRoot 'src\ImageKeeper.App\ImageKeeper.App.csproj') `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir

$runtimePythonTarget = Join-Path $publishDir 'runtime\python'
$runtimeNodeTarget = Join-Path $publishDir 'runtime\node'
$workspaceRoot = Join-Path $publishDir 'data\workspace'

New-Item -ItemType Directory -Force -Path $runtimePythonTarget | Out-Null
New-Item -ItemType Directory -Force -Path $runtimeNodeTarget | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'review') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'backup') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'excel') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'assert') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $workspaceRoot 'temp') | Out-Null

Copy-Item -LiteralPath (Join-Path $runtimePythonSource '*') -Destination $runtimePythonTarget -Recurse -Force
Copy-Item -LiteralPath (Join-Path $runtimeNodeSource '*') -Destination $runtimeNodeTarget -Recurse -Force

if (Test-Path $templateLibrarySource) {
    Copy-Item -LiteralPath $templateLibrarySource -Destination (Join-Path $workspaceRoot 'temp\文生图模板库_Codex.xlsx') -Force
}

$readmePath = Join-Path $publishDir 'INSTALL.md'
if (Test-Path (Join-Path $projectRoot 'INSTALL.md')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'INSTALL.md') -Destination $readmePath -Force
}

Write-Output "Portable package created: $publishDir"
