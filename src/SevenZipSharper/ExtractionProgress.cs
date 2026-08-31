namespace SevenZipSharper
{
    /// <summary>
    /// Progress snapshot reported to <see cref="System.IProgress{T}"/> during an extraction operation.
    /// </summary>
    public readonly record struct ExtractionProgress
    {
        /// <summary>
        /// Archive-relative path of the entry currently being extracted.
        /// </summary>
        public required string EntryPath { get; init; }

        /// <summary>
        /// Zero-based index of the entry currently being extracted.
        /// </summary>
        public required int EntryIndex { get; init; }

        /// <summary>
        /// Total number of entries being extracted in this operation.
        /// </summary>
        public required int TotalEntries { get; init; }

        /// <summary>
        /// Number of compressed bytes processed so far.
        /// </summary>
        public required ulong BytesProcessed { get; init; }

        /// <summary>
        /// Total compressed bytes to process, as reported by the archive.
        /// </summary>
        public required ulong TotalBytes { get; init; }
    }
}
