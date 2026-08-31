using AwesomeAssertions;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.UnitTests.Compression;

[TestOf(typeof(CompressionParameters))]
public sealed class CompressionParametersTests
{
    [Test]
    public void Default_HasExpectedValues()
    {
        var p = CompressionParameters.Default;

        p.Method.Should().Be(CompressionMethod.Lzma2);
        p.Level.Should().Be(CompressionLevel.Normal);
        p.DictionarySize.Should().BeNull();
        p.WordSize.Should().BeNull();
        p.ThreadCount.Should().BeNull();
        p.SolidMode.Should().BeTrue();
        p.EncryptionPassword.Should().BeNull();
        p.EncryptHeaders.Should().BeFalse();
    }

    [Test]
    public void MaximumLzma2_HasExpectedValues()
    {
        var p = CompressionParameters.MaximumLzma2;

        p.Method.Should().Be(CompressionMethod.Lzma2);
        p.Level.Should().Be(CompressionLevel.Ultra);
        p.DictionarySize.Should().Be(128u * 1024u * 1024u);
    }

    [Test]
    public void Store_HasExpectedValues()
    {
        var p = CompressionParameters.Store;

        p.Method.Should().Be(CompressionMethod.Copy);
        p.Level.Should().Be(CompressionLevel.Store);
        p.SolidMode.Should().BeFalse();
    }

    [Test]
    public void Validate_ReturnsOk_ForDefault() => CompressionParameters.Default.Validate().IsSuccess.Should().BeTrue();

    [Test]
    public void Validate_ReturnsOk_ForMaximumLzma2() => CompressionParameters.MaximumLzma2.Validate().IsSuccess.Should().BeTrue();

    [Test]
    public void Validate_ReturnsOk_ForStore() => CompressionParameters.Store.Validate().IsSuccess.Should().BeTrue();

    [Test]
    public void Validate_Fails_WhenThreadCountIsZero()
    {
        var p = CompressionParameters.Default with { ThreadCount = 0 };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenThreadCountIsNegative()
    {
        var p = CompressionParameters.Default with { ThreadCount = -1 };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenEncryptHeadersSetWithoutPassword()
    {
        var p = CompressionParameters.Default with { EncryptHeaders = true };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenEncryptionPasswordIsEmpty()
    {
        var p = CompressionParameters.Default with { EncryptionPassword = string.Empty };

        var result = p.Validate();

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("EncryptionPassword"));
    }

    [Test]
    public void Validate_ReturnsOk_WhenEncryptHeadersSetWithPassword()
    {
        var p = CompressionParameters.Default with { EncryptionPassword = "secret", EncryptHeaders = true };

        p.Validate().IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenLzmaDictionarySizeIsNotPowerOfTwo()
    {
        var p = CompressionParameters.Default with { DictionarySize = 1000 };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_ReturnsOk_WhenLzmaDictionarySizeIsPowerOfTwo()
    {
        var p = CompressionParameters.Default with { DictionarySize = 64 * 1024 * 1024 };

        p.Validate().IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenLzmaDictionarySizeIsTooSmall()
    {
        var p = CompressionParameters.Default with { DictionarySize = 512 };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenBZip2DictionarySizeIsNotMultipleOf100KB()
    {
        var p = CompressionParameters.Default with { Method = CompressionMethod.BZip2, DictionarySize = 150 * 1024 };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_ReturnsOk_WhenBZip2DictionarySizeIsValid()
    {
        var p = CompressionParameters.Default with { Method = CompressionMethod.BZip2, DictionarySize = 300 * 1024 };

        p.Validate().IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenLzmaWordSizeIsTooSmall()
    {
        var p = CompressionParameters.Default with { WordSize = 4 };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_Fails_WhenLzmaWordSizeIsTooLarge()
    {
        var p = CompressionParameters.Default with { WordSize = 274 };

        p.Validate().IsFailed.Should().BeTrue();
    }

    [Test]
    public void Validate_ReturnsOk_WhenLzmaWordSizeIsAtBoundary()
    {
        var low = CompressionParameters.Default with { WordSize = 5 };
        var high = CompressionParameters.Default with { WordSize = 273 };

        low.Validate().IsSuccess.Should().BeTrue();
        high.Validate().IsSuccess.Should().BeTrue();
    }
}
