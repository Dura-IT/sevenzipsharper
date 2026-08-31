using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices.Marshalling;

namespace SevenZipSharper.Interop.Streams;

[GeneratedComClass]
internal sealed partial class OutStreamAdapter : SeekableStreamAdapterBase, IOutStream
{
    internal OutStreamAdapter(Stream stream)
        : base(stream) { }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "COM callback must translate every managed exception into an HRESULT; exceptions cannot cross the native 7-Zip boundary."
    )]
    int ISequentialOutStream.Write(byte[] data, uint size, out uint processedSize)
    {
        if (size > int.MaxValue)
        {
            processedSize = 0;
            return HResult.InvalidArg;
        }

        try
        {
            _stream.Write(data, 0, (int)size);
            processedSize = size;
            return HResult.Ok;
        }
        catch (Exception)
        {
            processedSize = 0;
            return HResult.Fail;
        }
    }

    int IOutStream.Seek(long offset, uint seekOrigin, nint newPosition) => SeekStream(offset, seekOrigin, newPosition);

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "COM callback must translate every managed exception into an HRESULT; exceptions cannot cross the native 7-Zip boundary."
    )]
    int IOutStream.SetSize(ulong newSize)
    {
        try
        {
            _stream.SetLength((long)newSize);
            return HResult.Ok;
        }
        catch (Exception)
        {
            return HResult.Fail;
        }
    }
}
