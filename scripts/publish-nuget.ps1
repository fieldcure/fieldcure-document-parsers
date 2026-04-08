<#
.SYNOPSIS
    Pack, sign, and push DocumentParsers NuGet packages.
.EXAMPLE
    .\publish-nuget.ps1                      # full: pack → sign → push
    .\publish-nuget.ps1 -SkipPush            # pack → sign only
    .\publish-nuget.ps1 -SkipSign -SkipPush  # pack only (testing)
#>
param(
    [switch]$SkipSign,
    [switch]$SkipPush,
    [string]$NuGetApiKey
)

. "$PSScriptRoot\nuget-common.ps1"

Invoke-NuGetPublish `
    -Projects @(
        'src\DocumentParsers\DocumentParsers.csproj',
        'src\DocumentParsers.Pdf\DocumentParsers.Pdf.csproj',
        'src\DocumentParsers.Pdf.Ocr\DocumentParsers.Pdf.Ocr.csproj'
    ) `
    -SkipSign:$SkipSign `
    -SkipPush:$SkipPush `
    -NuGetApiKey $NuGetApiKey
