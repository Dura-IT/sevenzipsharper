using FluentResults;

namespace SevenZipSharper.Compression
{
    /// <summary>
    /// Describes how entries should be compressed when creating or updating an archive.
    /// Pass to <c>SevenZipCompressor</c> at construction time.
    /// </summary>
    /// <remarks>
    /// Use the static predefined instances (<see cref="Default"/>, <see cref="MaximumLzma2"/>,
    /// <see cref="Store"/>) as a starting point and use <c>with</c> expressions to override
    /// individual properties. Call <see cref="Validate"/> before passing to the compressor to
    /// surface configuration errors early.
    /// </remarks>
    /// <seealso href="https://www.7-zip.org/7z.html">7-Zip command-line reference</seealso>
    public record CompressionParameters
    {
        /// <summary>
        /// LZMA2 at <see cref="CompressionLevel.Normal"/> with all optional parameters set
        /// to automatic. Suitable for most workloads.
        /// </summary>
        public static readonly CompressionParameters Default = new CompressionParameters();

        /// <summary>
        /// LZMA2 at <see cref="CompressionLevel.Ultra"/> with a 128 MB dictionary. Produces
        /// the smallest possible 7z archives; requires significant CPU time and memory.
        /// </summary>
        public static readonly CompressionParameters MaximumLzma2 = new CompressionParameters
        {
            Method = CompressionMethod.Lzma2,
            Level = CompressionLevel.Ultra,
            DictionarySize = 128 * 1024 * 1024,
        };

        /// <summary>
        /// No compression — entries are stored verbatim. Equivalent to <c>-mx0</c> on the
        /// command line. Useful when the content is already compressed.
        /// </summary>
        public static readonly CompressionParameters Store = new CompressionParameters
        {
            Method = CompressionMethod.Copy,
            Level = CompressionLevel.Store,
            SolidMode = false,
        };

        /// <summary>
        /// Compression algorithm. Defaults to <see cref="CompressionMethod.Lzma2"/>.
        /// </summary>
        public CompressionMethod Method { get; init; } = CompressionMethod.Lzma2;

        /// <summary>
        /// Compression effort. Defaults to <see cref="CompressionLevel.Normal"/>.
        /// Higher levels produce smaller archives at the cost of more CPU time and memory.
        /// </summary>
        public CompressionLevel Level { get; init; } = CompressionLevel.Normal;

        /// <summary>
        /// Dictionary size in bytes, or <see langword="null"/> to let 7-Zip choose
        /// automatically based on <see cref="Level"/>.
        /// </summary>
        /// <remarks>
        /// For LZMA and LZMA2 the value must be a power of 2 and between 1 KB and 1536 MB.
        /// For BZip2 the value must be a multiple of 100 KB between 100 KB and 900 KB.
        /// Larger dictionaries improve the compression ratio but increase memory usage
        /// proportionally during both compression and decompression.
        /// </remarks>
        /// <seealso href="https://www.7-zip.org/7z.html">7-Zip command-line reference (-md switch)</seealso>
        public uint? DictionarySize { get; init; }

        /// <summary>
        /// Number of fast bytes (word size) for the match finder, or <see langword="null"/>
        /// for automatic. Valid range for LZMA and LZMA2 is 5–273. Ignored for other methods.
        /// </summary>
        public uint? WordSize { get; init; }

        /// <summary>
        /// Number of compression threads, or <see langword="null"/> to use all available
        /// logical processors. Must be ≥ 1 if specified. Only LZMA2 benefits from multiple
        /// threads; other methods are single-threaded.
        /// </summary>
        public int? ThreadCount { get; init; }

        /// <summary>
        /// Pack all entries into a single compressed block (solid archive). Improves
        /// compression ratio when many small, similar files are present. Defaults to
        /// <see langword="true"/>. Only meaningful for the 7z format.
        /// </summary>
        public bool SolidMode { get; init; } = true;

        /// <summary>
        /// Password used to encrypt archive entries with AES-256, or <see langword="null"/>
        /// for no encryption.
        /// </summary>
        /// <remarks>
        /// The password is held in managed memory as a plain <see cref="string"/> and will
        /// be passed as a wide-character string to the native 7-Zip library. Callers with
        /// strict security requirements should minimise the lifetime of the
        /// <see cref="CompressionParameters"/> instance.
        /// </remarks>
        public string? EncryptionPassword { get; init; }

        /// <summary>
        /// When <see langword="true"/>, the archive header (file names, sizes, timestamps)
        /// is also encrypted. Requires <see cref="EncryptionPassword"/> to be set.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool EncryptHeaders { get; init; }

        /// <summary>
        /// Validates that all property values form a consistent, supported configuration.
        /// </summary>
        /// <returns>
        /// <see cref="Result.Ok()"/> if the parameters are valid; a failed result with a
        /// descriptive message otherwise.
        /// </returns>
        public Result Validate()
        {
            if (ThreadCount < 1)
                return Result.Fail("ThreadCount must be 1 or greater.");

            if (EncryptionPassword?.Length == 0)
                return Result.Fail("EncryptionPassword must not be empty. Use null for no encryption.");

            if (EncryptHeaders && EncryptionPassword is null)
                return Result.Fail("EncryptHeaders requires EncryptionPassword to be set.");

            if (DictionarySize.HasValue)
            {
                var dictResult = ValidateDictionarySize(Method, DictionarySize.Value);
                if (dictResult.IsFailed)
                    return dictResult;
            }

            if (WordSize.HasValue && Method is CompressionMethod.Lzma or CompressionMethod.Lzma2)
            {
                var wordResult = ValidateWordSize(WordSize.Value);
                if (wordResult.IsFailed)
                    return wordResult;
            }

            return Result.Ok();
        }

        private static Result ValidateDictionarySize(CompressionMethod method, uint dict)
        {
            if (method is CompressionMethod.Lzma or CompressionMethod.Lzma2)
            {
                const uint minLzma = 1024;
                const uint maxLzma = 1536 * 1024 * 1024;

                if (!IsPowerOfTwo(dict))
                {
                    return Result.Fail($"DictionarySize {dict} is not a power of 2. LZMA and LZMA2 require a power-of-2 dictionary size.");
                }

                if (dict < minLzma || dict > maxLzma)
                {
                    return Result.Fail($"DictionarySize {dict} is out of range for LZMA/LZMA2. Valid range: 1 KB – 1536 MB.");
                }
            }

            if (method is CompressionMethod.BZip2)
            {
                const uint minBzip2 = 100 * 1024;
                const uint maxBzip2 = 900 * 1024;
                const uint stepBzip2 = 100 * 1024;

                if (dict < minBzip2 || dict > maxBzip2 || dict % stepBzip2 != 0)
                {
                    return Result.Fail($"DictionarySize {dict} is invalid for BZip2. Valid values: multiples of 100 KB between 100 KB and 900 KB.");
                }
            }

            return Result.Ok();
        }

        private static Result ValidateWordSize(uint wordSize)
        {
            const uint minWordSize = 5;
            const uint maxWordSize = 273;

            if (wordSize < minWordSize || wordSize > maxWordSize)
            {
                return Result.Fail($"WordSize {wordSize} is out of range for LZMA/LZMA2. Valid range: 5–273.");
            }

            return Result.Ok();
        }

        private static bool IsPowerOfTwo(uint value) => value > 0 && (value & (value - 1)) == 0;
    }
}
