using System;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop;

[TestOf(typeof(PropVariantMarshaller))]
public sealed class PropVariantMarshallerTests
{
    [Test]
    public void PackAtStride_ManagedStride_PreservesEachValuesBytes()
    {
        var values = new[]
        {
            PropVariant.FromUInt32(0x11223344u),
            PropVariant.FromUInt32(0x55667788u),
        };
        var expected = ExpectedBytes(values);

        var (ptr, free) = PropVariantMarshaller.PackAtStride(values, PropVariantLayout.ManagedSize);
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                for (var b = 0; b < PropVariantLayout.ManagedSize; b++)
                {
                    Marshal
                        .ReadByte(ptr + (i * PropVariantLayout.ManagedSize) + b)
                        .Should()
                        .Be(expected[i][b]);
                }
            }
        }
        finally
        {
            free();
        }
    }

    [Test]
    public void PackAtStride_WiderStride_CopiesHeadAndZeroesTrailingBytes()
    {
        // The win-x64/arm64 layout: a 24-byte stride whose leading 16 bytes hold the managed
        // PropVariant and whose trailing bytes must be zero. This path never runs on the Linux
        // coverage host at runtime, so packing it at an explicit stride is the only way to cover it.
        const int stride = 24;
        var values = new[]
        {
            PropVariant.FromUInt32(0x11223344u),
            PropVariant.FromUInt32(0x55667788u),
        };
        var expected = ExpectedBytes(values);

        var (ptr, free) = PropVariantMarshaller.PackAtStride(values, stride);
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                for (var b = 0; b < PropVariantLayout.ManagedSize; b++)
                    Marshal.ReadByte(ptr + (i * stride) + b).Should().Be(expected[i][b]);
                for (var b = PropVariantLayout.ManagedSize; b < stride; b++)
                    Marshal.ReadByte(ptr + (i * stride) + b).Should().Be(0);
            }
        }
        finally
        {
            free();
        }
    }

    [Test]
    public void AllocValuesBuffer_OnHost_ReturnsNonNullPointerAndFreesCleanly()
    {
        var values = new[] { PropVariant.FromUInt32(1u) };

        var (ptr, free) = PropVariantMarshaller.AllocValuesBuffer(values);

        ptr.Should().NotBe(IntPtr.Zero);
        var act = () => free();
        act.Should().NotThrow();
    }

    private static byte[][] ExpectedBytes(PropVariant[] values)
    {
        var result = new byte[values.Length][];
        for (var i = 0; i < values.Length; i++)
            result[i] = MemoryMarshal.AsBytes(values.AsSpan(i, 1)).ToArray();
        return result;
    }
}
