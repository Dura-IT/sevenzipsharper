using System;
using System.Collections.Generic;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop
{
    [TestOf(typeof(ArchiveClassIds))]
    public sealed class ArchiveClassIdsTests
    {
        private static IEnumerable<Guid> AllClassIds()
        {
            yield return ArchiveClassIds.SevenZip;
            yield return ArchiveClassIds.Zip;
            yield return ArchiveClassIds.BZip2;
            yield return ArchiveClassIds.Arj;
            yield return ArchiveClassIds.Lzh;
            yield return ArchiveClassIds.Cab;
            yield return ArchiveClassIds.Iso;
            yield return ArchiveClassIds.GZip;
            yield return ArchiveClassIds.Tar;
            yield return ArchiveClassIds.Xz;
            yield return ArchiveClassIds.Wim;
        }

        [Test]
        public void AllClassIds_AcrossAllFormats_AreUnique()
        {
            AllClassIds().Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void AllClassIds_AcrossAllFormats_ShareExpectedPrefix()
        {
            foreach (var guid in AllClassIds())
                guid.ToString().Should().StartWith("23170f69-40c1-278a-1000-0001");
        }
    }
}
