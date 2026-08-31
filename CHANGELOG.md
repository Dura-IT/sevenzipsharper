# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Package icon, README logo and social preview now use the framed Dura IT mark: the
  isometric box inverted onto a navy tile. The mark carries its own background, so it
  stays legible on dark backgrounds where the previous transparent version lost contrast
  against the surrounding canvas.

## [2.0.0] - 2026-07-28

### Added

- `win-x86` native RID support — the bundled `7z.dll` now covers 32-bit and
  AnyCPU-32-bit .NET processes running under WOW64 on 64-bit Windows. `linux-x64`,
  `linux-arm64`, `win-x64`, `win-arm64`, `win-x86`, `osx-arm64`, and `osx-x64` are
  now all covered by CI integration tests against the real native library (#6).
- `ExtractionOptions` with `FlushIntervalBytes` (default 64 MiB) to control how
  extracted entries are written to disk. When extracting to files, the output is
  now flushed to physical disk every `FlushIntervalBytes`, bounding how far
  decompression can run ahead of OS write-back. Set to `0` to disable and rely on
  OS write-back caching alone (#9).

### Changed

- **Breaking:** `ExtractAllAsync` and both `ExtractAsync` overloads take a new
  optional `ExtractionOptions?` parameter, positioned before `cancellationToken`.
  Callers passing `cancellationToken` positionally must switch to a named argument
  (`cancellationToken:`) or supply the options argument (#9).
- Bumped NuGet dependencies to latest stable: `SonarAnalyzer.CSharp`,
  `Microsoft.Extensions.DependencyInjection.Abstractions`,
  `Microsoft.Extensions.Logging.Abstractions`, `AwesomeAssertions`,
  `Microsoft.NET.Test.Sdk`, `coverlet.collector` (major, 6→10), and
  `SharpSevenZip` (benchmarks only).

### Fixed

- Windows PROPVARIANT stride on `win-x86`: the native PROPVARIANT is 16 bytes on
  32-bit Windows versus 24 bytes on x64/Arm64. `ISetProperties.SetProperties`
  previously assumed the 24-byte stride on all Windows architectures, which would
  misalign multi-parameter compression calls on x86 (#6).
- Sustained large-entry extraction no longer lets decompression outrun disk
  write-back without bound: dirty pages could previously accumulate far ahead of
  physical flush, causing severe kernel I/O/memory contention under high-throughput
  batch extraction. Writes are now paced to disk via periodic `Flush(true)` (#9).

## [1.0.2] - 2026-06-29

### Changed

- Bundled 7-Zip native libraries updated from 26.01 to 26.02 (bug fixes and
  security fixes in upstream 7-Zip).

## [1.0.1] - 2026-06-22

### Added

- Package icon embedded in both `DuraIT.SevenZipSharper` and
  `DuraIT.SevenZipSharper.Native` NuGet packages.

### Fixed

- ZIP compression method fallback accuracy: `LZMA`, `BZip2`, and `PPMd` are
  native ZIP methods and no longer treated as requiring a fallback to Deflate;
  only `LZMA2` falls back (matching 7-Zip behaviour).
- Parallel build race conditions in the native packaging project resolved via
  `Directory.Build.targets` (`FileListAbsolute.txt` write contention and
  `IncrementalClean` interference under parallel builds).

## [1.0.0] - 2026-06-17

First stable release. The public API is declared stable; future breaking changes
will increment the major version.

### Added

- `ARCHITECTURE.md` — technical guide to the COM interop layer, native library
  resolution, PROPVARIANT/BSTR/wchar_t marshalling, and the source-generated
  interface approach.

### Fixed

- Windows PROPVARIANT marshalling (24 vs 16 bytes on x64): `ISetProperties.SetProperties`
  was returning `E_INVALIDARG` (compression silently failed on Windows);
  `IInArchive.GetProperty` / `GetArchiveProperty` could cause
  `AccessViolationException` when reading archive properties. Both interop paths
  are now correct on all platforms.
- `ArchiveEntry.Path` now always uses forward slashes regardless of OS. 7-Zip on
  Windows returns backslash-separated paths; these are now normalized at the
  interop boundary to match the documented contract.

## [0.2.0] - 2026-06-15

### Added

- Password-protected compression for the 7z and Zip formats via the existing
  `CompressionParameters.EncryptionPassword` property. 7z uses AES-256
  natively; Zip emits `em=AES` (WinZip AES, extra field `0x9901`, strength 3)
  so encrypted Zip archives use AES-256 instead of the legacy weak ZipCrypto.
  `EncryptHeaders` (7z only) likewise becomes functional.
- Append paths (`AppendAsync`) handle encrypted archives, including
  header-encrypted 7z files where the existing archive must be opened with
  the password before new entries can be appended.
- Format-level fail-fast validation: setting `EncryptionPassword` on a format
  that does not support encryption (Tar, GZip, BZip2, Xz) returns
  `Result.Fail` instead of silently producing an unencrypted archive.
  `EncryptHeaders` on any non-7z format does the same.
- README: Format × method compatibility matrix derived from
  `FormatFallbackBehaviorTests` (the spec lives in the tests).
- `<example>` XML doc blocks on the public `SevenZipExtractor` and
  `SevenZipCompressor` methods drive IntelliSense.

### Changed

- `ArchiveFormatDetector` is now public with input validation
  (`ArgumentNullException.ThrowIfNull`, `stream.CanRead`) and an
  `<example>` block.

### Fixed

- Sonar security hotspots cleared: workflow steps now reference
  `$SONAR_TOKEN` via the `env:` block instead of `${{ secrets… }}` in the
  `run:` body, and `scripts/fetch-natives.sh` pins curl to HTTPS
  (`--proto '=https'`) to refuse plaintext redirects.

## [0.1.1] - 2026-06-13

### Added

- `scripts/release.sh <version>` — pre-flights semver, branch, clean tree,
  tag clash, and the matching natives GitHub release, then bumps both
  csprojs, commits, tags, and pushes.

### Changed

- CI/CD reorganised so the native 7-Zip binaries live as a GitHub release
  asset keyed by `scripts/7zip-version` (consumed by `ci.yml` and
  `publish.yml`). Managed-side CI no longer rebuilds natives every run.
- `ci.yml` split into a multi-OS compile matrix and a single Ubuntu
  test+sonar job, so integration tests actually run under Sonar coverage
  (previously silently skipped on Linux/Windows).
- Trusted Publishing for NuGet via the `NuGet/login` action.

### Fixed

- README performance framing — explicit, honest comparison against
  SharpSevenZip with benchmark methodology notes.

## [0.1.0] - 2026-06-10

Initial pre-release. Native 7-Zip binaries are not yet bundled; the packages are
published as pre-release pending native library compilation for all supported platforms.

### Added

- `SevenZipExtractor` — `OpenAsync`, `ListEntriesAsync`, `ExtractAllAsync`,
  `ExtractEntryAsync`, `ExtractAsync(filter)` with `CancellationToken` and
  `IProgress<ExtractionProgress>` support
- `SevenZipCompressor` — `CompressAsync`, `CompressFilesAsync`, `AppendAsync`,
  `CompressMultiVolumeAsync` with `CancellationToken` and
  `IProgress<CompressionProgress>` support
- `CompressionParameters` — LZMA2 method, compression level, solid mode, dictionary
  size, thread count, encryption (password + header encryption)
- `ArchiveFormat` enum covering 7-Zip, ZIP, gzip, bzip2, XZ, TAR, WIM, CAB, ARJ,
  LZH, CPIO, and ISO (RAR excluded — see README)
- `ArchiveFormatDetector` — format detection from file extension and magic bytes
- `Result<T>` (FluentResults) error handling throughout; exceptions reserved for
  invariant violations
- `ILogger<T>` integration with `LoggerMessage.Define` on all hot paths
- `AddSevenZipSharper()` `IServiceCollection` extension
- NuGet two-package design: `DuraIT.SevenZipSharper` (managed) +
  `DuraIT.SevenZipSharper.Native` (RID assets); single package reference is all
  consumers need
- RID-based native library bundling for `win-x64`, `win-arm64`, `osx-arm64`,
  `osx-x64`, `linux-x64`, `linux-arm64` via `NativeLibrary.SetDllImportResolver`
- GitHub Actions CI: build + test on Ubuntu, Windows, macOS; pack job

[Unreleased]: https://github.com/Dura-IT/SevenZipSharper/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/Dura-IT/SevenZipSharper/compare/v1.0.2...v2.0.0
[1.0.2]: https://github.com/Dura-IT/SevenZipSharper/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/Dura-IT/SevenZipSharper/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v1.0.0
[0.2.0]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v0.2.0
[0.1.1]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v0.1.1
[0.1.0]: https://github.com/Dura-IT/SevenZipSharper/releases/tag/v0.1.0
