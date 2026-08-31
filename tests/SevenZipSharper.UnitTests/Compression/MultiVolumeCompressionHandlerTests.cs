using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests.Compression
{
    [TestOf(typeof(MultiVolumeCompressionHandler))]
    public sealed class MultiVolumeCompressionHandlerTests
    {
        private static MultiVolumeCompressionHandler CreateHandler(Func<int, Stream>? factory = null, ulong maxVolumeBytes = 1024 * 1024)
        {
            factory ??= _ => new MemoryStream();
            IReadOnlyList<(string, Stream)> entries = new[] { ("a.txt", (Stream)new MemoryStream(new byte[] { 1 })) };
            return new MultiVolumeCompressionHandler(
                entries,
                progress: null,
                volumeStreamFactory: factory,
                maxVolumeBytes: maxVolumeBytes,
                cancellationToken: CancellationToken.None
            );
        }

        [Test]
        public void GetVolumeSize_ReturnsConfiguredMaxBytes()
        {
            var handler = CreateHandler(maxVolumeBytes: 5_000_000);
            IArchiveUpdateCallback2 cb = handler;
            var pSize = Marshal.AllocHGlobal(8);
            try
            {
                var hr = cb.GetVolumeSize(0, pSize);

                hr.Should().Be(HResult.Ok);
                unchecked((ulong)Marshal.ReadInt64(pSize)).Should().Be(5_000_000UL);
            }
            finally
            {
                Marshal.FreeHGlobal(pSize);
            }
        }

        [Test]
        public void GetVolumeSize_ReturnsSameValueForAllIndices()
        {
            var handler = CreateHandler(maxVolumeBytes: 1024);
            IArchiveUpdateCallback2 cb = handler;
            var pSize = Marshal.AllocHGlobal(8);
            try
            {
                cb.GetVolumeSize(0, pSize);
                var size0 = unchecked((ulong)Marshal.ReadInt64(pSize));

                cb.GetVolumeSize(1, pSize);
                var size1 = unchecked((ulong)Marshal.ReadInt64(pSize));

                cb.GetVolumeSize(99, pSize);
                var size99 = unchecked((ulong)Marshal.ReadInt64(pSize));

                size0.Should().Be(1024UL);
                size1.Should().Be(1024UL);
                size99.Should().Be(1024UL);
            }
            finally
            {
                Marshal.FreeHGlobal(pSize);
            }
        }

        [Test]
        public void GetVolumeSize_WithNullPointer_ReturnsOk()
        {
            var handler = CreateHandler(maxVolumeBytes: 1024);
            IArchiveUpdateCallback2 cb = handler;

            var hr = cb.GetVolumeSize(0, nint.Zero);

            hr.Should().Be(HResult.Ok);
        }

        [Test]
        public void GetVolumeStream_CallsFactoryWithCorrectIndex()
        {
            var calledWith = new List<int>();
            Func<int, Stream> factory = i =>
            {
                calledWith.Add(i);
                return new MemoryStream();
            };
            var handler = CreateHandler(factory);
            IArchiveUpdateCallback2 cb = handler;

            cb.GetVolumeStream(3, out _);

            calledWith.Should().ContainInOrder(3);
        }

        [Test]
        public void GetVolumeStream_ReturnsIOutStream()
        {
            var handler = CreateHandler();
            IArchiveUpdateCallback2 cb = handler;

            var hr = cb.GetVolumeStream(0, out var volumeStream);

            hr.Should().Be(HResult.Ok);
            volumeStream.Should().NotBeNull();
            volumeStream.Should().BeAssignableTo<IOutStream>();
        }

        [Test]
        public void CryptoGetTextPassword2_WithPasswordSet_ReturnsPasswordWithIsDefinedOne()
        {
            IReadOnlyList<(string, Stream)> entries = new[] { ("a.txt", (Stream)new MemoryStream(new byte[] { 1 })) };
            var handler = new MultiVolumeCompressionHandler(entries, null, _ => new MemoryStream(), 1024, CancellationToken.None, password: "hunter2");
            ICryptoGetTextPassword2 crypto = handler;

            var hr = crypto.CryptoGetTextPassword2(out var isDefined, out var password);

            hr.Should().Be(HResult.Ok);
            isDefined.Should().Be(1);
            password.Should().Be("hunter2");
        }

        [Test]
        public void CryptoGetTextPassword2_WithoutPassword_ReturnsZeroAndEmptyString()
        {
            var handler = CreateHandler();
            ICryptoGetTextPassword2 crypto = handler;

            var hr = crypto.CryptoGetTextPassword2(out var isDefined, out var password);

            hr.Should().Be(HResult.Ok);
            isDefined.Should().Be(0);
            password.Should().Be(string.Empty);
        }

        [Test]
        public void InheritedSetCompleted_ReturnsAbort_WhenCancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            IReadOnlyList<(string, Stream)> entries = new[] { ("a.txt", (Stream)new MemoryStream()) };
            var handler = new MultiVolumeCompressionHandler(entries, null, _ => new MemoryStream(), 1024, cts.Token);
            IArchiveUpdateCallback cb = handler;

            cb.SetCompleted(nint.Zero).Should().Be(HResult.Abort);
        }
    }
}
