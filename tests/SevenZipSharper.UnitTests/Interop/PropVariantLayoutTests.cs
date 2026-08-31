using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop;

[TestOf(typeof(PropVariantLayout))]
public sealed class PropVariantLayoutTests
{
    [Test]
    public void GetPropVariantStride_Windows64Bit_Returns24()
    {
        PropVariantLayout.GetPropVariantStride(isWindows: true, pointerSize: 8).Should().Be(24);
    }

    // win-x86: the Windows PROPVARIANT collapses to 16 bytes on a 4-byte pointer, so the
    // SetProperties array must be strided at 16, not the 64-bit value of 24 (issue #6).
    [Test]
    public void GetPropVariantStride_Windows32Bit_Returns16()
    {
        PropVariantLayout.GetPropVariantStride(isWindows: true, pointerSize: 4).Should().Be(PropVariantLayout.ManagedSize);
    }

    [TestCase(8)]
    [TestCase(4)]
    public void GetPropVariantStride_NonWindows_ReturnsManagedSize(int pointerSize)
    {
        PropVariantLayout.GetPropVariantStride(isWindows: false, pointerSize: pointerSize).Should().Be(PropVariantLayout.ManagedSize);
    }
}
