using System;
using System.Runtime.InteropServices;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Interop;

[TestFixture]
[TestOf(typeof(PlatformInfo))]
public sealed class PlatformInfoTests
{
    [TestCase(Architecture.X64, "win-x64")]
    [TestCase(Architecture.Arm64, "win-arm64")]
    [TestCase(Architecture.X86, "win-x86")]
    public void BuildRuntimeIdentifier_Windows_ReturnsExpectedRid(
        Architecture arch,
        string expected
    )
    {
        PlatformInfo.BuildRuntimeIdentifier(OSPlatform.Windows, arch).Should().Be(expected);
    }

    [TestCase(Architecture.X64, "osx-x64")]
    [TestCase(Architecture.Arm64, "osx-arm64")]
    public void BuildRuntimeIdentifier_OSX_ReturnsExpectedRid(Architecture arch, string expected)
    {
        PlatformInfo.BuildRuntimeIdentifier(OSPlatform.OSX, arch).Should().Be(expected);
    }

    [TestCase(Architecture.X64, "linux-x64")]
    [TestCase(Architecture.Arm64, "linux-arm64")]
    public void BuildRuntimeIdentifier_Linux_ReturnsExpectedRid(Architecture arch, string expected)
    {
        PlatformInfo.BuildRuntimeIdentifier(OSPlatform.Linux, arch).Should().Be(expected);
    }

    [TestCase(Architecture.Arm)]
    [TestCase(Architecture.Wasm)]
    public void BuildRuntimeIdentifier_UnsupportedArchitecture_Throws(Architecture arch)
    {
        var act = () => PlatformInfo.BuildRuntimeIdentifier(OSPlatform.Linux, arch);

        act.Should()
            .Throw<PlatformNotSupportedException>()
            .WithMessage("*Unsupported processor architecture*");
    }

    [Test]
    public void BuildRuntimeIdentifier_UnsupportedOS_Throws()
    {
        var act = () => PlatformInfo.BuildRuntimeIdentifier(OSPlatform.FreeBSD, Architecture.X64);

        act.Should()
            .Throw<PlatformNotSupportedException>()
            .WithMessage("*Unsupported operating system*");
    }

    [Test]
    public void BuildLibraryFileName_Windows_ReturnsDll()
    {
        PlatformInfo.BuildLibraryFileName(OSPlatform.Windows).Should().Be("7z.dll");
    }

    [Test]
    public void BuildLibraryFileName_OSX_ReturnsDylib()
    {
        PlatformInfo.BuildLibraryFileName(OSPlatform.OSX).Should().Be("7z.dylib");
    }

    [Test]
    public void BuildLibraryFileName_Linux_ReturnsSo()
    {
        PlatformInfo.BuildLibraryFileName(OSPlatform.Linux).Should().Be("7z.so");
    }

    [Test]
    public void BuildLibraryFileName_UnsupportedOS_Throws()
    {
        var act = () => PlatformInfo.BuildLibraryFileName(OSPlatform.FreeBSD);

        act.Should()
            .Throw<PlatformNotSupportedException>()
            .WithMessage("*Unsupported operating system*");
    }

    [Test]
    public void GetRuntimeIdentifier_OnHost_ReturnsValidRid()
    {
        PlatformInfo
            .GetRuntimeIdentifier()
            .Should()
            .MatchRegex("^(win|osx|linux)-(x64|arm64|x86)$");
    }

    [Test]
    public void GetLibraryFileName_OnHost_ReturnsValidFileName()
    {
        PlatformInfo.GetLibraryFileName().Should().BeOneOf("7z.dll", "7z.dylib", "7z.so");
    }
}
