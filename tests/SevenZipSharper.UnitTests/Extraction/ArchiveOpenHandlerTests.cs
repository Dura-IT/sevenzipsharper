using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Extraction;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Archive;

namespace SevenZipSharper.UnitTests.Extraction
{
    [TestOf(typeof(ArchiveOpenHandler))]
    public sealed class ArchiveOpenHandlerTests
    {
        [Test]
        public void GetPassword_ReturnsPassword_AndOk_WhenPasswordProvided()
        {
            var handler = new ArchiveOpenHandler("secret");
            IPasswordProvider pp = handler;

            var hr = pp.GetPassword(out var password);

            hr.Should().Be(HResult.Ok);
            password.Should().Be("secret");
        }

        [Test]
        public void GetPassword_ReturnsEmptyString_AndFalse_WhenNoPassword()
        {
            var handler = new ArchiveOpenHandler();
            IPasswordProvider pp = handler;

            var hr = pp.GetPassword(out var password);

            hr.Should().Be(HResult.False);
            password.Should().Be(string.Empty);
        }

        [Test]
        public void SetTotal_WithNullPointers_ReturnsOk()
        {
            var handler = new ArchiveOpenHandler();
            IArchiveOpenCallback cb = handler;

            cb.SetTotal(System.IntPtr.Zero, System.IntPtr.Zero).Should().Be(HResult.Ok);
        }

        [Test]
        public void SetCompleted_WithNullPointers_ReturnsOk()
        {
            var handler = new ArchiveOpenHandler();
            IArchiveOpenCallback cb = handler;

            cb.SetCompleted(System.IntPtr.Zero, System.IntPtr.Zero).Should().Be(HResult.Ok);
        }
    }
}
