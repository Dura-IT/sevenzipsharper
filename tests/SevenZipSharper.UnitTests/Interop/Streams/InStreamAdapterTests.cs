using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests.Interop.Streams;

[TestOf(typeof(InStreamAdapter))]
public sealed class InStreamAdapterTests
{
    [Test]
    public void Read_WithSufficientData_ReturnsDataFromUnderlyingStream()
    {
        var content = Encoding.UTF8.GetBytes("hello");
        using var stream = new MemoryStream(content);
        ISequentialInStream adapter = new InStreamAdapter(stream);
        var buffer = new byte[5];

        var hr = adapter.Read(buffer, 5, out var processed);

        hr.Should().Be(0);
        processed.Should().Be(5);
        buffer.Should().Equal(content);
    }

    [Test]
    public void Read_ReturnsActualBytesRead_WhenFewerAvailable()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2 });
        ISequentialInStream adapter = new InStreamAdapter(stream);
        var buffer = new byte[10];

        adapter.Read(buffer, 10, out var processed);

        processed.Should().Be(2);
    }

    [Test]
    public void Read_ReturnsInvalidArg_WhenSizeExceedsIntMax()
    {
        using var stream = new MemoryStream(new byte[1]);
        ISequentialInStream adapter = new InStreamAdapter(stream);
        var buffer = new byte[1];

        // ReSharper disable once RedundantCast — the cast documents uint arithmetic, not int overflow.

        var hr = adapter.Read(buffer, (uint)int.MaxValue + 1u, out var processed);

        hr.Should().Be(HResult.InvalidArg);
        processed.Should().Be(0);
    }

    [Test]
    public void Seek_WithValidOrigin_MovesUnderlyingStreamPosition()
    {
        using var stream = new MemoryStream(new byte[10]);
        IInStream adapter = new InStreamAdapter(stream);
        var ptr = Marshal.AllocHGlobal(sizeof(ulong));
        try
        {
            var hr = adapter.Seek(5, (uint)SeekOrigin.Begin, ptr);

            hr.Should().Be(0);
            ((ulong)Marshal.ReadInt64(ptr)).Should().Be(5);
            stream.Position.Should().Be(5);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [Test]
    public void Seek_ReturnsInvalidArg_WhenSeekOriginIsOutOfRange()
    {
        using var stream = new MemoryStream(new byte[10]);
        IInStream adapter = new InStreamAdapter(stream);

        var hr = adapter.Seek(0, 3, nint.Zero);

        hr.Should().Be(HResult.InvalidArg);
    }

    [Test]
    public void Seek_WithNullNewPosition_ReturnsOk()
    {
        using var stream = new MemoryStream(new byte[10]);
        IInStream adapter = new InStreamAdapter(stream);

        var hr = adapter.Seek(0, (uint)SeekOrigin.Begin, nint.Zero);

        hr.Should().Be(HResult.Ok);
        stream.Position.Should().Be(0);
    }
}
