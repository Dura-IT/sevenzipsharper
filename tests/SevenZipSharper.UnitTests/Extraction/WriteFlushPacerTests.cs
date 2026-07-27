using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Extraction;

namespace SevenZipSharper.UnitTests.Extraction;

[TestOf(typeof(WriteFlushPacer))]
public sealed class WriteFlushPacerTests
{
    [Test]
    public void ShouldFlush_ReturnsFalse_BeforeIntervalReached()
    {
        var pacer = new WriteFlushPacer(100);

        pacer.ShouldFlush(99).Should().BeFalse();
    }

    [Test]
    public void ShouldFlush_ReturnsTrue_WhenAccumulatedBytesExactlyReachInterval()
    {
        var pacer = new WriteFlushPacer(100);

        pacer.ShouldFlush(100).Should().BeTrue();
    }

    [Test]
    public void ShouldFlush_ReturnsTrue_WhenSingleWriteExceedsInterval()
    {
        var pacer = new WriteFlushPacer(100);

        pacer.ShouldFlush(250).Should().BeTrue();
    }

    [Test]
    public void ShouldFlush_AccumulatesAcrossWrites_UntilIntervalReached()
    {
        var pacer = new WriteFlushPacer(100);

        pacer.ShouldFlush(60).Should().BeFalse();
        pacer.ShouldFlush(40).Should().BeTrue();
    }

    [Test]
    public void ShouldFlush_ResetsAccumulator_AfterFlushing()
    {
        var pacer = new WriteFlushPacer(100);

        pacer.ShouldFlush(100).Should().BeTrue(); // first interval reached
        pacer.ShouldFlush(50).Should().BeFalse(); // accumulator reset after flush
        pacer.ShouldFlush(50).Should().BeTrue(); // interval reached again
    }

    [Test]
    public void ShouldFlush_AlwaysReturnsFalse_WhenIntervalIsZero()
    {
        var pacer = new WriteFlushPacer(0);

        pacer.ShouldFlush(int.MaxValue).Should().BeFalse();
        pacer.ShouldFlush(int.MaxValue).Should().BeFalse();
    }

    [Test]
    public void ShouldFlush_AlwaysReturnsFalse_WhenIntervalIsNegative()
    {
        var pacer = new WriteFlushPacer(-1);

        pacer.ShouldFlush(int.MaxValue).Should().BeFalse();
    }
}
