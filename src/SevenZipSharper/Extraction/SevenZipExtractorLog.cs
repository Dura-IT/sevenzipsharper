using System;
using Microsoft.Extensions.Logging;
using SevenZipSharper.Interop;

namespace SevenZipSharper.Extraction;

internal static class SevenZipExtractorLog
{
    private static readonly Action<ILogger, ArchiveFormat, Exception?> _archiveOpened = LoggerMessage.Define<ArchiveFormat>(
        LogLevel.Information,
        new EventId(100, nameof(ArchiveOpened)),
        "Opened {Format} archive"
    );

    private static readonly Action<ILogger, ArchiveFormat, int, Exception?> _archiveOpenFailed = LoggerMessage.Define<ArchiveFormat, int>(
        LogLevel.Error,
        new EventId(101, nameof(ArchiveOpenFailed)),
        "Failed to open {Format} archive (HRESULT: 0x{HResult:X8})"
    );

    private static readonly Action<ILogger, ArchiveFormat, int, Exception?> _listEntriesFailed = LoggerMessage.Define<ArchiveFormat, int>(
        LogLevel.Error,
        new EventId(102, nameof(ListEntriesFailed)),
        "Failed to list entries in {Format} archive (HRESULT: 0x{HResult:X8})"
    );

    private static readonly Action<ILogger, ArchiveFormat, uint, Exception?> _extractAllCompleted = LoggerMessage.Define<ArchiveFormat, uint>(
        LogLevel.Information,
        new EventId(103, nameof(ExtractAllCompleted)),
        "Extracted entries from {Format} archive ({Count} total)"
    );

    private static readonly Action<ILogger, ArchiveFormat, int, Exception?> _extractAllFailed = LoggerMessage.Define<ArchiveFormat, int>(
        LogLevel.Error,
        new EventId(104, nameof(ExtractAllFailed)),
        "Failed to extract all from {Format} archive (HRESULT: 0x{HResult:X8})"
    );

    private static readonly Action<ILogger, string, Exception?> _extractEntryCompleted = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(105, nameof(ExtractEntryCompleted)),
        "Extracted entry {Path}"
    );

    private static readonly Action<ILogger, string, int, Exception?> _extractEntryFailed = LoggerMessage.Define<string, int>(
        LogLevel.Error,
        new EventId(106, nameof(ExtractEntryFailed)),
        "Failed to extract entry {Path} (HRESULT: 0x{HResult:X8})"
    );

    private static readonly Action<ILogger, ArchiveFormat, int, Exception?> _extractFilteredFailed = LoggerMessage.Define<ArchiveFormat, int>(
        LogLevel.Error,
        new EventId(107, nameof(ExtractFilteredFailed)),
        "Failed to extract filtered entries from {Format} archive (HRESULT: 0x{HResult:X8})"
    );

    public static void ArchiveOpened(ILogger logger, ArchiveFormat format) => _archiveOpened(logger, format, null);

    public static void ArchiveOpenFailed(ILogger logger, ArchiveFormat format, int hResult) => _archiveOpenFailed(logger, format, hResult, null);

    public static void ListEntriesFailed(ILogger logger, ArchiveFormat format, int hResult) => _listEntriesFailed(logger, format, hResult, null);

    public static void ExtractAllCompleted(ILogger logger, ArchiveFormat format, uint count) => _extractAllCompleted(logger, format, count, null);

    public static void ExtractAllFailed(ILogger logger, ArchiveFormat format, int hResult) => _extractAllFailed(logger, format, hResult, null);

    public static void ExtractEntryCompleted(ILogger logger, string path) => _extractEntryCompleted(logger, path, null);

    public static void ExtractEntryFailed(ILogger logger, string path, int hResult) => _extractEntryFailed(logger, path, hResult, null);

    public static void ExtractFilteredFailed(ILogger logger, ArchiveFormat format, int hResult) => _extractFilteredFailed(logger, format, hResult, null);

    private static readonly Action<ILogger, ArchiveFormat, OperationResult, Exception?> _extractionHadEntryErrors = LoggerMessage.Define<
        ArchiveFormat,
        OperationResult
    >(LogLevel.Error, new EventId(108, nameof(ExtractionHadEntryErrors)), "Extraction from {Format} archive completed with entry errors: {OperationResult}");

    public static void ExtractionHadEntryErrors(ILogger logger, ArchiveFormat format, OperationResult result) =>
        _extractionHadEntryErrors(logger, format, result, null);

    private static readonly Action<ILogger, ArchiveFormat, int, Exception?> _archiveCloseFailed = LoggerMessage.Define<ArchiveFormat, int>(
        LogLevel.Warning,
        new EventId(109, nameof(ArchiveCloseFailed)),
        "Failed to close {Format} archive during disposal (HRESULT: 0x{HResult:X8})"
    );

    public static void ArchiveCloseFailed(ILogger logger, ArchiveFormat format, int hResult) => _archiveCloseFailed(logger, format, hResult, null);
}
