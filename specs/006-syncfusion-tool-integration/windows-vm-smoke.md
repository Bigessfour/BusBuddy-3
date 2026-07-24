# Windows VM smoke checklist — Syncfusion 34.1.32 (Spec 006)

Run inside the Windows guest after [PR #21](https://github.com/Bigessfour/BusBuddy-3/pull/21) is on `master` (shared folder / `git pull`).

## Prerequisites

- .NET 9 SDK in the VM
- `SYNCFUSION_LICENSE_KEY` set as user/machine env (or `SYNCFUSION_LICENSE_KEY.txt` drop-in used by `utm_run_in_vm.ps1`)
- Optional: Ollama if testing local AI chat

## Steps

```powershell
cd <path-to-BusBuddy-3>   # shared folder
git pull origin master
dotnet restore BusBuddy.sln -p:EnableWindowsTargeting=true
dotnet build BusBuddy.sln -c Release -p:EnableWindowsTargeting=true
# Launch:
.\utm_run_in_vm.ps1
# or:
dotnet run --project BusBuddy.WPF/BusBuddy.WPF.csproj -c Release
```

## Pass criteria

- [ ] Build succeeds in VM
- [ ] App starts without Syncfusion trial watermark (license registered)
- [ ] Main window: Dashboard / Students / Routes grids render (SfDataGrid)
- [ ] Theme still FluentDark (or FluentLight) via SfSkinManager — no broken chrome
- [ ] No crash on open of Google Earth / Reports shells

## Record result

Check the boxes above, then mark the VM smoke line in [docs/action-items.md](../../docs/action-items.md) and note date/operator in a PR comment or issue note.
