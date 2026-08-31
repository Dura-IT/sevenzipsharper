using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using SevenZipSharper.Compression;

namespace SevenZipSharper.Benchmarks
{
    /// <summary>
    /// Compares archive extraction performance against SharpSevenZip across formats and TFMs.
    /// Requires native 7-Zip libraries in runtimes/&lt;RID&gt;/native/ to run.
    /// </summary>
    /// <remarks>
    /// Both extractors are created and opened in <see cref="IterationSetupAttribute"/> so the timed
    /// window covers only decompression: <see cref="SevenZipExtractor.ExtractEntryAsync"/> for
    /// SevenZipSharper and <see cref="SharpSevenZip.SharpSevenZipExtractor.ExtractFile(int,System.IO.Stream)"/>
    /// for SharpSevenZip. Both call the same underlying native <c>IInArchive::Extract</c> on a
    /// pre-opened archive.
    /// </remarks>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    public class ExtractionBenchmarks
    {
        [Params(ArchiveFormat.SevenZip, ArchiveFormat.Zip)]
        public ArchiveFormat Format { get; set; }

        private byte[] _archive = null!;
        private MemoryStream _outputBuffer = null!;
        private MemoryStream _oursStream = null!;
        private MemoryStream _sharpStream = null!;
        private SevenZipExtractor _oursExtractor = null!;
        private IReadOnlyList<ArchiveEntry> _oursEntries = null!;
        private SharpSevenZip.SharpSevenZipExtractor _sharpExtractor = null!;

        [GlobalSetup]
        public async Task GlobalSetupAsync()
        {
            var payload = GenerateCompressiblePayload(1024 * 1024);

            using var compressor = new SevenZipCompressor(Format, CompressionParameters.Default, NullLogger<SevenZipCompressor>.Instance);

            var ms = new MemoryStream();
            await compressor.CompressAsync(new[] { ("payload.bin", (Stream)new MemoryStream(payload)) }, ms);
            _archive = ms.ToArray();
            _outputBuffer = new MemoryStream(capacity: 2 * 1024 * 1024);

            SharpSevenZip.SharpSevenZipExtractor.SetLibraryPath(NativeLibPath);
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _outputBuffer.SetLength(0);

            _oursStream = new MemoryStream(_archive, writable: false);
            _oursExtractor = new SevenZipExtractor(_oursStream, Format, NullLogger<SevenZipExtractor>.Instance);
            _oursExtractor.OpenAsync().GetAwaiter().GetResult();
            _oursEntries = _oursExtractor.ListEntriesAsync().GetAwaiter().GetResult().Value;

            _sharpStream = new MemoryStream(_archive, writable: false);
            _sharpExtractor = new SharpSevenZip.SharpSevenZipExtractor(_sharpStream);
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            _oursExtractor.Dispose();
            _oursStream.Dispose();
            _sharpExtractor.Dispose();
            _sharpStream.Dispose();
        }

        [GlobalCleanup]
        public void GlobalCleanup() => _outputBuffer.Dispose();

        [Benchmark(Description = "SevenZipSharper")]
        public Task ExtractWithSevenZipSharper() => _oursExtractor.ExtractEntryAsync(_oursEntries[0], _outputBuffer);

        [Benchmark(Description = "SharpSevenZip", Baseline = true)]
        public void ExtractWithSharpSevenZip() => _sharpExtractor.ExtractFile(0, _outputBuffer);

        internal static string NativeLibPath => Path.Combine(AppContext.BaseDirectory, "runtimes", BaseRuntimeIdentifier(), "native", NativeLibName());

        private static string BaseRuntimeIdentifier()
        {
            var arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => throw new PlatformNotSupportedException($"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}"),
            };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return $"win-{arch}";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return $"osx-{arch}";
            return $"linux-{arch}";
        }

        internal static byte[] GenerateCompressiblePayload(int size)
        {
            // Repeating ASCII text — high compressibility, realistic for document archives.
            const string pattern =
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. " + "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. ";
            var patternBytes = System.Text.Encoding.ASCII.GetBytes(pattern);
            var data = new byte[size];
            for (var i = 0; i < data.Length; i++)
                data[i] = patternBytes[i % patternBytes.Length];
            return data;
        }

        private static string NativeLibName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "7z.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "7z.dylib";
            return "7z.so";
        }
    }
}
