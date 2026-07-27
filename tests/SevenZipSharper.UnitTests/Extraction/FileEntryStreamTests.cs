using System;
using System.IO;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Extraction;
using SevenZipSharper.Interop;

namespace SevenZipSharper.UnitTests.Extraction;

[TestOf(typeof(FileEntryStream))]
public sealed class FileEntryStreamTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public void Write_WithValidData_WritesDataToFileAndReturnsOk()
    {
        var path = Path.Combine(_tempDir, "out.bin");
        var data = new byte[] { 1, 2, 3, 4, 5 };
        using var stream = new FileEntryStream(path, flushIntervalBytes: 0);

        var hr = stream.Write(data, (uint)data.Length, out var processedSize);

        hr.Should().Be(HResult.Ok);
        processedSize.Should().Be((uint)data.Length);
        stream.Dispose();
        File.ReadAllBytes(path).Should().Equal(data);
    }

    [Test]
    public void Write_WhenStreamIsDisposed_ReturnsFailAndZeroProcessedSize()
    {
        var path = Path.Combine(_tempDir, "disposed.bin");
        var stream = new FileEntryStream(path, flushIntervalBytes: 0);
        stream.Dispose();

        var hr = stream.Write(new byte[] { 1 }, 1, out var processedSize);

        hr.Should().Be(HResult.Fail);
        processedSize.Should().Be(0u);
    }

    [Test]
    public void Write_WhenFlushIntervalReached_FlushesToDiskAndWritesCorrectData()
    {
        var path = Path.Combine(_tempDir, "flushed.bin");
        var data = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
        using var stream = new FileEntryStream(path, flushIntervalBytes: 4);

        var hr = stream.Write(data, (uint)data.Length, out var processedSize);

        hr.Should().Be(HResult.Ok);
        processedSize.Should().Be((uint)data.Length);
        stream.Dispose();
        File.ReadAllBytes(path).Should().Equal(data);
    }

    [Test]
    public void Write_AcrossMultipleCalls_WithFlushInterval_WritesAllDataInOrder()
    {
        var path = Path.Combine(_tempDir, "multi.bin");
        var first = new byte[] { 1, 2, 3 };
        var second = new byte[] { 4, 5, 6 };
        var expected = new byte[] { 1, 2, 3, 4, 5, 6 };
        using var stream = new FileEntryStream(path, flushIntervalBytes: 4);

        stream.Write(first, (uint)first.Length, out _).Should().Be(HResult.Ok);
        stream.Write(second, (uint)second.Length, out _).Should().Be(HResult.Ok);

        stream.Dispose();
        File.ReadAllBytes(path).Should().Equal(expected);
    }

    [Test]
    public void Constructor_CreatesNestedDirectories()
    {
        var nested = Path.Combine(_tempDir, "a", "b", "file.txt");

        using var stream = new FileEntryStream(nested, flushIntervalBytes: 0);

        Directory.Exists(Path.Combine(_tempDir, "a", "b")).Should().BeTrue();
    }

    [Test]
    public void Dispose_WhenStreamIsOpen_AllowsAnotherWriterToOpen()
    {
        var path = Path.Combine(_tempDir, "reopen.bin");
        var stream = new FileEntryStream(path, flushIntervalBytes: 0);
        stream.Dispose();

        var act = () =>
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        };

        act.Should().NotThrow<IOException>();
    }
}
