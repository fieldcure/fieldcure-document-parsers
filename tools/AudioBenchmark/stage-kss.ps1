<#
.SYNOPSIS
    Stage a KSS dataset subset for the AudioBenchmark.

.DESCRIPTION
    Reads the KSS transcript.v.1.4.txt file, picks an evenly-spaced
    subset (default 15 files), copies the WAVs into a staging directory,
    and writes <basename>.gt.txt next to each WAV using the second
    Korean column (NFC-encoded). The third column in the transcript is
    NFD-encoded Hangul Jamo and renders "broken" on non-mac systems —
    we deliberately do NOT use it.

    No files in the original KSS folder are modified.

.EXAMPLE
    .\stage-kss.ps1 -Source 'E:\Audio Samples\kss' -Destination .\kss-bench

.EXAMPLE
    .\stage-kss.ps1 -Source 'E:\Audio Samples\kss' -Destination .\kss-bench -SampleCount 30
#>
param(
    [Parameter(Mandatory)]
    [string]$Source,

    [Parameter(Mandatory)]
    [string]$Destination,

    [int]$SampleCount = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$transcriptPath = Join-Path $Source 'transcript.v.1.4.txt'
if (-not (Test-Path $transcriptPath)) {
    throw "Transcript not found: $transcriptPath"
}

# Read all lines as UTF-8 (System.IO.File.ReadAllLines preserves encoding correctly).
$lines = [System.IO.File]::ReadAllLines($transcriptPath, [System.Text.Encoding]::UTF8)
Write-Host "Loaded $($lines.Length) transcript entries."

# Each line: path|korean_nfc|korean_nfc|korean_nfd|duration_sec|english
# We use column 2 (NFC) for the ground truth.
$entries = @()
foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Split('|')
    if ($parts.Length -lt 5) { continue }
    $relPath = $parts[0]
    $kr = $parts[1]
    $duration = [double]$parts[4]
    $entries += [pscustomobject]@{
        Filename = [System.IO.Path]::GetFileName($relPath)
        GroundTruth = $kr
        DurationSeconds = $duration
    }
}

Write-Host "Parsed $($entries.Count) usable entries."

# Pick evenly-spaced indices so the subset spans the full duration distribution.
$step = [math]::Max(1, [int]($entries.Count / $SampleCount))
$picked = @()
for ($i = 0; $i -lt $entries.Count -and $picked.Count -lt $SampleCount; $i += $step) {
    $picked += $entries[$i]
}

Write-Host "Selected $($picked.Count) files (every ${step}th entry)."

# Stage: copy WAV + write GT. NFC-normalize the GT explicitly so any stray
# decomposed sequences are collapsed before WER/CER comparison.
if (-not (Test-Path $Destination)) {
    New-Item -ItemType Directory -Path $Destination | Out-Null
}

$copied = 0
foreach ($entry in $picked) {
    $srcWav = Join-Path $Source $entry.Filename
    if (-not (Test-Path $srcWav)) {
        Write-Warning "Missing wav: $srcWav (skipped)"
        continue
    }

    $destWav = Join-Path $Destination $entry.Filename
    Copy-Item $srcWav $destWav -Force

    $gtBasename = [System.IO.Path]::GetFileNameWithoutExtension($entry.Filename)
    $destGt = Join-Path $Destination "$gtBasename.gt.txt"
    $nfcText = $entry.GroundTruth.Normalize([System.Text.NormalizationForm]::FormC)
    [System.IO.File]::WriteAllText($destGt, $nfcText, [System.Text.UTF8Encoding]::new($false))

    $copied++
}

Write-Host ""
Write-Host "Staged $copied file(s) at $Destination"
Write-Host "Total audio duration: $([math]::Round((($picked | Measure-Object DurationSeconds -Sum).Sum), 1)) sec"
Write-Host ""
Write-Host "Run benchmark:"
Write-Host "  dotnet run -c Release -- '$Destination' --output ./kss-baseline.csv"
