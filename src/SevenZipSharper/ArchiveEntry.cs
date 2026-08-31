using System;

namespace SevenZipSharper
{
    /// <summary>
    /// Metadata for a single entry within an archive.
    /// </summary>
    public record ArchiveEntry
    {
        /// <summary>
        /// Zero-based index of the entry within the archive.
        /// </summary>
        public required int Index { get; init; }

        /// <summary>
        /// Entry path relative to the archive root, using forward-slash separators.
        /// </summary>
        public required string Path { get; init; }

        /// <summary>
        /// Uncompressed size of the entry in bytes.
        /// </summary>
        public required ulong Size { get; init; }

        /// <summary>
        /// Compressed size of the entry in bytes as stored in the archive.
        /// </summary>
        public required ulong PackedSize { get; init; }

        /// <summary>
        /// CRC-32 checksum of the uncompressed data.
        /// </summary>
        public required uint Crc { get; init; }

        /// <summary>
        /// <see langword="true"/> if the entry represents a directory rather than a file.
        /// </summary>
        public required bool IsDirectory { get; init; }

        /// <summary>
        /// <see langword="true"/> if the entry's data is encrypted.
        /// </summary>
        public required bool IsEncrypted { get; init; }

        /// <summary>
        /// Last write time of the entry in UTC, or <see langword="null"/> if not stored.
        /// </summary>
        public DateTime? LastWriteTime { get; init; }

        /// <summary>
        /// Creation time of the entry in UTC, or <see langword="null"/> if not stored.
        /// </summary>
        public DateTime? CreationTime { get; init; }

        /// <summary>
        /// Last access time of the entry in UTC, or <see langword="null"/> if not stored.
        /// </summary>
        public DateTime? LastAccessTime { get; init; }

        /// <summary>
        /// Platform-specific file attributes, or <see langword="null"/> if the format does not store them.
        /// </summary>
        public uint? Attributes { get; init; }
    }
}
