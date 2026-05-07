<#
.SYNOPSIS
    Pack, sign, and push FieldCure.DocumentParsers.Ocr NuGet package.
.EXAMPLE
    .\publish-ocr.ps1                                                # full: pack -> sign -> push
    .\publish-ocr.ps1 -SkipPush                                      # pack -> sign only
    .\publish-ocr.ps1 -SkipSign -SkipPush                            # pack only (testing)
    .\publish-ocr.ps1 -PackageVersion '1.2.0-preview.1'              # publish prerelease nupkg
                                                                     # (csproj <Version> stays as-is)
#>
param(
    [switch]$SkipSign,
    [switch]$SkipPush,
    [string]$NuGetApiKey,
    [string]$PackageVersion
)

. "$PSScriptRoot\nuget-common.ps1"

# When publishing a prerelease via -PackageVersion, MSBuild's /p:PackageVersion
# override propagates to OCR's transitive ProjectReference on Imaging — making
# the resulting nuspec say Imaging dep = "<same prerelease>", which doesn't
# exist on nuget.org. Pin the dep back to the published Imaging version so the
# prerelease nupkg's dep chain resolves cleanly on consumer restore.
$depOverrides = @{}
if ($PackageVersion) {
    $depOverrides = @{
        'FieldCure.DocumentParsers.Imaging' = '1.0.0'
    }
}

Invoke-NuGetPublish `
    -Projects @(
        'src\DocumentParsers.Ocr\DocumentParsers.Ocr.csproj'
    ) `
    -SkipSign:$SkipSign `
    -SkipPush:$SkipPush `
    -NuGetApiKey $NuGetApiKey `
    -PackageVersion $PackageVersion `
    -DependencyOverrides $depOverrides
