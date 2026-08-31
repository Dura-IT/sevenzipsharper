using System;
using System.IO;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using SevenZipSharper.Compression;

namespace SevenZipSharper.UnitTests.DependencyInjection;

[TestOf(typeof(SevenZipSharperServiceCollectionExtensions))]
public sealed class SevenZipSharperServiceCollectionExtensionsTests
{
    [Test]
    public void AddSevenZipSharper_RegistersExtractorFactory()
    {
        var services = new Mock<IServiceCollection>();

        services.Object.AddSevenZipSharper();

        services.Verify(s =>
            s.Add(
                It.Is<ServiceDescriptor>(d =>
                    d.ServiceType == typeof(Func<Stream, ArchiveFormat, SevenZipExtractor>)
                    && d.Lifetime == ServiceLifetime.Singleton
                )
            )
        );
    }

    [Test]
    public void AddSevenZipSharper_RegistersCompressorFactory()
    {
        var services = new Mock<IServiceCollection>();

        services.Object.AddSevenZipSharper();

        services.Verify(s =>
            s.Add(
                It.Is<ServiceDescriptor>(d =>
                    d.ServiceType
                        == typeof(Func<ArchiveFormat, CompressionParameters, SevenZipCompressor>)
                    && d.Lifetime == ServiceLifetime.Singleton
                )
            )
        );
    }

    [Test]
    public void AddSevenZipSharper_ReturnsServiceCollection()
    {
        var services = new Mock<IServiceCollection>();

        var result = services.Object.AddSevenZipSharper();

        result.Should().BeSameAs(services.Object);
    }

    // ── Factory delegate invocation ───────────────────────────────────────
    // Capture the ImplementationFactory from the ServiceDescriptor and invoke
    // it with a mock IServiceProvider. This exercises the outer `sp => ...`
    // lambda without ever calling the native SevenZipExtractor/Compressor
    // constructors (those are only invoked inside the inner returned Func<>).

    [Test]
    public void AddSevenZipSharper_ExtractorFactory_ReturnsNonNullDelegate()
    {
        ServiceDescriptor? captured = null;
        var services = new Mock<IServiceCollection>();
        services
            .Setup(s => s.Add(It.IsAny<ServiceDescriptor>()))
            .Callback<ServiceDescriptor>(d =>
            {
                if (d.ServiceType == typeof(Func<Stream, ArchiveFormat, SevenZipExtractor>))
                    captured = d;
            });
        services.Object.AddSevenZipSharper();

        var factory = captured!.ImplementationFactory!(new Mock<IServiceProvider>().Object);

        factory.Should().NotBeNull().And.BeOfType<Func<Stream, ArchiveFormat, SevenZipExtractor>>();
    }

    [Test]
    public void AddSevenZipSharper_CompressorFactory_ReturnsNonNullDelegate()
    {
        ServiceDescriptor? captured = null;
        var services = new Mock<IServiceCollection>();
        services
            .Setup(s => s.Add(It.IsAny<ServiceDescriptor>()))
            .Callback<ServiceDescriptor>(d =>
            {
                if (
                    d.ServiceType
                    == typeof(Func<ArchiveFormat, CompressionParameters, SevenZipCompressor>)
                )
                {
                    captured = d;
                }
            });
        services.Object.AddSevenZipSharper();

        var factory = captured!.ImplementationFactory!(new Mock<IServiceProvider>().Object);

        factory
            .Should()
            .NotBeNull()
            .And.BeOfType<Func<ArchiveFormat, CompressionParameters, SevenZipCompressor>>();
    }
}
