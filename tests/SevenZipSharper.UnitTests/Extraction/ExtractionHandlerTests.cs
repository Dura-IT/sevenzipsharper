using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Extraction;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests.Extraction
{
    [GeneratedComClass]
    internal sealed partial class TrackingStream : ISequentialOutStream, IDisposable
    {
        public bool Disposed { get; private set; }

        public int Write(byte[] data, uint size, out uint processedSize)
        {
            processedSize = size;
            return HResult.Ok;
        }

        public void Dispose() => Disposed = true;
    }

    file sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        internal SynchronousProgress(Action<T> callback)
        {
            _callback = callback;
        }

        public void Report(T value) => _callback(value);
    }

    [TestOf(typeof(ExtractionHandler))]
    public sealed class ExtractionHandlerTests
    {
        private static ExtractionHandler CreateHandler(
            Func<uint, (ISequentialOutStream? Stream, string EntryPath)>? streamProvider = null,
            IProgress<ExtractionProgress>? progress = null,
            int totalEntries = 1,
            CancellationToken cancellationToken = default
        ) => new ExtractionHandler(streamProvider ?? (_ => (null, string.Empty)), progress, totalEntries, cancellationToken);

        [Test]
        public void SetTotal_WithAnyValue_ReturnsOk()
        {
            var handler = CreateHandler();
            IArchiveExtractCallback cb = handler;

            cb.SetTotal(12345).Should().Be(HResult.Ok);
        }

        [Test]
        public void SetCompleted_ReturnsOk_WhenTokenNotCancelled()
        {
            var handler = CreateHandler();
            IArchiveExtractCallback cb = handler;
            cb.SetTotal(1000);

            cb.SetCompleted(nint.Zero).Should().Be(HResult.Ok);
        }

        [Test]
        public void SetCompleted_ReturnsAbort_WhenTokenCancelled()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var handler = CreateHandler(cancellationToken: cts.Token);
            IArchiveExtractCallback cb = handler;

            cb.SetCompleted(nint.Zero).Should().Be(HResult.Abort);
        }

        [Test]
        public void SetCompleted_ReportsProgress_WhenProgressProvided()
        {
            var reported = new List<ExtractionProgress>();
            var progress = new SynchronousProgress<ExtractionProgress>(p => reported.Add(p));
            var handler = CreateHandler(progress: progress, totalEntries: 5);
            IArchiveExtractCallback cb = handler;
            cb.SetTotal(1000);
            var ptr = Marshal.AllocHGlobal(sizeof(ulong));
            try
            {
                Marshal.WriteInt64(ptr, (long)300UL);
                cb.SetCompleted(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            reported.Should().HaveCount(1);
            reported[0].BytesProcessed.Should().Be(300);
            reported[0].TotalBytes.Should().Be(1000);
            reported[0].TotalEntries.Should().Be(5);
        }

        [Test]
        public void PrepareOperation_ReturnsOk()
        {
            var handler = CreateHandler();
            IArchiveExtractCallback cb = handler;

            cb.PrepareOperation(AskMode.Extract).Should().Be(HResult.Ok);
        }

        [Test]
        public void GetStream_ReturnsNull_AndOk_ForNonExtractMode()
        {
            var handler = CreateHandler();
            IArchiveExtractCallback cb = handler;

            var hr = cb.GetStream(0, out var outStream, AskMode.Skip);

            hr.Should().Be(HResult.Ok);
            outStream.Should().BeNull();
        }

        [Test]
        public void GetStream_CallsStreamProvider_AndAssignsOutStream_ForExtractMode()
        {
            var expected = new TrackingStream();
            var called = false;
            var handler = CreateHandler(streamProvider: _ =>
            {
                called = true;
                return (expected, string.Empty);
            });
            IArchiveExtractCallback cb = handler;

            cb.GetStream(0, out var outStream, AskMode.Extract);

            called.Should().BeTrue();
            outStream.Should().BeSameAs(expected);
        }

        [Test]
        public void GetStream_SetsCurrentEntryPath_ForProgressReporting()
        {
            var reported = new List<ExtractionProgress>();
            var progress = new SynchronousProgress<ExtractionProgress>(p => reported.Add(p));
            var handler = CreateHandler(streamProvider: _ => (null, "some/file.txt"), progress: progress);
            IArchiveExtractCallback cb = handler;
            cb.SetTotal(100);

            cb.GetStream(0, out _, AskMode.Extract);
            var ptr = Marshal.AllocHGlobal(sizeof(ulong));
            try
            {
                Marshal.WriteInt64(ptr, (long)50UL);
                cb.SetCompleted(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            reported[0].EntryPath.Should().Be("some/file.txt");
        }

        [Test]
        public void SetOperationResult_DisposesCurrentStream()
        {
            var stream = new TrackingStream();
            var handler = CreateHandler(streamProvider: _ => (stream, "file.bin"));
            IArchiveExtractCallback cb = handler;

            cb.GetStream(0, out var outStream, AskMode.Extract);
            outStream.Should().BeSameAs(stream);
            cb.SetOperationResult(OperationResult.Ok);

            stream.Disposed.Should().BeTrue();
        }

        [Test]
        public void SetOperationResult_DoesNotThrow_WhenNoStream()
        {
            var handler = CreateHandler();
            IArchiveExtractCallback cb = handler;
            cb.GetStream(0, out _, AskMode.Extract);

            FluentActions.Invoking(() => cb.SetOperationResult(OperationResult.Ok)).Should().NotThrow();
        }

        [Test]
        public void LastEntryError_IsOk_WhenAllEntriesSucceed()
        {
            var handler = CreateHandler();
            IArchiveExtractCallback cb = handler;
            cb.GetStream(0, out _, AskMode.Extract);

            cb.SetOperationResult(OperationResult.Ok);

            handler.LastEntryError.Should().Be(OperationResult.Ok);
        }

        [Test]
        public void LastEntryError_IsCrcError_WhenEntryFails()
        {
            var handler = CreateHandler();
            IArchiveExtractCallback cb = handler;
            cb.GetStream(0, out _, AskMode.Extract);

            cb.SetOperationResult(OperationResult.CrcError);

            handler.LastEntryError.Should().Be(OperationResult.CrcError);
        }
    }
}
