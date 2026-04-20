# Scripts — Maintainers Only

## Publish Scripts

| Script | Packages |
|--------|----------|
| `publish-core.ps1` | `FieldCure.DocumentParsers` only |
| `publish-imaging.ps1` | `FieldCure.DocumentParsers.Imaging` only |
| `publish-ocr.ps1` | `FieldCure.DocumentParsers.Ocr` only |
| `publish-nuget.ps1` | All three (Core + Imaging + Ocr) |

```powershell
# Single package
.\scripts\publish-core.ps1                      # pack → sign → push
.\scripts\publish-imaging.ps1                   # pack → sign → push
.\scripts\publish-ocr.ps1                       # pack → sign → push

# All at once
.\scripts\publish-nuget.ps1                     # pack → sign → push
.\scripts\publish-nuget.ps1 -SkipPush           # pack → sign only
.\scripts\publish-nuget.ps1 -SkipSign -SkipPush # pack only (testing)
```

All scripts accept `-SkipSign`, `-SkipPush`, and `-NuGetApiKey` parameters.

## Prerequisites

- GlobalSign EV code signing USB dongle connected
- NuGet.org API Key ([nuget.org/account/apikeys](https://www.nuget.org/account/apikeys))
- Alternatively, set `$env:NUGET_API_KEY` instead of passing `-NuGetApiKey`

## Signing Certificate

- **Issuer**: GlobalSign
- **Subject**: Fieldcure Co., Ltd.
- **Method**: USB token (EV Code Signing)
- **Timestamp**: GlobalSign TSA

## Output

Built `.nupkg` files are placed in the `artifacts/` folder.
