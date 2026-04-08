<#
.SYNOPSIS
    Pack, sign, and push FieldCure.DocumentParsers.Pdf.Ocr NuGet package.
.EXAMPLE
    .\publish-pdf-ocr.ps1                      # full: pack → sign → push
    .\publish-pdf-ocr.ps1 -SkipPush            # pack → sign only
    .\publish-pdf-ocr.ps1 -SkipSign -SkipPush  # pack only (testing)
#>
param(
    [switch]$SkipSign,
    [switch]$SkipPush,
    [string]$NuGetApiKey
)

. "$PSScriptRoot\nuget-common.ps1"

Invoke-NuGetPublish `
    -Projects @(
        'src\DocumentParsers.Pdf.Ocr\DocumentParsers.Pdf.Ocr.csproj'
    ) `
    -SkipSign:$SkipSign `
    -SkipPush:$SkipPush `
    -NuGetApiKey $NuGetApiKey
