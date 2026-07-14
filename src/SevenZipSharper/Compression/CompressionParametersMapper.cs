using System.Collections.Generic;
using SevenZipSharper;
using SevenZipSharper.Compression;
using SevenZipSharper.Interop;

namespace SevenZipSharper.Compression;

internal static class CompressionParametersMapper
{
    // 7-Zip ISetProperties key names
    private const string PropKeyLevel = "x";
    private const string PropKeyMethod = "0"; // codec at position 0 in the filter chain
    private const string PropKeySolid = "s";
    private const string PropKeyDictionarySize = "d";
    private const string PropKeyWordSize = "fb";
    private const string PropKeyThreadCount = "mt";
    private const string PropKeyEncryptHeaders = "he";
    private const string PropKeyZipEncryptionMethod = "em";

    private const string PropValueOn = "on";
    private const string PropValueOff = "off";

    // 7-Zip's Zip handler treats bare "AES" as AES-256 (see ZipHandlerOut.cpp: v defaults to 3 when no bit-count follows).
    private const string MethodValueAes = "AES";

    // ISetProperties method name values
    private const string MethodNameLzma = "LZMA";
    private const string MethodNameLzma2 = "LZMA2";
    private const string MethodNameBZip2 = "BZip2";
    private const string MethodNameDeflate = "Deflate";
    private const string MethodNamePpmd = "PPMd";
    private const string MethodNameCopy = "Copy";

    internal static (string[] Names, PropVariant[] Values) ToSetProperties(
        CompressionParameters parameters,
        ArchiveFormat format
    )
    {
        var names = new List<string>();
        var values = new List<PropVariant>();

        void Add(string name, PropVariant value)
        {
            names.Add(name);
            values.Add(value);
        }

        Add(PropKeyLevel, PropVariant.FromUInt32((uint)parameters.Level));

        // Single-codec formats (GZip, BZip2, Tar, Xz) have a fixed codec and reject the method
        // property with E_INVALIDARG. Only emit it for formats that support codec selection.
        if (format is ArchiveFormat.SevenZip or ArchiveFormat.Zip)
            Add(PropKeyMethod, PropVariant.FromString(ToMethodName(parameters.Method)));

        // Solid mode is 7z-specific; the Zip handler rejects it with E_INVALIDARG, which
        // would abort SetProperties before em=AES is ever processed.
        if (format == ArchiveFormat.SevenZip)
        {
            Add(
                PropKeySolid,
                PropVariant.FromString(parameters.SolidMode ? PropValueOn : PropValueOff)
            );
        }

        if (parameters.DictionarySize.HasValue)
            Add(PropKeyDictionarySize, PropVariant.FromUInt32(parameters.DictionarySize.Value));

        if (parameters.WordSize.HasValue)
            Add(PropKeyWordSize, PropVariant.FromUInt32(parameters.WordSize.Value));

        if (parameters.ThreadCount.HasValue)
            Add(PropKeyThreadCount, PropVariant.FromUInt32((uint)parameters.ThreadCount.Value));

        if (parameters.EncryptHeaders && !string.IsNullOrEmpty(parameters.EncryptionPassword))
            Add(PropKeyEncryptHeaders, PropVariant.FromString(PropValueOn));

        if (parameters.EncryptionPassword is not null && format == ArchiveFormat.Zip)
            Add(PropKeyZipEncryptionMethod, PropVariant.FromString(MethodValueAes));

        return (names.ToArray(), values.ToArray());
    }

    private static string ToMethodName(CompressionMethod method) =>
        method switch
        {
            CompressionMethod.Lzma => MethodNameLzma,
            CompressionMethod.Lzma2 => MethodNameLzma2,
            CompressionMethod.BZip2 => MethodNameBZip2,
            CompressionMethod.Deflate => MethodNameDeflate,
            CompressionMethod.Ppmd => MethodNamePpmd,
            CompressionMethod.Copy => MethodNameCopy,
            _ => throw new System.ArgumentOutOfRangeException(nameof(method), method, null),
        };
}
