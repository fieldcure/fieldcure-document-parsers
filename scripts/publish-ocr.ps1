<#
.SYNOPSIS
    Pack, sign, and push FieldCure.DocumentParsers.Ocr NuGet package.
.EXAMPLE
    .\publish-ocr.ps1                      # full: pack → sign → push
    .\publish-ocr.ps1 -SkipPush            # pack → sign only
    .\publish-ocr.ps1 -SkipSign -SkipPush  # pack only (testing)
#>
param(
    [switch]$SkipSign,
    [switch]$SkipPush,
    [string]$NuGetApiKey
)

. "$PSScriptRoot\nuget-common.ps1"

Invoke-NuGetPublish `
    -Projects @(
        'src\DocumentParsers.Ocr\DocumentParsers.Ocr.csproj'
    ) `
    -SkipSign:$SkipSign `
    -SkipPush:$SkipPush `
    -NuGetApiKey $NuGetApiKey
