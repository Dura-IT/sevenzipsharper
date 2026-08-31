namespace SevenZipSharper
{
    /// <summary>
    /// Options controlling how extracted entries are written to disk.
    /// </summary>
    public sealed record ExtractionOptions
    {
        /// <summary>
        /// Default number of bytes written between forced flushes to physical disk (64 MiB).
        /// </summary>
        public const long DefaultFlushIntervalBytes = 64L * 1024 * 1024;

        /// <summary>
        /// Number of bytes written to an output file between forced flushes to physical disk
        /// (<c>fsync</c> / <c>FlushFileBuffers</c>). Bounds how far decompression can run ahead of
        /// disk write-back, preventing unbounded OS page-cache growth during sustained large-entry
        /// extraction. Set to <c>0</c> or a negative value to disable periodic flushing and rely
        /// solely on OS write-back caching.
        /// </summary>
        /// <remarks>
        /// Applies only to extraction paths that write files to disk. Extraction to a
        /// caller-provided stream (<see cref="SevenZipExtractor.ExtractEntryAsync"/>) is unaffected.
        /// </remarks>
        public long FlushIntervalBytes { get; init; } = DefaultFlushIntervalBytes;
    }
}
