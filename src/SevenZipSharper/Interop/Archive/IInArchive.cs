using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.Interop.Archive;

[GeneratedComInterface]
[Guid("23170F69-40C1-278A-0000-000600600000")]
internal partial interface IInArchive
{
    [PreserveSig]
    int Open(IInStream stream, IntPtr maxCheckStartPosition, IArchiveOpenCallback? openArchiveCallback);

    [PreserveSig]
    int Close();

    [PreserveSig]
    int GetNumberOfItems(out uint numItems);

    // value is a raw pointer to a PROPVARIANT-sized buffer. We pass nint instead of
    // ref PropVariant because Windows' propidlbase.h PROPVARIANT is 24 bytes on x64 while
    // our managed PropVariant struct is 16 bytes (matching POSIX 7-Zip's MyWindows.h
    // PROPVARIANT); a ref into a 16-byte struct would let native overflow into adjacent
    // memory on Windows. Callers should stackalloc 24 bytes (worst case across both
    // platforms) and zero before the call.
    [PreserveSig]
    int GetProperty(uint index, ItemPropId propId, nint value);

    [PreserveSig]
    int Extract([In, MarshalUsing(CountElementName = "numItems")] uint[]? indices, uint numItems, int testMode, IArchiveExtractCallback extractCallback);

    // See GetProperty for why this takes nint, not ref PropVariant.
    [PreserveSig]
    int GetArchiveProperty(ItemPropId propId, nint value);

    [PreserveSig]
    int GetNumberOfProperties(out uint numProps);

    [PreserveSig]
    int GetPropertyInfo(uint index, [MarshalUsing(typeof(SevenZipBStrMarshaller))] out string? name, out uint propId, out ushort varType);

    [PreserveSig]
    int GetNumberOfArchiveProperties(out uint numProps);

    [PreserveSig]
    int GetArchivePropertyInfo(uint index, [MarshalUsing(typeof(SevenZipBStrMarshaller))] out string? name, out uint propId, out ushort varType);
}
