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

## GitHub Remote Setup

After you create the GitHub repo, wire this local repo to it:

```powershell
git remote add origin https://github.com/Koraji95-coder/Office.git
git push -u origin main
```

## Other Workstation Setup

On the other PC, clone this repo directly into the standard path:

```powershell
git clone https://github.com/Koraji95-coder/Office.git C:\Dev\Daily
```

Then clone `Suite` into `C:\Dev\Suite` and run Suite's workstation bootstrap from the `Suite` repo. If both repos are already in their standard roots, Suite does not need a `-DailyRepoUrl` argument.

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
