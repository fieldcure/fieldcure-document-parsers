<#
.SYNOPSIS
    Pack, sign, and push FieldCure.DocumentParsers.Audio NuGet package.
.EXAMPLE
    .\publish-audio.ps1                      # full: pack - sign - push
    .\publish-audio.ps1 -SkipPush            # pack - sign only
    .\publish-audio.ps1 -SkipSign -SkipPush  # pack only (testing)
#>
param(
    [switch]$SkipSign,
    [switch]$SkipPush,
    [string]$NuGetApiKey
)

. "$PSScriptRoot\nuget-common.ps1"

Invoke-NuGetPublish `
    -Projects @(
        'src\DocumentParsers.Audio\DocumentParsers.Audio.csproj'
    ) `
    -SkipSign:$SkipSign `
    -SkipPush:$SkipPush `
    -NuGetApiKey $NuGetApiKey
