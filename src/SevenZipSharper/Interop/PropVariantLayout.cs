namespace SevenZipSharper.Interop;

// Platform-dependent memory layout for native PROPVARIANT interop. The stride between
// consecutive PROPVARIANT array elements is architecture-dependent on Windows, which the
// SetProperties marshalling path (SevenZipCompressor.AllocPlatformValuesBuffer) must honour
// when it repacks the managed PropVariant[] into a native buffer.
internal static class PropVariantLayout
{
    // Size of the managed PropVariant struct (StructLayout Size = 16). Matches POSIX 7-Zip's
    // MyWindows.h PROPVARIANT (8-byte header + 8-byte union) on every architecture.
    internal const int ManagedSize = 16;

    // Stride, in bytes, between consecutive native PROPVARIANT elements for the running
    // platform. POSIX always equals ManagedSize. Windows propidlbase.h PROPVARIANT is 24 bytes
    // on 64-bit (8-byte header + 16-byte union, padded to hold counted-array members
    // {ULONG cElems; T* pElems}) but only 16 bytes on 32-bit, where the 4-byte pointer
    // collapses that union back to 8 bytes.
    internal static int GetPropVariantStride(bool isWindows, int pointerSize)
    {
        if (!isWindows)
            return ManagedSize;

        return pointerSize == 8 ? 24 : ManagedSize;
    }
}
