# Contributing to Siraj

Bug reports, translation fixes, and pull requests are welcome.

## Reporting issues

Before opening a new issue, check whether it has already been reported. When filing a bug, include:

- Your Windows version (for example Windows 11 24H2) and architecture (x64 or ARM64).
- Siraj version (shown in Settings -> Updates).
- Clear steps to reproduce the problem, plus a screenshot if it is a UI issue.
- Relevant lines from `%LOCALAPPDATA%\Siraj\logs\siraj.log` if the app crashed or logged an error.

## Local setup

You need Windows 10 (1809+) or Windows 11 and the .NET 10 SDK. Inno Setup 6.7+ is only required when building installers.

```powershell
dotnet test tests/Siraj.Core.Tests
dotnet build src/Siraj.App/Siraj.App.csproj -p:Platform=x64
```

To test without touching your real settings or playing audio at night, set:

```powershell
$env:SIRAJ_DATA_DIR = "$env:TEMP\siraj-dev"
$env:SIRAJ_MUTE = "1"
```

## Pull requests

- Keep changes focused on one fix or feature per PR.
- Run `dotnet test tests/Siraj.Core.Tests` and verify a clean `x64` build with zero warnings before submitting.
- Update localization files in `src/Siraj.App/Strings/` (and `strings/`) when adding or changing UI text.
- Keep comments brief and focused on why non-obvious workarounds exist.
