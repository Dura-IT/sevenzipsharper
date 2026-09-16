using System;
using Microsoft.Extensions.Logging;
using SevenZipSharper.Interop;

namespace SevenZipSharper.Extraction
{
    internal static class SevenZipExtractorLog
    {
        private static readonly Action<ILogger, ArchiveFormat, Exception?> ArchiveOpenedMessage = LoggerMessage.Define<ArchiveFormat>(
            LogLevel.Information,
            new EventId(100, nameof(ArchiveOpened)),
            "Opened {Format} archive"
        );

        private static readonly Action<ILogger, ArchiveFormat, int, Exception?> ArchiveOpenFailedMessage = LoggerMessage.Define<ArchiveFormat, int>(
            LogLevel.Error,
            new EventId(101, nameof(ArchiveOpenFailed)),
            "Failed to open {Format} archive (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, ArchiveFormat, int, Exception?> ListEntriesFailedMessage = LoggerMessage.Define<ArchiveFormat, int>(
            LogLevel.Error,
            new EventId(102, nameof(ListEntriesFailed)),
            "Failed to list entries in {Format} archive (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, ArchiveFormat, uint, Exception?> ExtractAllCompletedMessage = LoggerMessage.Define<ArchiveFormat, uint>(
            LogLevel.Information,
            new EventId(103, nameof(ExtractAllCompleted)),
            "Extracted entries from {Format} archive ({Count} total)"
        );

        private static readonly Action<ILogger, ArchiveFormat, int, Exception?> ExtractAllFailedMessage = LoggerMessage.Define<ArchiveFormat, int>(
            LogLevel.Error,
            new EventId(104, nameof(ExtractAllFailed)),
            "Failed to extract all from {Format} archive (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, string, Exception?> ExtractEntryCompletedMessage = LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(105, nameof(ExtractEntryCompleted)),
            "Extracted entry {Path}"
        );

        private static readonly Action<ILogger, string, int, Exception?> ExtractEntryFailedMessage = LoggerMessage.Define<string, int>(
            LogLevel.Error,
            new EventId(106, nameof(ExtractEntryFailed)),
            "Failed to extract entry {Path} (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, ArchiveFormat, int, Exception?> ExtractFilteredFailedMessage = LoggerMessage.Define<ArchiveFormat, int>(
            LogLevel.Error,
            new EventId(107, nameof(ExtractFilteredFailed)),
            "Failed to extract filtered entries from {Format} archive (HRESULT: 0x{HResult:X8})"
        );

        public static void ArchiveOpened(ILogger logger, ArchiveFormat format) => ArchiveOpenedMessage(logger, format, null);

        public static void ArchiveOpenFailed(ILogger logger, ArchiveFormat format, int hResult) => ArchiveOpenFailedMessage(logger, format, hResult, null);

        public static void ListEntriesFailed(ILogger logger, ArchiveFormat format, int hResult) => ListEntriesFailedMessage(logger, format, hResult, null);

        public static void ExtractAllCompleted(ILogger logger, ArchiveFormat format, uint count) => ExtractAllCompletedMessage(logger, format, count, null);

        public static void ExtractAllFailed(ILogger logger, ArchiveFormat format, int hResult) => ExtractAllFailedMessage(logger, format, hResult, null);

        public static void ExtractEntryCompleted(ILogger logger, string path) => ExtractEntryCompletedMessage(logger, path, null);

        public static void ExtractEntryFailed(ILogger logger, string path, int hResult) => ExtractEntryFailedMessage(logger, path, hResult, null);

        public static void ExtractFilteredFailed(ILogger logger, ArchiveFormat format, int hResult) =>
            ExtractFilteredFailedMessage(logger, format, hResult, null);

        private static readonly Action<ILogger, ArchiveFormat, OperationResult, Exception?> ExtractionHadEntryErrorsMessage = LoggerMessage.Define<
            ArchiveFormat,
            OperationResult
        >(
            LogLevel.Error,
            new EventId(108, nameof(ExtractionHadEntryErrors)),
            "Extraction from {Format} archive completed with entry errors: {OperationResult}"
        );

        public static void ExtractionHadEntryErrors(ILogger logger, ArchiveFormat format, OperationResult result) =>
            ExtractionHadEntryErrorsMessage(logger, format, result, null);

        private static readonly Action<ILogger, ArchiveFormat, int, Exception?> ArchiveCloseFailedMessage = LoggerMessage.Define<ArchiveFormat, int>(
            LogLevel.Warning,
            new EventId(109, nameof(ArchiveCloseFailed)),
            "Failed to close {Format} archive during disposal (HRESULT: 0x{HResult:X8})"
        );

        public static void ArchiveCloseFailed(ILogger logger, ArchiveFormat format, int hResult) => ArchiveCloseFailedMessage(logger, format, hResult, null);
    }
}
