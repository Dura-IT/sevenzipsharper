using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Detection;

namespace SevenZipSharper.UnitTests.Detection
{
    [TestOf(typeof(ArchiveFormatDetector))]
    public sealed class ArchiveFormatDetectorTests
    {
        #region FromExtension

        private static IEnumerable<TestCaseData> ExtensionCases()
        {
            yield return new TestCaseData("archive.7z", ArchiveFormat.SevenZip);
            yield return new TestCaseData("archive.zip", ArchiveFormat.Zip);
            yield return new TestCaseData("archive.jar", ArchiveFormat.Zip);
            yield return new TestCaseData("archive.epub", ArchiveFormat.Zip);
            yield return new TestCaseData("archive.apk", ArchiveFormat.Zip);
            yield return new TestCaseData("archive.gz", ArchiveFormat.GZip);
            yield return new TestCaseData("archive.tgz", ArchiveFormat.GZip);
            yield return new TestCaseData("archive.bz2", ArchiveFormat.BZip2);
            yield return new TestCaseData("archive.tbz", ArchiveFormat.BZip2);
            yield return new TestCaseData("archive.tbz2", ArchiveFormat.BZip2);
            yield return new TestCaseData("archive.tar", ArchiveFormat.Tar);
            yield return new TestCaseData("archive.iso", ArchiveFormat.Iso);
            yield return new TestCaseData("archive.cab", ArchiveFormat.Cab);
            yield return new TestCaseData("archive.arj", ArchiveFormat.Arj);
            yield return new TestCaseData("archive.lzh", ArchiveFormat.Lzh);
            yield return new TestCaseData("archive.lha", ArchiveFormat.Lzh);
            yield return new TestCaseData("archive.xz", ArchiveFormat.Xz);
            yield return new TestCaseData("archive.txz", ArchiveFormat.Xz);
            yield return new TestCaseData("archive.wim", ArchiveFormat.Wim);
            yield return new TestCaseData("archive.swm", ArchiveFormat.Wim);
            yield return new TestCaseData("archive.esd", ArchiveFormat.Wim);
        }

        [TestCaseSource(nameof(ExtensionCases))]
        public void FromExtension_ReturnsFormat_ForKnownExtension(string path, ArchiveFormat expected)
        {
            var result = ArchiveFormatDetector.FromExtension(path);

            result.Should().Be(expected);
        }

        [Test]
        public void FromExtension_IsCaseInsensitive()
        {
            ArchiveFormatDetector.FromExtension("archive.ZIP").Should().Be(ArchiveFormat.Zip);
            ArchiveFormatDetector.FromExtension("archive.7Z").Should().Be(ArchiveFormat.SevenZip);
        }

        [Test]
        public void FromExtension_ReturnsNull_ForUnknownExtension()
        {
            ArchiveFormatDetector.FromExtension("archive.txt").Should().BeNull();
        }

        [Test]
        public void FromExtension_ReturnsNull_WhenNoExtension()
        {
            ArchiveFormatDetector.FromExtension("archive").Should().BeNull();
        }

        [Test]
        public void FromExtension_UsesLastExtensionOnly()
        {
            // .tar.gz → detects GZip (the outermost format), not Tar
            ArchiveFormatDetector.FromExtension("archive.tar.gz").Should().Be(ArchiveFormat.GZip);
        }

        [Test]
        public void FromExtension_WorksWithFullPath()
        {
            ArchiveFormatDetector.FromExtension("/home/user/downloads/backup.7z").Should().Be(ArchiveFormat.SevenZip);
        }

        #endregion

        #region FromStreamAsync

