using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Interop;
using SevenZipSharper.Interop.Streams;

namespace SevenZipSharper.UnitTests.Interop.Streams;

[TestOf(typeof(OutStreamAdapter))]
public sealed class OutStreamAdapterTests
{
    [Test]
    public void Write_WithValidData_WritesDataToUnderlyingStream()
    {
        using var stream = new MemoryStream();
        ISequentialOutStream adapter = new OutStreamAdapter(stream);
        var data = Encoding.UTF8.GetBytes("hello");

        var hr = adapter.Write(data, (uint)data.Length, out var processed);

        hr.Should().Be(0);
        processed.Should().Be((uint)data.Length);
        stream.ToArray().Should().Equal(data);
    }

    [Test]
    public void Write_ReturnsInvalidArg_WhenSizeExceedsIntMax()
    {
        using var stream = new MemoryStream();
        ISequentialOutStream adapter = new OutStreamAdapter(stream);
        var data = new byte[1];

        // ReSharper disable once RedundantCast — the cast documents uint arithmetic, not int overflow.

        var hr = adapter.Write(data, (uint)int.MaxValue + 1u, out var processed);

        hr.Should().Be(HResult.InvalidArg);
        processed.Should().Be(0);
    }

    [Test]
    public void Seek_WithValidOrigin_MovesUnderlyingStreamPosition()
    {
        using var stream = new MemoryStream(new byte[10]);
        IOutStream adapter = new OutStreamAdapter(stream);
        var ptr = Marshal.AllocHGlobal(sizeof(ulong));
        try
        {
            var hr = adapter.Seek(4, (uint)SeekOrigin.Begin, ptr);

            hr.Should().Be(0);
            ((ulong)Marshal.ReadInt64(ptr)).Should().Be(4);
            stream.Position.Should().Be(4);
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
        IOutStream adapter = new OutStreamAdapter(stream);

        var hr = adapter.Seek(0, 3, nint.Zero);

        hr.Should().Be(HResult.InvalidArg);
    }

    [Test]
    public void Seek_WithNullNewPosition_ReturnsOk()
    {
        using var stream = new MemoryStream(new byte[10]);
        IOutStream adapter = new OutStreamAdapter(stream);

        var hr = adapter.Seek(0, (uint)SeekOrigin.Begin, nint.Zero);

        hr.Should().Be(HResult.Ok);
        stream.Position.Should().Be(0);
    }

    [Test]
    public void SetSize_WithPositiveSize_SetsLengthOnUnderlyingStream()
    {
        using var stream = new MemoryStream();
        IOutStream adapter = new OutStreamAdapter(stream);

        var hr = adapter.SetSize(128);

        hr.Should().Be(0);
        stream.Length.Should().Be(128);
    }
}
