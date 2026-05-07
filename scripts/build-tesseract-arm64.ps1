<#
.SYNOPSIS
    Builds slim win-arm64 Tesseract + Leptonica DLLs for FieldCure.DocumentParsers.Ocr.

.DESCRIPTION
    Upstream Tesseract NuGet 5.2.0 ships only x64 native DLLs. To support
    win-arm64 (e.g., AssistStudio ARM64), we build tesseract + leptonica from
    source via vcpkg with two customizations:

      1. Overlay-port at scripts/vcpkg-overlay/ports/tesseract removes the
         libcurl + libarchive dependency chain (~10MB of unused runtime libs).
      2. Custom triplet arm64-windows-mixed keeps tesseract + leptonica as
         DLLs while statically linking image format dependencies (libpng,
         libtiff, libjpeg, libwebp, openjpeg, giflib, zlib) into leptonica.

    Result: 2 DLLs (~7MB total) matching the slim footprint of the upstream
    x64 NuGet build, but for arm64-windows.

    The output DLLs are staged at src/DocumentParsers.Ocr/native/win-arm64/
    with these target filenames (the .NET wrapper expects "tesseract50"):
      - tesseract50.dll    (renamed from tesseract55.dll)
      - leptonica-1.87.0.dll  (kept as-is; tesseract50.dll imports this name)

    Native version mismatch with x64 (5.0 vs 5.5) is intentional and harmless:
    Tesseract C API in 5.x adds symbols only — no removals or signature
    changes — so the wrapper's DllImport surface is fully compatible.

.PARAMETER VcpkgRoot
    Path to a classic vcpkg checkout. Defaults to D:\vcpkg.
    Bootstrap a fresh one with:
      git clone https://github.com/microsoft/vcpkg.git D:\vcpkg
      D:\vcpkg\bootstrap-vcpkg.bat -disableMetrics

.PARAMETER SkipSign
    Skip Authenticode signing of the staged DLLs. Useful for local iteration.
    Final shipping builds must be signed (GlobalSign EV, CN=Fieldcure Co., Ltd.).

.NOTES
    Requires Visual Studio 2022 with the ARM64 build tools workload installed.
    First-time build takes ~3-5 minutes (subsequent runs hit the binary cache).

    To upgrade to a newer Tesseract version:
      1. Update REF + SHA512 in scripts/vcpkg-overlay/ports/tesseract/portfile.cmake
      2. Update version in scripts/vcpkg-overlay/ports/tesseract/vcpkg.json
      3. If filename changes (e.g., tesseract56.dll), update the rename below
         AND build/FieldCure.DocumentParsers.Ocr.targets references for x64.
#>

[CmdletBinding()]
param(
    [string]$VcpkgRoot = 'D:\vcpkg',
    [switch]$SkipSign
)

$ErrorActionPreference = 'Stop'

$repoRoot       = Resolve-Path (Join-Path $PSScriptRoot '..')
$overlayPorts   = Join-Path $repoRoot 'scripts\vcpkg-overlay\ports'
$overlayTriplets = Join-Path $repoRoot 'scripts\vcpkg-overlay\triplets'
$stageDir       = Join-Path $repoRoot 'src\DocumentParsers.Ocr\native\win-arm64'

if (-not (Test-Path "$VcpkgRoot\vcpkg.exe")) {
    throw "vcpkg.exe not found at $VcpkgRoot. Bootstrap with: git clone https://github.com/microsoft/vcpkg.git $VcpkgRoot ; $VcpkgRoot\bootstrap-vcpkg.bat"
}

Write-Host "[1/4] Building tesseract:arm64-windows-mixed via vcpkg overlay..." -ForegroundColor Cyan
& "$VcpkgRoot\vcpkg.exe" install tesseract:arm64-windows-mixed `
    --overlay-triplets=$overlayTriplets `
    --overlay-ports=$overlayPorts
if ($LASTEXITCODE -ne 0) { throw "vcpkg install failed (exit $LASTEXITCODE)" }

$installBin = Join-Path $VcpkgRoot 'installed\arm64-windows-mixed\bin'
$tessSrc = Join-Path $installBin 'tesseract55.dll'
$leptSrc = Join-Path $installBin 'leptonica-1.87.0.dll'
foreach ($p in @($tessSrc, $leptSrc)) {
    if (-not (Test-Path $p)) { throw "Expected vcpkg output not found: $p" }
}

Write-Host "[2/4] Staging DLLs to $stageDir..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
Copy-Item $tessSrc (Join-Path $stageDir 'tesseract50.dll') -Force
Copy-Item $leptSrc (Join-Path $stageDir 'leptonica-1.87.0.dll') -Force

Write-Host "[3/4] Verifying renamed DLL imports..." -ForegroundColor Cyan
$dumpbin = Get-ChildItem -Path "C:\Program Files\Microsoft Visual Studio\2022" -Filter dumpbin.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like '*Hostx64\x64\dumpbin.exe' } | Select-Object -First 1
if ($dumpbin) {
    $deps = & $dumpbin.FullName /dependents (Join-Path $stageDir 'tesseract50.dll') 2>&1
    $expected = ($deps -join "`n") -match 'leptonica-1\.87\.0\.dll'
    if (-not $expected) { throw 'tesseract50.dll does not import leptonica-1.87.0.dll — staging is broken.' }
    Write-Host '  tesseract50.dll -> leptonica-1.87.0.dll import: OK' -ForegroundColor Green
} else {
    Write-Warning 'dumpbin.exe not found; skipping import verification.'
}

if ($SkipSign) {
    Write-Host "[4/4] -SkipSign set; leaving DLLs unsigned." -ForegroundColor Yellow
} else {
    Write-Host "[4/4] Signing staged DLLs (Authenticode, EV cert)..." -ForegroundColor Cyan
    $signtool = Get-ChildItem -Path 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*\x64\signtool.exe' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { throw 'signtool.exe not found. Install Windows 10 SDK or pass -SkipSign.' }
    & $signtool.FullName sign /n 'Fieldcure Co., Ltd.' /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
        (Join-Path $stageDir 'tesseract50.dll') (Join-Path $stageDir 'leptonica-1.87.0.dll')
    if ($LASTEXITCODE -ne 0) { throw "signtool failed (exit $LASTEXITCODE). Ensure GlobalSign EV USB dongle is connected." }
    Write-Host '  Signed.' -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Staged ARM64 DLLs:" -ForegroundColor Green
Get-ChildItem $stageDir | Format-Table Name, @{N='Size (MB)';E={'{0:N2}' -f ($_.Length/1MB)}}, LastWriteTime
