using System.Diagnostics.CodeAnalysis;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop
{
    [TestOf(typeof(SevenZipBStrMarshaller))]
    [SuppressMessage(
        "Security",
        "S6640:Make sure that using \"unsafe\" is safe here.",
        Justification = "The test class is unsafe only to invoke SevenZipBStrMarshaller's raw-pointer "
            + "methods; every pointer originates from and is freed by the marshaller under test."
    )]
    public sealed unsafe class SevenZipBStrMarshallerTests
    {
        [Test]
        public void ConvertToUnmanaged_NullManaged_ReturnsNullPointer()
        {
            var ptr = SevenZipBStrMarshaller.ConvertToUnmanaged(null);

            ((nint)ptr).Should().Be(nint.Zero);
        }

        [Test]
        public void ConvertToManaged_NullPointer_ReturnsNull()
        {
            SevenZipBStrMarshaller.ConvertToManaged(null).Should().BeNull();
        }

        [Test]
        public void Free_NullPointer_DoesNotThrow()
        {
            var act = () => SevenZipBStrMarshaller.Free(null);

            act.Should().NotThrow();
        }

        [Test]
        public void ConvertToUnmanaged_EmptyString_RoundTrips()
        {
            var ptr = SevenZipBStrMarshaller.ConvertToUnmanaged(string.Empty);
            try
            {
                SevenZipBStrMarshaller.ConvertToManaged(ptr).Should().Be(string.Empty);
            }
            finally
            {
                SevenZipBStrMarshaller.Free(ptr);
            }
        }

        [Test]
        public void ConvertToUnmanaged_SingleAsciiChar_RoundTrips()
        {
            var ptr = SevenZipBStrMarshaller.ConvertToUnmanaged("x");
            try
            {
                SevenZipBStrMarshaller.ConvertToManaged(ptr).Should().Be("x");
            }
            finally
            {
                SevenZipBStrMarshaller.Free(ptr);
            }
        }

        [Test]
        public void ConvertToUnmanaged_MultiCharAscii_RoundTrips()
        {
            var ptr = SevenZipBStrMarshaller.ConvertToUnmanaged("password");
            try
            {
                SevenZipBStrMarshaller.ConvertToManaged(ptr).Should().Be("password");
            }
            finally
            {
                SevenZipBStrMarshaller.Free(ptr);
            }
        }

        [Test]
        public void ConvertToUnmanaged_BmpUnicode_RoundTrips()
        {
            // U+00E9 (é), U+4E2D (中), U+00FC (ü) — BMP characters, safely representable.
            const string value = "é中ü";
            var ptr = SevenZipBStrMarshaller.ConvertToUnmanaged(value);
            try
            {
                SevenZipBStrMarshaller.ConvertToManaged(ptr).Should().Be(value);
            }
            finally
            {
                SevenZipBStrMarshaller.Free(ptr);
            }
        }
    }
}
