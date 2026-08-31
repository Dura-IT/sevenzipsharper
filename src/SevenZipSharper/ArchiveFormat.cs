namespace SevenZipSharper
{
    /// <summary>
    /// Archive formats supported for reading and writing.
    /// </summary>
    /// <remarks>
    /// RAR is intentionally excluded. The unRAR source carries a redistribution restriction
    /// that is incompatible with this library's LGPL licence. Use a dedicated unRAR library
    /// if you need RAR support.
    /// </remarks>
    public enum ArchiveFormat
    {
        /// <summary>
        /// 7-Zip (.7z) format.
        /// </summary>
        SevenZip,

        /// <summary>
        /// ZIP (.zip) format.
        /// </summary>
        Zip,

        /// <summary>
        /// gzip (.gz) format.
        /// </summary>
        GZip,

        /// <summary>
        /// bzip2 (.bz2) format.
        /// </summary>
        BZip2,

        /// <summary>
        /// POSIX tar (.tar) format.
        /// </summary>
        Tar,

        /// <summary>
        /// ISO 9660 optical disc image (.iso) format.
        /// </summary>
        Iso,

        /// <summary>
        /// Microsoft Cabinet (.cab) format.
        /// </summary>
        Cab,

        /// <summary>
        /// ARJ (.arj) format.
        /// </summary>
        Arj,

        /// <summary>
        /// LZH/LHA (.lzh, .lha) format.
        /// </summary>
        Lzh,

        /// <summary>
        /// XZ (.xz) format.
        /// </summary>
        Xz,

        /// <summary>
        /// Windows Imaging Format (.wim) format.
        /// </summary>
        Wim,
    }
}
