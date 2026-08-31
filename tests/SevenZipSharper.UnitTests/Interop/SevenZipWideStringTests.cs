using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop;

[TestOf(typeof(SevenZipWideString))]
public sealed class SevenZipWideStringTests
{
    [Test]
    public void Alloc_EmptyString_RoundTrips()
    {
        var ptr = SevenZipWideString.Alloc(string.Empty);
        try
        {
            SevenZipWideString.Read(ptr).Should().Be(string.Empty);
        }
        finally
        {
            SevenZipWideString.Free(ptr);
        }
    }

    [Test]
    public void Alloc_SingleAsciiChar_RoundTrips()
    {
        var ptr = SevenZipWideString.Alloc("x");
        try
        {
            SevenZipWideString.Read(ptr).Should().Be("x");
        }
        finally
        {
            SevenZipWideString.Free(ptr);
        }
    }

    [Test]
    public void Alloc_MultiCharAscii_RoundTrips()
    {
        var ptr = SevenZipWideString.Alloc("LZMA");
        try
        {
            SevenZipWideString.Read(ptr).Should().Be("LZMA");
        }
        finally
        {
            SevenZipWideString.Free(ptr);
        }
    }

    [Test]
    public void Alloc_MixedCaseKeyword_RoundTrips()
    {
        var ptr = SevenZipWideString.Alloc("LZMA2");
        try
        {
            SevenZipWideString.Read(ptr).Should().Be("LZMA2");
        }
        finally
        {
            SevenZipWideString.Free(ptr);
        }
    }

    [Test]
    public void Alloc_BmpUnicode_RoundTrips()
    {
        // U+00E9 (é), U+4E2D (中), U+00FC (ü) — all in BMP, safely representable as wchar_t
        const string value = "é中ü";
        var ptr = SevenZipWideString.Alloc(value);
        try
        {
            SevenZipWideString.Read(ptr).Should().Be(value);
        }
        finally
        {
            SevenZipWideString.Free(ptr);
        }
    }

    [Test]
    public void Read_NullPointer_ReturnsNull()
    {
        SevenZipWideString.Read(nint.Zero).Should().BeNull();
    }

    [Test]
    public void Free_NullPointer_DoesNotThrow()
    {
        var act = () => SevenZipWideString.Free(nint.Zero);
        act.Should().NotThrow();
    }

    [TestCase("on")]
    [TestCase("off")]
    [TestCase("e")]
    [TestCase("0")]
    public void Alloc_CommonPropertyValues_RoundTrips(string value)
    {
        var ptr = SevenZipWideString.Alloc(value);
        try
        {
            SevenZipWideString.Read(ptr).Should().Be(value);
        }
        finally
        {
            SevenZipWideString.Free(ptr);
        }
    }
}
