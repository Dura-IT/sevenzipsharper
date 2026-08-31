namespace SevenZipSharper.Compression
{
    /// <summary>
    /// Compression effort applied when creating an archive. Higher levels produce smaller
    /// archives at the cost of more CPU time and memory.
    /// </summary>
    /// <remarks>
    /// The integer values correspond to 7-Zip's <c>-mx</c> switch. Only the discrete values
    /// 0, 1, 3, 5, 7, and 9 are defined — intermediate values are not exposed because they
    /// are not meaningful presets in either 7-Zip or the ZIP format. Levels above
    /// <see cref="Normal"/> also increase the dictionary size and number of fast bytes
    /// automatically, which can significantly raise memory usage.
    /// </remarks>
    /// <seealso href="https://www.7-zip.org/7z.html">7-Zip command-line reference</seealso>
    public enum CompressionLevel
    {
        /// <summary>
        /// No compression (level 0). Equivalent to using <see cref="CompressionMethod.Copy"/>.
        /// Archive size will be larger than the input.
        /// </summary>
        Store = 0,

        /// <summary>
        /// Fastest compression (level 1). Minimal CPU usage; compression ratio is poor.
        /// Suitable when speed is critical and archive size is unimportant.
        /// </summary>
        Fastest = 1,

        /// <summary>
        /// Fast compression (level 3). Good balance of speed with a reasonable ratio.
        /// </summary>
        Fast = 3,

        /// <summary>
        /// Normal compression (level 5). The default. Good ratio with moderate CPU and
        /// memory usage. Suitable for most workloads.
        /// </summary>
        Normal = 5,

        /// <summary>
        /// Maximum compression (level 7). Noticeably slower than <see cref="Normal"/>;
        /// 7-Zip increases the dictionary size and fast-byte count automatically.
        /// </summary>
        Maximum = 7,

        /// <summary>
        /// Ultra compression (level 9). The highest available ratio. Can require several
        /// gigabytes of RAM depending on the dictionary size. Not recommended for
        /// memory-constrained environments.
        /// </summary>
        Ultra = 9,
    }
}
