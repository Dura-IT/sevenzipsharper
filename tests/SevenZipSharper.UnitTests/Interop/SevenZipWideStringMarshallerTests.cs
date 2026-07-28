using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop;

[TestOf(typeof(SevenZipWideStringMarshaller))]
[SuppressMessage(
    "Security",
    "S6640:Make sure that using \"unsafe\" is safe here.",
    Justification = "The test class is unsafe only to invoke SevenZipWideStringMarshaller's raw-pointer "
        + "methods; every pointer originates from and is freed by the marshaller under test."
)]
public sealed unsafe class SevenZipWideStringMarshallerTests
{
    [Test]
    public void ConvertToUnmanaged_NullManaged_ReturnsNullPointer()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(null);

        ((nint)ptr).Should().Be(nint.Zero);
    }

    [Test]
    public void ConvertToManaged_NullPointer_ReturnsNull()
    {
        SevenZipWideStringMarshaller.ConvertToManaged(null).Should().BeNull();
    }

    [Test]
    public void Free_NullPointer_DoesNotThrow()
    {
        var act = () => SevenZipWideStringMarshaller.Free(null);

        act.Should().NotThrow();
    }

    [Test]
    public void ConvertToUnmanaged_EmptyString_RoundTrips()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(string.Empty);
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be(string.Empty);
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_SingleAsciiChar_RoundTrips()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged("x");
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be("x");
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_MultiCharAscii_RoundTrips()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged("LZMA");
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be("LZMA");
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_MixedCaseKeyword_RoundTrips()
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged("LZMA2");
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be("LZMA2");
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [Test]
    public void ConvertToUnmanaged_BmpUnicode_RoundTrips()
    {
        // U+00E9 (é), U+4E2D (中), U+00FC (ü) — all in BMP, safely representable as wchar_t
        const string value = "é中ü";
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(value);
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be(value);
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }

    [TestCase("on")]
    [TestCase("off")]
    [TestCase("e")]
    [TestCase("0")]
    public void ConvertToUnmanaged_CommonPropertyValues_RoundTrips(string value)
    {
        var ptr = SevenZipWideStringMarshaller.ConvertToUnmanaged(value);
        try
        {
            SevenZipWideStringMarshaller.ConvertToManaged(ptr).Should().Be(value);
        }
        finally
        {
            SevenZipWideStringMarshaller.Free(ptr);
        }
    }
}
