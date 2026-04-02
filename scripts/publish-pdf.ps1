<#
.SYNOPSIS
    Pack, sign, and push FieldCure.DocumentParsers.Pdf NuGet package.
.EXAMPLE
    .\publish-pdf.ps1                      # full: pack → sign → push
    .\publish-pdf.ps1 -SkipPush            # pack → sign only
    .\publish-pdf.ps1 -SkipSign -SkipPush  # pack only (testing)
#>
param(
    [switch]$SkipSign,
    [switch]$SkipPush,
    [string]$NuGetApiKey
)

. "$PSScriptRoot\nuget-common.ps1"

Invoke-NuGetPublish `
    -Projects @(
        'src\DocumentParsers.Pdf\DocumentParsers.Pdf.csproj'
    ) `
    -SkipSign:$SkipSign `
    -SkipPush:$SkipPush `
    -NuGetApiKey $NuGetApiKey
