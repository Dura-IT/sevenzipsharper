namespace SevenZipSharper
{
    /// <summary>
    /// Top-level metadata for an archive, returned by <see cref="SevenZipExtractor.OpenAsync"/>.
    /// </summary>
    public record ArchiveInfo
    {
        /// <summary>
        /// Format of the archive.
        /// </summary>
        public required ArchiveFormat Format { get; init; }

        /// <summary>
        /// <see langword="true"/> if the archive uses solid compression (entries share a combined data stream).
        /// </summary>
        public required bool IsSolid { get; init; }

        /// <summary>
        /// <see langword="true"/> if the archive headers or contents are encrypted.
        /// </summary>
        public required bool IsEncrypted { get; init; }

        /// <summary>
        /// Total size of the archive on disk in bytes.
        /// </summary>
        public required ulong PhysicalSize { get; init; }

        /// <summary>
        /// Number of volumes the archive is split across.
        /// </summary>
        public required int VolumeCount { get; init; }

        /// <summary>
        /// Optional comment embedded in the archive, or <see langword="null"/> if none is present.
        /// </summary>
        public string? Comment { get; init; }
    }
}
