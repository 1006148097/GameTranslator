param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,
    [string]$AlternateImagePath,
    [switch]$Diagnostic
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
trap {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}

Add-Type -AssemblyName System.Runtime.WindowsRuntime

function ConvertTo-WinRtTask {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Operation,
        [Parameter(Mandatory = $true)]
        [type]$ResultType
    )

    $asTaskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq "AsTask" -and
            $_.IsGenericMethod -and
            $_.GetParameters().Count -eq 1
        } |
        Select-Object -First 1

    $genericMethod = $asTaskMethod.MakeGenericMethod($ResultType)
    return $genericMethod.Invoke($null, @($Operation))
}

function Await-WinRtOperation {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Operation,
        [Parameter(Mandatory = $true)]
        [type]$ResultType
    )

    $task = ConvertTo-WinRtTask $Operation $ResultType
    $task.Wait()
    return $task.Result
}

$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Storage.FileAccessMode, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics.Imaging, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType = WindowsRuntime]
$null = [Windows.Media.Ocr.OcrResult, Windows.Media.Ocr, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]
$null = [Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime]

function Get-SoftwareBitmapFromPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $file = Await-WinRtOperation `
        ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Path)) `
        ([Windows.Storage.StorageFile])
    $stream = Await-WinRtOperation `
        ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) `
        ([Windows.Storage.Streams.IRandomAccessStream])
    $decoder = Await-WinRtOperation `
        ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) `
        ([Windows.Graphics.Imaging.BitmapDecoder])
    return Await-WinRtOperation `
        ($decoder.GetSoftwareBitmapAsync()) `
        ([Windows.Graphics.Imaging.SoftwareBitmap])
}

$softwareBitmaps = @(Get-SoftwareBitmapFromPath $ImagePath)
if (-not [string]::IsNullOrWhiteSpace($AlternateImagePath)) {
    $softwareBitmaps += Get-SoftwareBitmapFromPath $AlternateImagePath
}

function Get-PrimaryLanguage {
    param([string]$LanguageTag)
    return ($LanguageTag -split "-")[0].ToLowerInvariant()
}

function Get-OcrScore {
    param(
        [string]$Text,
        [string]$PrimaryLanguage
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return -1
    }

    $compactLength = ([regex]::Replace($Text, "\s", "")).Length
    $hangul = ([regex]::Matches($Text, "[\u1100-\u11ff\u3130-\u318f\uac00-\ud7af]")).Count
    $kana = ([regex]::Matches($Text, "[\u3040-\u30ff\u31f0-\u31ff]")).Count
    $han = ([regex]::Matches($Text, "[\u3400-\u9fff]")).Count
    $latin = ([regex]::Matches($Text, "[A-Za-z]")).Count
    $score = $compactLength

    switch ($PrimaryLanguage) {
        "ko" { $score += ($hangul * 25) - ($kana * 20) - ($han * 18) }
        "ja" { $score += ($kana * 25) + ($han * 4) - ($hangul * 25) }
        "zh" { $score += ($han * 12) - ($kana * 15) - ($hangul * 25) }
        "en" { $score += ($latin * 8) - (($hangul + $kana + $han) * 15) }
    }
    return $score
}

$supportedPrimaryLanguages = @("ko", "ja", "zh", "en")
$candidates = @()
$pendingCandidates = @()
$variantIndex = 0
foreach ($softwareBitmap in $softwareBitmaps) {
    $seenPrimaryLanguages = @{}
    foreach ($language in [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages) {
        $primaryLanguage = Get-PrimaryLanguage $language.LanguageTag
        if (($supportedPrimaryLanguages -notcontains $primaryLanguage) -or
            $seenPrimaryLanguages.ContainsKey($primaryLanguage)) {
            continue
        }

        $seenPrimaryLanguages[$primaryLanguage] = $true
        $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($language)
        if ($null -eq $engine) {
            continue
        }

        $recognitionTask = ConvertTo-WinRtTask `
            ($engine.RecognizeAsync($softwareBitmap)) `
            ([Windows.Media.Ocr.OcrResult])
        $pendingCandidates += [pscustomobject]@{
            Task = $recognitionTask
            Language = $primaryLanguage
            Variant = $variantIndex
        }
    }
    $variantIndex++
}

if ($pendingCandidates.Count -gt 0) {
    [System.Threading.Tasks.Task]::WaitAll(
        [System.Threading.Tasks.Task[]]@(
            $pendingCandidates | ForEach-Object { $_.Task }
        )
    )
}

foreach ($pending in $pendingCandidates) {
    $result = $pending.Task.Result
    $lineText = ($result.Lines | ForEach-Object { $_.Text }) -join "`n"
    if ([string]::IsNullOrWhiteSpace($lineText)) {
        $lineText = $result.Text
    }
    $candidates += [pscustomobject]@{
        Text = $lineText
        Score = Get-OcrScore $lineText $pending.Language
        Language = $pending.Language
        Variant = $pending.Variant
    }
}

if ($candidates.Count -eq 0) {
    throw "Windows OCR is unavailable. Install a Windows OCR language component."
}

if ($Diagnostic) {
    Write-Output ($candidates | ConvertTo-Json -Compress)
}
else {
    $bestCandidate = $candidates |
        Sort-Object -Property Score -Descending |
        Select-Object -First 1
    Write-Output $bestCandidate.Text
}
