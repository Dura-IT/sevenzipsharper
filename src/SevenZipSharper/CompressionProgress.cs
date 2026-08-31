namespace SevenZipSharper
{
    /// <summary>
    /// Progress snapshot reported to <see cref="System.IProgress{T}"/> during a compression operation.
    /// </summary>
    public readonly record struct CompressionProgress
    {
        /// <summary>
        /// Archive-relative path of the entry currently being compressed.
        /// </summary>
        public required string EntryPath { get; init; }

        /// <summary>
        /// Number of uncompressed bytes processed so far.
        /// </summary>
        public required ulong BytesProcessed { get; init; }

        /// <summary>
        /// Total uncompressed bytes to process across all entries.
        /// </summary>
        public required ulong TotalBytes { get; init; }
    }
}
