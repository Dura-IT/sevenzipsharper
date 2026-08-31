using System;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace SevenZipSharper.IntegrationTests
{
    /// <summary>
    /// Assembly-level setup fixture that skips all integration tests when the native 7-Zip library
    /// is not available (e.g. on CI without native assets, or a fresh clone without runtimes/).
    /// </summary>
    [SetUpFixture]
    public class NativeLibraryGuard
    {
        [OneTimeSetUp]
        public void CheckNativeLibrary()
        {
            try
            {
                using var ms = new System.IO.MemoryStream(new byte[1]);
                using var _ = new SevenZipExtractor(ms, ArchiveFormat.SevenZip, NullLogger<SevenZipExtractor>.Instance);
            }
            catch (Exception ex)
            {
                Assert.Ignore($"Native 7-Zip library not available — integration tests skipped. ({ex.GetType().Name}: {ex.Message})");
            }
        }
    }
}
