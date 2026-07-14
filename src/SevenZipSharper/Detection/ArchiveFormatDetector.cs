using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SevenZipSharper.Detection;

/// <summary>
/// Detects archive formats from a file extension or by sniffing magic bytes at the start of a stream.
/// </summary>
/// <remarks>
/// Both detection methods return <see langword="null"/> when no known format matches; they do not throw
/// for unrecognised input. <see cref="FromStreamAsync"/> reads up to 262 bytes from the stream and
/// restores the original position when the stream is seekable.
/// ISO detection requires reading 32 KB (CD001 signature at offset 32769) and is not attempted here;
/// use <see cref="FromExtension"/> for ISO.
/// </remarks>
/// <example>
/// <code>
/// await using var fs = File.OpenRead("archive.bin");
/// var format = await ArchiveFormatDetector.FromStreamAsync(fs)
///              ?? ArchiveFormatDetector.FromExtension("archive.bin");
/// if (format is null) throw new InvalidOperationException("Unknown archive format.");
/// using var extractor = new SevenZipExtractor(fs, format.Value, logger);
/// </code>
/// </example>
public static class ArchiveFormatDetector
{
    private static readonly (ArchiveFormat Format, int Offset, byte[] Bytes)[] Signatures =
    [
        (ArchiveFormat.Wim, 0, [0x4D, 0x53, 0x57, 0x49, 0x4D, 0x00, 0x00, 0x00]),
        (ArchiveFormat.SevenZip, 0, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]),
        (ArchiveFormat.Xz, 0, [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00]),
        (ArchiveFormat.Cab, 0, [0x4D, 0x53, 0x43, 0x46]),
        (ArchiveFormat.BZip2, 0, [0x42, 0x5A, 0x68]),
        // Lzh: '-lh' at offset 2 (e.g. -lh5-, -lhd-)
        (ArchiveFormat.Lzh, 2, [0x2D, 0x6C, 0x68]),
        (ArchiveFormat.Zip, 0, [0x50, 0x4B]),
        (ArchiveFormat.GZip, 0, [0x1F, 0x8B]),
        (ArchiveFormat.Arj, 0, [0x60, 0xEA]),
        // POSIX/GNU TAR: 'ustar' at offset 257
        (ArchiveFormat.Tar, 257, [0x75, 0x73, 0x74, 0x61, 0x72]),
    ];

    // Covers all signatures above: TAR offset 257 + 5 bytes = 262.
    private const int BufferSize = 262;

    private static readonly Dictionary<string, ArchiveFormat> ExtensionMap = new Dictionary<
        string,
        ArchiveFormat
    >(StringComparer.OrdinalIgnoreCase)
    {
        [".7z"] = ArchiveFormat.SevenZip,
        [".zip"] = ArchiveFormat.Zip,
        [".jar"] = ArchiveFormat.Zip,
        [".epub"] = ArchiveFormat.Zip,
        [".apk"] = ArchiveFormat.Zip,
        [".gz"] = ArchiveFormat.GZip,
        [".tgz"] = ArchiveFormat.GZip,
        [".bz2"] = ArchiveFormat.BZip2,
        [".tbz"] = ArchiveFormat.BZip2,
        [".tbz2"] = ArchiveFormat.BZip2,
        [".tar"] = ArchiveFormat.Tar,
        [".iso"] = ArchiveFormat.Iso,
        [".cab"] = ArchiveFormat.Cab,
        [".arj"] = ArchiveFormat.Arj,
        [".lzh"] = ArchiveFormat.Lzh,
        [".lha"] = ArchiveFormat.Lzh,
        [".xz"] = ArchiveFormat.Xz,
        [".txz"] = ArchiveFormat.Xz,
        [".wim"] = ArchiveFormat.Wim,
        [".swm"] = ArchiveFormat.Wim,
        [".esd"] = ArchiveFormat.Wim,
    };

    /// <summary>
    /// Resolves an archive format from the extension of <paramref name="path"/>.
    /// </summary>
    /// <param name="path">File name or full path. Only the extension is consulted; the file does not need to exist.</param>
    /// <returns>The matching format, or <see langword="null"/> if the extension is missing or unknown.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="path"/> is <see langword="null"/>.</exception>
    public static ArchiveFormat? FromExtension(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
            return null;

        return ExtensionMap.TryGetValue(ext, out var format) ? format : null;
    }

    /// <summary>
    /// Sniffs the first 262 bytes of <paramref name="stream"/> for a known archive signature.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the candidate archive. The original position is restored on seekable streams.</param>
    /// <param name="cancellationToken">Token to cancel the read.</param>
    /// <returns>The matching format, or <see langword="null"/> if no signature matched (including empty streams).</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is triggered before the read completes.</exception>
    public static async Task<ArchiveFormat?> FromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        var originalPosition = stream.CanSeek ? stream.Position : -1L;

        var buffer = new byte[BufferSize];
        var bytesRead = await ReadFullyAsync(stream, buffer, cancellationToken)
            .ConfigureAwait(false);

        if (stream.CanSeek)
            stream.Position = originalPosition;

        if (bytesRead == 0)
            return null;

        return MatchSignature(buffer, bytesRead);
    }

    private static ArchiveFormat? MatchSignature(byte[] buffer, int length)
    {
        var span = buffer.AsSpan(0, length);

        foreach (var (format, offset, bytes) in Signatures)
        {
            if (
                span.Length >= offset + bytes.Length
                && span.Slice(offset, bytes.Length).SequenceEqual(bytes)
            )
            {
                return format;
            }
        }

        return null;
    }

    private static async Task<int> ReadFullyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken
    )
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream
                .ReadAsync(buffer.AsMemory(totalRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
                break;
            totalRead += read;
        }
        return totalRead;
    }
}
