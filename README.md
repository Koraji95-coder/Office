# Daily Office

Separate repo for the `Office` desktop app (`DailyDesk`) and its local knowledge/mockup assets.

## Layout

- `DailyDesk/`: WPF application source
- `Knowledge/`: repo-owned knowledge and seed content
- `Mockups/`: UI mockups and experiments

## Local Roots

Recommended workstation path:

```text
C:\Dev\Daily
```

`Suite Runtime Control` resolves Office from workstation-local config first, then `C:\Dev\Daily`, and only falls back to the old OneDrive path for compatibility.

## Build

```powershell
cd DailyDesk
dotnet build
```

## Run

```powershell
cd DailyDesk
dotnet run
```

## Relationship To Suite

- `Suite` stays in its own repo.
- `Daily Office` stays in this repo.
- `Suite Runtime Control` lives in `Suite` and launches the built Office executable from the workstation-local path.
