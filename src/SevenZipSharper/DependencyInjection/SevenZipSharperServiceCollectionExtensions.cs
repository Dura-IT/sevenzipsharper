using System;
using System.IO;
using Microsoft.Extensions.Logging;
using SevenZipSharper;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering SevenZipSharper services.
/// </summary>
public static class SevenZipSharperServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SevenZipExtractor"/> and <see cref="SevenZipCompressor"/> factories as singletons.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSevenZipSharper(this IServiceCollection services)
    {
        NativeLibraryLoader.Register();

        services.AddSingleton<Func<Stream, ArchiveFormat, SevenZipExtractor>>(sp =>
            (stream, format) =>
                new SevenZipExtractor(
                    stream,
                    format,
                    sp.GetRequiredService<ILogger<SevenZipExtractor>>()
                )
        );

        services.AddSingleton<Func<ArchiveFormat, CompressionParameters, SevenZipCompressor>>(sp =>
            (format, parameters) =>
                new SevenZipCompressor(
                    format,
                    parameters,
                    sp.GetRequiredService<ILogger<SevenZipCompressor>>()
                )
        );

        return services;
    }
}
