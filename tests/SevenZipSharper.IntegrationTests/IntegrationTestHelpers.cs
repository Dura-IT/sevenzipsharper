using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests
{
    internal static class IntegrationTestHelpers
    {
        internal static string UniqueTempDir(string prefix) => Path.Combine(Path.GetTempPath(), $"szs_{prefix}_{Guid.NewGuid():N}");

        internal static async Task<byte[]> BuildArchiveAsync(
            ArchiveFormat format,
            CompressionParameters parameters,
            params (string Path, byte[] Content)[] entries
        )
        {
            var streamEntries = new (string, Stream)[entries.Length];
            for (var i = 0; i < entries.Length; i++)
                streamEntries[i] = (entries[i].Path, new MemoryStream(entries[i].Content));

            using var output = new MemoryStream();
            using var compressor = new SevenZipCompressor(format, parameters, NullLogger<SevenZipCompressor>.Instance);
            var result = await compressor.CompressAsync(streamEntries, output);
            if (result.IsFailed)
            {
                throw new InvalidOperationException($"Test fixture archive creation failed: {string.Join("; ", result.Errors)}");
            }

            return output.ToArray();
        }
    }
}
