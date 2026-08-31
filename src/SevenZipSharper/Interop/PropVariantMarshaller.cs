using System;
using System.Runtime.InteropServices;

namespace SevenZipSharper.Interop
{
    internal static class PropVariantMarshaller
    {
        // Returns a pointer to a contiguous array of PROPVARIANT values laid out at the running
        // platform's native stride, plus a freer to release any unmanaged allocation.
        internal static (nint Ptr, Action Free) AllocValuesBuffer(PropVariant[] values)
        {
            var stride = PropVariantLayout.GetPropVariantStride(OperatingSystem.IsWindows(), IntPtr.Size);
            return PackAtStride(values, stride);
        }

        // Packs the managed PropVariant[] into native memory at the given element stride. When the
        // stride equals the managed PropVariant size (POSIX and win-x86) the array layout already
        // matches 7-Zip's PROPVARIANT, so the array is pinned in place. A wider stride (win-x64/arm64)
        // allocates a fresh buffer and copies each 16-byte PropVariant into the head of its slot,
        // leaving the trailing bytes zeroed. Parameterised on stride so the wider-stride copy path is
        // unit-testable off Windows.
        internal static (nint Ptr, Action Free) PackAtStride(PropVariant[] values, int stride)
        {
            if (stride == PropVariantLayout.ManagedSize)
            {
                var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
                return (handle.AddrOfPinnedObject(), () => handle.Free());
            }

            var buf = Marshal.AllocCoTaskMem(values.Length * stride);
            for (var i = 0; i < values.Length; i++)
            {
                var src = MemoryMarshal.AsBytes(values.AsSpan(i, 1));
                for (var b = 0; b < PropVariantLayout.ManagedSize; b++)
                    Marshal.WriteByte(buf + (i * stride) + b, src[b]);
                for (var b = PropVariantLayout.ManagedSize; b < stride; b++)
                    Marshal.WriteByte(buf + (i * stride) + b, 0);
            }
            return (buf, () => Marshal.FreeCoTaskMem(buf));
        }
    }
}
