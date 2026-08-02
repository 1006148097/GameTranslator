param()

$ErrorActionPreference = "Stop"
$testDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $testDirectory
$sourceRoot = Join-Path $projectRoot "src\GameTranslator"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$applicationPath = Join-Path $testDirectory "GameTranslator-regression.exe"
$coreTestPath = Join-Path $testDirectory "CoreLogicSmoke-regression.exe"
$ocrTestPath = Join-Path $testDirectory "OcrRegression-regression.exe"
$bridgePath = Join-Path $testDirectory "OcrBridge.ps1"
$generatedPaths = @(
    $applicationPath,
    $coreTestPath,
    $ocrTestPath,
    $bridgePath
)

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

try {
    $applicationArguments = @(
        "/nologo",
        "/target:winexe",
        "/platform:x64",
        "/out:$applicationPath"
    )
    $applicationArguments += $references | ForEach-Object { "/reference:$_" }
    $applicationArguments += Get-ChildItem -LiteralPath $sourceRoot -Filter "*.cs" |
        Select-Object -ExpandProperty FullName
    & $compiler $applicationArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Application test build failed: $LASTEXITCODE"
    }

    Copy-Item -LiteralPath (Join-Path $sourceRoot "OcrBridge.ps1") `
        -Destination $bridgePath -Force

    & $compiler /nologo /target:exe "/out:$coreTestPath" `
        "/reference:$applicationPath" `
        (Join-Path $testDirectory "CoreLogicSmoke.cs")
    if ($LASTEXITCODE -ne 0) {
        throw "Core logic test build failed: $LASTEXITCODE"
    }

    & $compiler /nologo /target:exe "/out:$ocrTestPath" `
        "/reference:$applicationPath" `
        "/reference:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" `
        (Join-Path $testDirectory "OcrRegression.cs")
    if ($LASTEXITCODE -ne 0) {
        throw "OCR regression build failed: $LASTEXITCODE"
    }

    & $coreTestPath
    if ($LASTEXITCODE -ne 0) {
        throw "Core logic regression failed: $LASTEXITCODE"
    }
    & $ocrTestPath
    if ($LASTEXITCODE -ne 0) {
        throw "OCR regression failed: $LASTEXITCODE"
    }
    Write-Host "ALL REGRESSION TESTS PASSED"
}
finally {
    foreach ($generatedPath in $generatedPaths) {
        if (Test-Path -LiteralPath $generatedPath) {
            Remove-Item -LiteralPath $generatedPath -Force
        }
    }
}
