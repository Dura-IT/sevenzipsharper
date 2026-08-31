using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.IntegrationTests
{
    /// <summary>
    /// Exercises <see cref="CompressionParameters.Validate"/> directly and via the actual compression
    /// flow, ensuring invalid configurations surface as <see cref="FluentResults.Result"/> failures
    /// (not silent fallbacks or runtime crashes).
    /// </summary>
    [TestFixture]
    [TestOf(typeof(CompressionParameters))]
    public sealed class ParameterValidationTests
    {
        private static readonly byte[] Payload = System.Text.Encoding.UTF8.GetBytes("validation");

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(-100)]
        public void Validate_ThreadCountBelowOne_Fails(int threadCount)
        {
            var parameters = new CompressionParameters { ThreadCount = threadCount };

            var result = parameters.Validate();

            result.IsFailed.Should().BeTrue();
            result.Errors[0].Message.Should().Contain("ThreadCount");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(64)]
        public void Validate_ThreadCountAtLeastOne_Succeeds(int threadCount)
        {
            new CompressionParameters { ThreadCount = threadCount }
                .Validate()
                .IsSuccess.Should()
                .BeTrue();
        }

        [Test]
        public void Validate_EncryptHeadersWithoutPassword_Fails()
        {
            var parameters = new CompressionParameters { EncryptHeaders = true };

            var result = parameters.Validate();

            result.IsFailed.Should().BeTrue();
            result.Errors[0].Message.Should().Contain("EncryptionPassword");
        }

        [Test]
        public void Validate_EncryptHeadersWithPassword_Succeeds()
        {
            var parameters = new CompressionParameters { EncryptHeaders = true, EncryptionPassword = "secret" };

            parameters.Validate().IsSuccess.Should().BeTrue();
        }

        [TestCase(1500u)] // Not a power of 2
        [TestCase(2049u)] // Not a power of 2
        [TestCase(0u)] // Below 1 KB
        [TestCase(1024u * 1024u * 1024u * 2u)] // Above 1536 MB
        public void Validate_LzmaInvalidDictionarySize_Fails(uint size)
        {
            var parameters = new CompressionParameters { Method = CompressionMethod.Lzma2, DictionarySize = size };

            parameters.Validate().IsFailed.Should().BeTrue();
        }

        [TestCase(1024u)] // 1 KB — minimum
        [TestCase(65536u)] // 64 KB
        [TestCase(16u * 1024u * 1024u)] // 16 MB — typical default
        public void Validate_LzmaValidDictionarySize_Succeeds(uint size)
        {
            var parameters = new CompressionParameters { Method = CompressionMethod.Lzma2, DictionarySize = size };

            parameters.Validate().IsSuccess.Should().BeTrue();
        }

        [TestCase(50u * 1024u)] // Below 100 KB minimum
        [TestCase(1000u * 1024u)] // Above 900 KB maximum
        [TestCase(150u * 1024u)] // Not a multiple of 100 KB
        public void Validate_BZip2InvalidDictionarySize_Fails(uint size)
        {
            var parameters = new CompressionParameters { Method = CompressionMethod.BZip2, DictionarySize = size };

            parameters.Validate().IsFailed.Should().BeTrue();
        }

        [TestCase(100u * 1024u)] // Minimum
        [TestCase(500u * 1024u)] // Mid-range
        [TestCase(900u * 1024u)] // Maximum
        public void Validate_BZip2ValidDictionarySize_Succeeds(uint size)
        {
            var parameters = new CompressionParameters { Method = CompressionMethod.BZip2, DictionarySize = size };

            parameters.Validate().IsSuccess.Should().BeTrue();
        }

        [TestCase(4u)] // Below 5
        [TestCase(274u)] // Above 273
        [TestCase(1000u)] // Far above 273
        public void Validate_LzmaInvalidWordSize_Fails(uint wordSize)
        {
            var parameters = new CompressionParameters { Method = CompressionMethod.Lzma, WordSize = wordSize };

            parameters.Validate().IsFailed.Should().BeTrue();
        }

        [TestCase(5u)] // Minimum
        [TestCase(32u)] // Typical default
        [TestCase(273u)] // Maximum
        public void Validate_LzmaValidWordSize_Succeeds(uint wordSize)
        {
            var parameters = new CompressionParameters { Method = CompressionMethod.Lzma, WordSize = wordSize };

            parameters.Validate().IsSuccess.Should().BeTrue();
        }

        /// <summary>
        /// WordSize is only validated for LZMA/LZMA2; for other methods the value is ignored
        /// (passed through to 7-Zip which silently drops it).
        /// </summary>
        [TestCase(CompressionMethod.BZip2)]
        [TestCase(CompressionMethod.Deflate)]
        [TestCase(CompressionMethod.Ppmd)]
        [TestCase(CompressionMethod.Copy)]
        public void Validate_OutOfRangeWordSizeOnNonLzmaMethod_Succeeds(CompressionMethod method)
        {
            var parameters = new CompressionParameters { Method = method, WordSize = 9999 };

            parameters.Validate().IsSuccess.Should().BeTrue();
        }

        [Test]
        public async Task CompressAsync_InvalidThreadCount_FailsBeforeNativeCall()
        {
            var parameters = new CompressionParameters { Method = CompressionMethod.Lzma2, ThreadCount = 0 };

            using var output = new MemoryStream();
            using var compressor = new SevenZipCompressor(ArchiveFormat.SevenZip, parameters, NullLogger<SevenZipCompressor>.Instance);
            var entries = new[] { ("a.bin", (Stream)new MemoryStream(Payload)) };

            var result = await compressor.CompressAsync(entries, output);

            result.IsFailed.Should().BeTrue("invalid parameters should be caught before reaching the native layer");
            result.Errors[0].Message.Should().Contain("ThreadCount");
        }

        [Test]
        public async Task CompressAsync_InvalidDictionarySize_FailsBeforeNativeCall()
        {
            var parameters = new CompressionParameters
            {
                Method = CompressionMethod.Lzma2,
                DictionarySize = 1500, // Not a power of 2
            };

            using var output = new MemoryStream();
            using var compressor = new SevenZipCompressor(ArchiveFormat.SevenZip, parameters, NullLogger<SevenZipCompressor>.Instance);
            var entries = new[] { ("a.bin", (Stream)new MemoryStream(Payload)) };

            var result = await compressor.CompressAsync(entries, output);

            result.IsFailed.Should().BeTrue();
            result.Errors[0].Message.Should().Contain("DictionarySize");
        }
    }
}
