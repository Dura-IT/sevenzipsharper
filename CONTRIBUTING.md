# Contributing to SevenZipSharper

## Prerequisites

- [.NET 11 SDK](https://dotnet.microsoft.com/download)
- .NET 8 and .NET 10 runtimes (the tests run on both target frameworks)
- CSharpier (installed as a local tool — see below)

## Getting started

```bash
git clone git@github.com:Dura-IT/SevenZipSharper.git
cd SevenZipSharper
dotnet tool restore        # installs dotnet-csharpier from dotnet-tools.json
dotnet build
dotnet test
```

## Code style

All `.cs` files are formatted with [CSharpier](https://csharpier.com/). A pre-commit
hook runs `dotnet csharpier format` on staged files automatically — no manual step
required. If you need to format manually:

```bash
dotnet csharpier format .
```

The build is warning-free; `SonarAnalyzer.CSharp` is included via `Directory.Build.props`
and all analyzer issues must be resolved before a PR is opened.

## Tests

Tests live under `tests/` and mirror the source folder structure. Every class has a
corresponding `[ClassName]Tests.cs` with the `[TestOf(typeof(X))]` attribute.

```bash
dotnet test
```

New code must include tests. Method naming convention: `MethodName_Scenario_ExpectedResult`.

## Pull requests

1. Branch from `main`.
2. Keep commits small and focused; write short, descriptive commit messages.
3. Ensure `dotnet build` and `dotnet test` both pass with no warnings.
4. Open the PR against `main` and describe what changed and why.

For significant changes — new public API, architectural decisions, format additions —
open an issue first to discuss the approach before investing time in an implementation.

If you are working on the interop layer (COM interfaces, marshalling, native
library loading), read [ARCHITECTURE.md](ARCHITECTURE.md) first. The cross-platform
PROPVARIANT, BSTR, and wchar_t details are non-obvious and have caused real bugs.

## Reporting bugs

Open a [GitHub issue](https://github.com/Dura-IT/SevenZipSharper/issues). For security
vulnerabilities, follow the [security policy](SECURITY.md) instead.
