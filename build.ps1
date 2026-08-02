param(
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $projectRoot "src\GameTranslator"
$outputRoot = Join-Path $projectRoot "dist"
$assetRoot = Join-Path $projectRoot "assets"
$iconPath = Join-Path $assetRoot "logo.ico"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "找不到 Windows C# 编译器：$compiler"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

if (-not (Test-Path -LiteralPath $iconPath)) {
    throw "找不到应用图标：$iconPath"
}

$references = @(
    "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll",
    "C:\Windows\Microsoft.NET\assembly\GAC_64\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll",
    "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll",
    "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.Serialization.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll"
)

foreach ($reference in $references) {
    if (-not (Test-Path -LiteralPath $reference)) {
        throw "缺少编译引用：$reference"
    }
}

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/win32icon:$iconPath",
    "/out:$outputRoot\GameTranslator.exe"
)
$arguments += $references | ForEach-Object { "/reference:$_" }
$arguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" |
    Select-Object -ExpandProperty FullName

& $compiler $arguments
if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出代码：$LASTEXITCODE"
}

Copy-Item -LiteralPath (Join-Path $sourceRoot "MainWindow.xaml") `
    -Destination (Join-Path $outputRoot "MainWindow.xaml") -Force
Copy-Item -LiteralPath (Join-Path $sourceRoot "OcrBridge.ps1") `
    -Destination (Join-Path $outputRoot "OcrBridge.ps1") -Force
Copy-Item -LiteralPath (Join-Path $assetRoot "logo.png") `
    -Destination (Join-Path $outputRoot "logo.png") -Force
Copy-Item -LiteralPath $iconPath `
    -Destination (Join-Path $outputRoot "logo.ico") -Force

Write-Host "BUILD OK: $outputRoot\GameTranslator.exe"

if ($Run) {
    Start-Process -FilePath (Join-Path $outputRoot "GameTranslator.exe")
}
