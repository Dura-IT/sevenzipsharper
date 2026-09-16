using System;
using Microsoft.Extensions.Logging;

namespace SevenZipSharper.Compression
{
    internal static class SevenZipCompressorLog
    {
        private static readonly Action<ILogger, uint, ArchiveFormat, Exception?> CompressionCompletedMessage = LoggerMessage.Define<uint, ArchiveFormat>(
            LogLevel.Information,
            new EventId(200, nameof(CompressionCompleted)),
            "Compressed {Count} entries as {Format}"
        );

        private static readonly Action<ILogger, ArchiveFormat, int, Exception?> CompressionFailedMessage = LoggerMessage.Define<ArchiveFormat, int>(
            LogLevel.Error,
            new EventId(201, nameof(CompressionFailed)),
            "Compression failed for {Format} archive (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, int, Exception?> OpenExistingFailedMessage = LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(202, nameof(OpenExistingFailed)),
            "Failed to open existing archive for append (HRESULT: 0x{HResult:X8})"
        );

        private static readonly Action<ILogger, ArchiveFormat, Exception?> AppendNotSupportedMessage = LoggerMessage.Define<ArchiveFormat>(
            LogLevel.Warning,
            new EventId(203, nameof(AppendNotSupported)),
            "Archive format {Format} does not support append"
        );

        private static readonly Action<ILogger, ArchiveFormat, uint, int, Exception?> AppendCompletedMessage = LoggerMessage.Define<ArchiveFormat, uint, int>(
            LogLevel.Information,
            new EventId(204, nameof(AppendCompleted)),
            "Appended entries to {Format} archive ({ExistingCount} existing + {NewCount} new)"
        );

        private static readonly Action<ILogger, ArchiveFormat, uint, Exception?> MultiVolumeCompletedMessage = LoggerMessage.Define<ArchiveFormat, uint>(
            LogLevel.Information,
            new EventId(205, nameof(MultiVolumeCompleted)),
            "Compressed multi-volume {Format} archive ({Count} entries)"
        );

        public static void CompressionCompleted(ILogger logger, uint count, ArchiveFormat format) => CompressionCompletedMessage(logger, count, format, null);

        public static void CompressionFailed(ILogger logger, ArchiveFormat format, int hResult) => CompressionFailedMessage(logger, format, hResult, null);

        public static void OpenExistingFailed(ILogger logger, int hResult) => OpenExistingFailedMessage(logger, hResult, null);

        public static void AppendNotSupported(ILogger logger, ArchiveFormat format) => AppendNotSupportedMessage(logger, format, null);

        public static void AppendCompleted(ILogger logger, ArchiveFormat format, uint existingCount, int newCount) =>
            AppendCompletedMessage(logger, format, existingCount, newCount, null);

        private static readonly Action<ILogger, int, Exception?> SetPropertiesFailedMessage = LoggerMessage.Define<int>(
            LogLevel.Error,
            new EventId(206, nameof(SetPropertiesFailed)),
            "Failed to apply archive properties (HRESULT: 0x{HResult:X8})"
        );

        public static void MultiVolumeCompleted(ILogger logger, ArchiveFormat format, uint count) => MultiVolumeCompletedMessage(logger, format, count, null);

        public static void SetPropertiesFailed(ILogger logger, int hResult) => SetPropertiesFailedMessage(logger, hResult, null);
    }
}
