using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Extraction;

// streamProvider(index) → (output stream or null to skip, entry path for progress reporting).
// Returned streams that implement IDisposable are disposed after each entry completes.
[GeneratedComClass]
internal sealed partial class ExtractionHandler : IArchiveExtractCallback, IPasswordProvider
{
    private readonly Func<uint, (ISequentialOutStream? Stream, string EntryPath)> _streamProvider;
    private readonly IProgress<ExtractionProgress>? _progress;
    private readonly int _totalEntries;
    private readonly CancellationToken _cancellationToken;
    private readonly string? _password;

    private ulong _totalBytes;
    private int _completedEntries;
    private string _currentEntryPath = string.Empty;
    private IDisposable? _activeEntryOwner;

    internal ExtractionHandler(
        Func<uint, (ISequentialOutStream? Stream, string EntryPath)> streamProvider,
        IProgress<ExtractionProgress>? progress,
        int totalEntries,
        CancellationToken cancellationToken,
        string? password = null
    )
    {
        _streamProvider = streamProvider;
        _progress = progress;
        _totalEntries = totalEntries;
        _cancellationToken = cancellationToken;
        _password = password;
    }

    int IPasswordProvider.GetPassword(out string password)
    {
        password = _password ?? string.Empty;
        return _password is not null ? HResult.Ok : HResult.False;
    }

    internal OperationResult LastEntryError { get; private set; } = OperationResult.Ok;

    int IProgress.SetTotal(ulong total)
    {
        _totalBytes = total;
        return HResult.Ok;
    }

    int IProgress.SetCompleted(nint completeValue)
    {
        if (_cancellationToken.IsCancellationRequested)
            return HResult.Abort;

        if (completeValue == nint.Zero)
            return HResult.Ok;

        var bytesProcessed = (ulong)Marshal.ReadInt64(completeValue);
        _progress?.Report(
            new ExtractionProgress
            {
                EntryPath = _currentEntryPath,
                EntryIndex = _completedEntries,
                TotalEntries = _totalEntries,
                BytesProcessed = bytesProcessed,
                TotalBytes = _totalBytes,
            }
        );

        return HResult.Ok;
    }

    int IArchiveExtractCallback.GetStream(uint index, out ISequentialOutStream? outStream, AskMode askExtractMode)
    {
        _activeEntryOwner?.Dispose();
        _activeEntryOwner = null;
        outStream = null;

        if (askExtractMode != AskMode.Extract)
        {
            _currentEntryPath = string.Empty;
            return HResult.Ok;
        }

        var (stream, entryPath) = _streamProvider(index);

        outStream = stream;
        _currentEntryPath = entryPath;

        if (stream is IDisposable disposable)
            _activeEntryOwner = disposable;

        return HResult.Ok;
    }

    int IArchiveExtractCallback.PrepareOperation(AskMode askExtractMode) => HResult.Ok;

    int IArchiveExtractCallback.SetOperationResult(OperationResult result)
    {
        _activeEntryOwner?.Dispose();
        _activeEntryOwner = null;
        _completedEntries++;

        if (result != OperationResult.Ok)
            LastEntryError = result;

        return HResult.Ok;
    }
}
