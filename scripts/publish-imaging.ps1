<#
.SYNOPSIS
    Pack, sign, and push FieldCure.DocumentParsers.Imaging NuGet package.
.EXAMPLE
    .\publish-imaging.ps1                      # full: pack → sign → push
    .\publish-imaging.ps1 -SkipPush            # pack → sign only
    .\publish-imaging.ps1 -SkipSign -SkipPush  # pack only (testing)
#>
param(
    [switch]$SkipSign,
    [switch]$SkipPush,
    [string]$NuGetApiKey
)

. "$PSScriptRoot\nuget-common.ps1"

Invoke-NuGetPublish `
    -Projects @(
        'src\DocumentParsers.Imaging\DocumentParsers.Imaging.csproj'
    ) `
    -SkipSign:$SkipSign `
    -SkipPush:$SkipPush `
    -NuGetApiKey $NuGetApiKey
