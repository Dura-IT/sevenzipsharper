using System;
using System.IO;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop
{
    [TestOf(typeof(NativeLibraryLoader))]
    public sealed class NativeLibraryLoaderTests
    {
        [Test]
        public void ResolveLibraryPath_WhenFileExists_ReturnsCorrectPath()
        {
            var rid = PlatformInfo.GetRuntimeIdentifier();
            var fileName = PlatformInfo.GetLibraryFileName();
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var nativeDir = Path.Combine(tempDir, "runtimes", rid, "native");
            var expectedPath = Path.Combine(nativeDir, fileName);

            Directory.CreateDirectory(nativeDir);
            File.WriteAllBytes(expectedPath, []);

            try
            {
                var result = NativeLibraryLoader.ResolveLibraryPath(tempDir);

                result.Should().Be(expectedPath);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        public void ResolveLibraryPath_WhenFileNotFound_ThrowsDllNotFoundException()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var act = () => NativeLibraryLoader.ResolveLibraryPath(tempDir);

            act.Should().Throw<DllNotFoundException>().WithMessage("*native library not found*");
        }
    }
}
