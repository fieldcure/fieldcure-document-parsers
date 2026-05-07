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
    with these target filenames (the .NET wrapper hardcodes both DLL names
    in Tesseract.Interop.Constants):
      - tesseract50.dll       (renamed from tesseract55.dll)
      - leptonica-1.82.0.dll  (renamed from leptonica-1.87.0.dll, AND tesseract50.dll's
                               import table is patched to reference this name)

    Why the leptonica rename + PE patch: charlesw/tesseract pre-loads
    "leptonica-1.82.0" by exact filename via LibraryLoader.Instance.LoadLibrary
    before loading tesseract50.dll. If our DLL is named leptonica-1.87.0.dll
    the preload fails with DllNotFoundException. Renaming the file is not
    enough on its own — tesseract55.dll's PE import table also references
    "leptonica-1.87.0.dll" by name, so we patch the import string in-place
    (same byte length: 21 bytes including the trailing null). Authenticode
    re-signing happens after the patch.

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

# Replace an ASCII byte sequence in a binary file in-place. Both strings
# must be the same length; the function asserts at least one occurrence.
# Used to retarget tesseract50.dll's leptonica import name from the vcpkg
# build's "leptonica-1.87.0.dll" to the wrapper-expected "leptonica-1.82.0.dll".
function Edit-AsciiBytesInFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Old,
        [Parameter(Mandatory)][string]$New
    )
    if ($Old.Length -ne $New.Length) {
        throw "Edit-AsciiBytesInFile: replacement length differs ($($Old.Length) vs $($New.Length)). PE import names must be patched in-place to keep section offsets stable."
    }
    $oldBytes = [System.Text.Encoding]::ASCII.GetBytes($Old)
    $newBytes = [System.Text.Encoding]::ASCII.GetBytes($New)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $count = 0
    for ($i = 0; $i -le $bytes.Length - $oldBytes.Length; $i++) {
        $match = $true
        for ($j = 0; $j -lt $oldBytes.Length; $j++) {
            if ($bytes[$i + $j] -ne $oldBytes[$j]) { $match = $false; break }
        }
        if ($match) {
            for ($j = 0; $j -lt $newBytes.Length; $j++) { $bytes[$i + $j] = $newBytes[$j] }
            $count++
        }
    }
    if ($count -eq 0) {
        throw "Edit-AsciiBytesInFile: pattern '$Old' not found in $Path"
    }
    [System.IO.File]::WriteAllBytes($Path, $bytes)
    return $count
}

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

$stagedTess = Join-Path $stageDir 'tesseract50.dll'
$stagedLept = Join-Path $stageDir 'leptonica-1.82.0.dll'

Write-Host "[2/5] Staging DLLs to $stageDir..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
Copy-Item $tessSrc $stagedTess -Force
Copy-Item $leptSrc $stagedLept -Force

Write-Host "[3/5] Patching tesseract50.dll PE import: leptonica-1.87.0.dll -> leptonica-1.82.0.dll..." -ForegroundColor Cyan
$replaced = Edit-AsciiBytesInFile -Path $stagedTess -Old 'leptonica-1.87.0.dll' -New 'leptonica-1.82.0.dll'
Write-Host "  Patched $replaced occurrence(s)." -ForegroundColor Green

Write-Host "[4/5] Verifying patched DLL imports..." -ForegroundColor Cyan
$dumpbin = Get-ChildItem -Path "C:\Program Files\Microsoft Visual Studio\2022" -Filter dumpbin.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -like '*Hostx64\x64\dumpbin.exe' } | Select-Object -First 1
if ($dumpbin) {
    $deps = & $dumpbin.FullName /dependents $stagedTess 2>&1
    $depsText = $deps -join "`n"
    if ($depsText -match 'leptonica-1\.87\.0\.dll') { throw 'tesseract50.dll still imports leptonica-1.87.0.dll after patch.' }
    if ($depsText -notmatch 'leptonica-1\.82\.0\.dll') { throw 'tesseract50.dll does not import leptonica-1.82.0.dll after patch.' }
    Write-Host '  tesseract50.dll -> leptonica-1.82.0.dll import: OK' -ForegroundColor Green
} else {
    Write-Warning 'dumpbin.exe not found; skipping import verification.'
}

if ($SkipSign) {
    Write-Host "[5/5] -SkipSign set; leaving DLLs unsigned." -ForegroundColor Yellow
} else {
    Write-Host "[5/5] Signing staged DLLs (Authenticode, EV cert)..." -ForegroundColor Cyan
    $signtool = Get-ChildItem -Path 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*\x64\signtool.exe' } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { throw 'signtool.exe not found. Install Windows 10 SDK or pass -SkipSign.' }
    & $signtool.FullName sign /n 'Fieldcure Co., Ltd.' /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $stagedTess $stagedLept
    if ($LASTEXITCODE -ne 0) { throw "signtool failed (exit $LASTEXITCODE). Ensure GlobalSign EV USB dongle is connected." }
    Write-Host '  Signed.' -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Staged ARM64 DLLs:" -ForegroundColor Green
Get-ChildItem $stageDir | Format-Table Name, @{N='Size (MB)';E={'{0:N2}' -f ($_.Length/1MB)}}, LastWriteTime