        private static IEnumerable<TestCaseData> MagicByteCases()
        {
            yield return new TestCaseData(new byte[] { 0x4D, 0x53, 0x57, 0x49, 0x4D, 0x00, 0x00, 0x00 }, ArchiveFormat.Wim).SetName("Wim");
            yield return new TestCaseData(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, ArchiveFormat.SevenZip).SetName("SevenZip");
            yield return new TestCaseData(new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }, ArchiveFormat.Xz).SetName("Xz");
            yield return new TestCaseData(new byte[] { 0x4D, 0x53, 0x43, 0x46, 0x00, 0x00 }, ArchiveFormat.Cab).SetName("Cab");
            yield return new TestCaseData(new byte[] { 0x42, 0x5A, 0x68, 0x39 }, ArchiveFormat.BZip2).SetName("BZip2");
            yield return new TestCaseData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, ArchiveFormat.Zip).SetName("Zip");
            yield return new TestCaseData(new byte[] { 0x1F, 0x8B, 0x08 }, ArchiveFormat.GZip).SetName("GZip");
            yield return new TestCaseData(new byte[] { 0x60, 0xEA, 0x00 }, ArchiveFormat.Arj).SetName("Arj");
        }

        [TestCaseSource(nameof(MagicByteCases))]
        public async Task FromStreamAsync_ReturnsFormat_ForMagicBytes(byte[] header, ArchiveFormat expected)
        {
            using var stream = new MemoryStream(header);

            var result = await ArchiveFormatDetector.FromStreamAsync(stream);

            result.Should().Be(expected);
        }

        [Test]
        public async Task FromStreamAsync_ReturnsLzh_ForLzhSignature()
        {
            // '-lh' signature at offset 2
            var bytes = new byte[8];
            bytes[2] = 0x2D; // '-'
            bytes[3] = 0x6C; // 'l'
            bytes[4] = 0x68; // 'h'

            using var stream = new MemoryStream(bytes);
            var result = await ArchiveFormatDetector.FromStreamAsync(stream);

            result.Should().Be(ArchiveFormat.Lzh);
        }

        [Test]
        public async Task FromStreamAsync_ReturnsTar_ForUstarSignature()
        {
            // 'ustar' at offset 257
            var bytes = new byte[262];
            bytes[257] = 0x75; // 'u'
            bytes[258] = 0x73; // 's'
            bytes[259] = 0x74; // 't'
            bytes[260] = 0x61; // 'a'
            bytes[261] = 0x72; // 'r'

            using var stream = new MemoryStream(bytes);
            var result = await ArchiveFormatDetector.FromStreamAsync(stream);

            result.Should().Be(ArchiveFormat.Tar);
        }

        [Test]
        public async Task FromStreamAsync_ReturnsNull_ForEmptyStream()
        {
            using var stream = new MemoryStream([]);

            var result = await ArchiveFormatDetector.FromStreamAsync(stream);

            result.Should().BeNull();
        }

        [Test]
        public async Task FromStreamAsync_ReturnsNull_ForUnrecognisedBytes()
        {
            using var stream = new MemoryStream([0x00, 0x01, 0x02, 0x03]);

            var result = await ArchiveFormatDetector.FromStreamAsync(stream);

            result.Should().BeNull();
        }

        [Test]
        public async Task FromStreamAsync_RestoresStreamPosition_AfterDetection()
        {
            var bytes = new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x00 };
            using var stream = new MemoryStream(bytes);

            await ArchiveFormatDetector.FromStreamAsync(stream);

            stream.Position.Should().Be(0);
        }

        [Test]
        public async Task FromStreamAsync_RestoresNonZeroStartPosition()
        {
            var bytes = new byte[] { 0xFF, 0xFF, 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
            using var stream = new MemoryStream(bytes);
            stream.Position = 2;

            await ArchiveFormatDetector.FromStreamAsync(stream);

            stream.Position.Should().Be(2);
        }

        #endregion

        #region Argument validation

        [Test]
        public void FromExtension_ThrowsArgumentNull_WhenPathIsNull()
        {
            var act = () => ArchiveFormatDetector.FromExtension(null!);

            act.Should().Throw<ArgumentNullException>().WithParameterName("path");
        }

        [Test]
        public async Task FromStreamAsync_ThrowsArgumentNull_WhenStreamIsNull()
        {
            var act = async () => await ArchiveFormatDetector.FromStreamAsync(null!);

            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("stream");
        }

        [Test]
        public async Task FromStreamAsync_ThrowsArgumentException_WhenStreamIsNotReadable()
        {
            await using var stream = new NonReadableStream();

            var act = async () => await ArchiveFormatDetector.FromStreamAsync(stream);

            await act.Should().ThrowAsync<ArgumentException>().WithParameterName("stream");
        }

        private sealed class NonReadableStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position
            {
                get => 0;
                set => throw new NotSupportedException();
            }

            public override void Flush() { }

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) { }
        }

        #endregion
    }
}
