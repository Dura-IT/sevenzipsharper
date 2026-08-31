using System;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop
{
    [TestOf(typeof(ArchiveFormatRegistry))]
    public sealed class ArchiveFormatRegistryTests
    {
        [TestCase(ArchiveFormat.SevenZip)]
        [TestCase(ArchiveFormat.Zip)]
        [TestCase(ArchiveFormat.BZip2)]
        [TestCase(ArchiveFormat.Arj)]
        [TestCase(ArchiveFormat.Lzh)]
        [TestCase(ArchiveFormat.Cab)]
        [TestCase(ArchiveFormat.Iso)]
        [TestCase(ArchiveFormat.GZip)]
        [TestCase(ArchiveFormat.Tar)]
        [TestCase(ArchiveFormat.Xz)]
        [TestCase(ArchiveFormat.Wim)]
        public void GetClassId_KnownFormat_ReturnsNonEmptyGuid(ArchiveFormat format)
        {
            var classId = ArchiveFormatRegistry.GetClassId(format);

            classId.Should().NotBeEmpty();
        }

        [Test]
        public void GetClassId_KnownFormat_ReturnsMatchingClassId()
        {
            ArchiveFormatRegistry.GetClassId(ArchiveFormat.SevenZip).Should().Be(ArchiveClassIds.SevenZip);
            ArchiveFormatRegistry.GetClassId(ArchiveFormat.Zip).Should().Be(ArchiveClassIds.Zip);
            ArchiveFormatRegistry.GetClassId(ArchiveFormat.GZip).Should().Be(ArchiveClassIds.GZip);
        }

        [Test]
        public void GetClassId_UnknownFormat_ThrowsArgumentOutOfRangeException()
        {
            var act = () => ArchiveFormatRegistry.GetClassId((ArchiveFormat)999);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
