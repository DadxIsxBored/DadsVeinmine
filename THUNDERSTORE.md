# Thunderstore release

Run:

```powershell
.\build.ps1 -Package
```

Upload the single ZIP in `dist/`. The matching unpacked directory is retained beside it for inspection.

Before release, update the version in:

- `DadsVeinmine.csproj`
- `src/AssemblyInfo.cs`
- `src/DadsVeinminePlugin.cs`
- `package/manifest.json`
- `CHANGELOG.md`
