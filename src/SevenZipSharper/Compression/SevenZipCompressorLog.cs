using System;
using Microsoft.Extensions.Logging;

namespace SevenZipSharper.Compression
{
    internal static class SevenZipCompressorLog
    {
        private static readonly Action<ILogger, uint, ArchiveFormat, Exception?> _compressionCompleted = LoggerMessage.Define<uint, ArchiveFormat>(
            LogLevel.Information,
            new EventId(200, nameof(CompressionCompleted)),
            "Compressed {Count} entries as {Format}"
        );

        private static readonly Action<ILogger, ArchiveFormat, int, Exception?> _compressionFailed = LoggerMessage.Define<ArchiveFormat, int>(
            LogLevel.Error,
            new EventId(201, nameof(CompressionFailed)),
            "Compression failed for {Format} archive (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, int, Exception?> _openExistingFailed = LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(202, nameof(OpenExistingFailed)),
            "Failed to open existing archive for append (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, ArchiveFormat, Exception?> _appendNotSupported = LoggerMessage.Define<ArchiveFormat>(
            LogLevel.Warning,
            new EventId(203, nameof(AppendNotSupported)),
            "Archive format {Format} does not support append"
        );

        private static readonly Action<ILogger, ArchiveFormat, uint, int, Exception?> _appendCompleted = LoggerMessage.Define<ArchiveFormat, uint, int>(
            LogLevel.Information,
            new EventId(204, nameof(AppendCompleted)),
            "Appended entries to {Format} archive ({ExistingCount} existing + {NewCount} new)"
        );

        private static readonly Action<ILogger, ArchiveFormat, uint, Exception?> _multiVolumeCompleted = LoggerMessage.Define<ArchiveFormat, uint>(
            LogLevel.Information,
            new EventId(205, nameof(MultiVolumeCompleted)),
            "Compressed multi-volume {Format} archive ({Count} entries)"
        );

        public static void CompressionCompleted(ILogger logger, uint count, ArchiveFormat format) => _compressionCompleted(logger, count, format, null);

        public static void CompressionFailed(ILogger logger, ArchiveFormat format, int hResult) => _compressionFailed(logger, format, hResult, null);

        public static void OpenExistingFailed(ILogger logger, int hResult) => _openExistingFailed(logger, hResult, null);

        public static void AppendNotSupported(ILogger logger, ArchiveFormat format) => _appendNotSupported(logger, format, null);

        public static void AppendCompleted(ILogger logger, ArchiveFormat format, uint existingCount, int newCount) =>
            _appendCompleted(logger, format, existingCount, newCount, null);

        private static readonly Action<ILogger, int, Exception?> _setPropertiesFailed = LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(206, nameof(SetPropertiesFailed)),
            "Failed to apply archive properties (HRESULT: 0x{HResult:X8})"
        );

        public static void MultiVolumeCompleted(ILogger logger, ArchiveFormat format, uint count) => _multiVolumeCompleted(logger, format, count, null);

        public static void SetPropertiesFailed(ILogger logger, int hResult) => _setPropertiesFailed(logger, hResult, null);
    }
}
