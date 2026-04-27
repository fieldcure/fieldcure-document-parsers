<#
.SYNOPSIS
    Stage the BBC "Five Wonders" documentary audio with a cleaned ground truth.

.DESCRIPTION
    The user-supplied script is a YouTube auto-transcript with timestamp
    markers like "0:01" and "27:54" interleaved between paragraphs. We
    strip those (any line that is purely digits + colon), collapse
    whitespace, and write the result as a single line .gt.txt next to
    a copy of the MP3. The original sample folder stays untouched.

    Music annotations like "[Music]" are kept — Whisper will sometimes
    transcribe them as words, sometimes drop them; either way it is fair
    to compare against the user-provided reference.

.EXAMPLE
    .\stage-bbc-five-wonders.ps1 -SampleDir 'E:\Audio Samples' -Destination .\bbc-bench
#>
param(
    [Parameter(Mandatory)]
    [string]$SampleDir,

    [Parameter(Mandatory)]
    [string]$Destination
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$mp3 = Get-ChildItem -Path $SampleDir -Filter 'Five Wonders*.mp3' | Select-Object -First 1
if (-not $mp3) { throw "BBC Five Wonders MP3 not found under $SampleDir" }

$scriptPath = Join-Path $SampleDir 'Five Wonders_script.txt'
if (-not (Test-Path $scriptPath)) { throw "Script not found: $scriptPath" }

if (-not (Test-Path $Destination)) {
    New-Item -ItemType Directory -Path $Destination | Out-Null
}

# Read script, drop timestamp-only lines, normalize whitespace.
$raw = [System.IO.File]::ReadAllText($scriptPath, [System.Text.Encoding]::UTF8)
$lines = $raw -split "(`r`n|`n)"

$kept = New-Object System.Collections.Generic.List[string]
foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrEmpty($trimmed)) { continue }
    # Skip pure timestamp lines (e.g. "0:01", "27:54")
    if ($trimmed -match '^\d{1,3}:\d{2}$') { continue }
    $kept.Add($trimmed)
}

# Join paragraphs with a single space — WER/CER normalization will
# collapse runs of whitespace anyway, but writing a single line keeps
# the file readable.
$cleaned = ($kept -join ' ').Normalize([System.Text.NormalizationForm]::FormC)

$destMp3 = Join-Path $Destination $mp3.Name
Copy-Item $mp3.FullName $destMp3 -Force

$gtPath = Join-Path $Destination ([System.IO.Path]::GetFileNameWithoutExtension($mp3.Name) + '.gt.txt')
[System.IO.File]::WriteAllText($gtPath, $cleaned, [System.Text.UTF8Encoding]::new($false))

$wordCount = ($cleaned -split '\s+' | Where-Object { $_ }).Count

Write-Host "Staged audio: $destMp3"
Write-Host "Staged GT   : $gtPath ($wordCount words after timestamp strip)"
Write-Host ""
Write-Host "Run benchmark (Tiny + Large bookends, ~10 min on this host):"
Write-Host "  cd tools\AudioBenchmark"
Write-Host "  dotnet run -c Release --no-build -- '$Destination' --models Tiny,Large --output ./bbc-baseline.csv --transcripts ./bbc-transcripts"
