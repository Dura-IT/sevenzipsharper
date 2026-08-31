namespace SevenZipSharper.Compression
{
    /// <summary>
    /// Compression algorithm applied to entries when creating an archive.
    /// </summary>
    /// <remarks>
    /// Not every method is compatible with every archive format. LZMA2 requires the 7z format
    /// (it has no ZIP method code and falls back to Deflate when used with ZIP). LZMA, BZip2,
    /// PPMd, and Deflate are available in both 7z and ZIP, though archives using LZMA, BZip2,
    /// or PPMd compression inside a ZIP may not be readable by tools that only support standard
    /// ZIP (Store + Deflate). Copy (store) is always available regardless of format.
    /// </remarks>
    /// <seealso href="https://www.7-zip.org/7z.html">7-Zip command-line reference</seealso>
    public enum CompressionMethod
    {
        /// <summary>
        /// Original LZMA algorithm. Produces excellent compression ratios. Superseded by
        /// <see cref="Lzma2"/> for multi-threaded workloads; retained for compatibility.
        /// </summary>
        Lzma,

        /// <summary>
        /// LZMA2 algorithm. Default method for the 7z format since 7-Zip version 9.x.
        /// Supports multi-threaded compression and solid blocks.
        /// </summary>
        Lzma2,

        /// <summary>
        /// BZip2 algorithm. Available in 7z and ZIP formats. Slower than LZMA2 for similar
        /// compression ratios, but broadly supported by other tools.
        /// </summary>
        BZip2,

        /// <summary>
        /// Deflate algorithm. Used in ZIP archives. Fast with moderate compression ratios;
        /// universally supported across all ZIP-compatible tools.
        /// </summary>
        Deflate,

        /// <summary>
        /// PPMd algorithm. Exceptionally strong compression for plain-text content.
        /// Available in 7z and ZIP formats.
        /// </summary>
        Ppmd,

        /// <summary>
        /// No compression — entries are stored as-is. Useful when the input data is already
        /// compressed (e.g. JPEG, MP4) or when archive overhead must be minimised.
        /// </summary>
        Copy,
    }
}
