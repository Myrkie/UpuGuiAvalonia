using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Serilog;
using UnityPackageToolsNet.Models;

namespace UnityPackageToolsNet.Services;

public static class UnityPackageExtractor
{
    private static readonly ILogger Logger = Log.ForContext(typeof(UnityPackageExtractor));
    private static readonly HashSet<string> SupportedStreams = ["asset", "asset.meta", "pathname"];

    public static async Task<IReadOnlyList<ExtractedUnityPackageAsset>> ExtractAsync(string packagePath, string outputDirectory, bool extractMetadata, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath)) throw new FileNotFoundException("Unity package not found.", packagePath);
        string root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        Logger.Information("Opening Unity package {PackagePath}", packagePath);
        await using var input = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[]? packageIcon = ReadPackageIcon(input);
        input.Position = 0;
        await using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
        await using var tar = new TarReader(gzip);
        var assets = new Dictionary<string, PackageEntry>(StringComparer.OrdinalIgnoreCase);
        int entriesRead = 0;
        while (await tar.GetNextEntryAsync(copyData: false, cancellationToken) is { } entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                continue;
            }

            string entryName = NormalizeTarName(entry.Name);
            if (entryName.Equals(".icon.png", StringComparison.OrdinalIgnoreCase))
            {
                packageIcon = await ReadEntryAsync(entry.DataStream, cancellationToken);
                continue;
            }

            string[] parts = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) continue;
            string guid = parts[0].ToLowerInvariant();
            if (!IsValidGuid(guid)) continue;
            string streamName = parts[1];
            if (!SupportedStreams.Contains(streamName)) continue;
            if (!assets.TryGetValue(guid, out PackageEntry? packageEntry))
            {
                packageEntry = new PackageEntry(guid);
                assets.Add(guid, packageEntry);
            }

            if (packageEntry.Streams.ContainsKey(streamName))
            {
                throw new InvalidDataException($"Duplicate package stream: {entry.Name}");
            }

            byte[] content = await ReadEntryAsync(entry.DataStream, cancellationToken);
            packageEntry.Streams.Add(streamName, content);
            entriesRead++;
            Logger.Debug("Read package stream {StreamName} for GUID {Guid}", streamName, guid);
        }

        Logger.Information("Read {StreamCount} package streams belonging to {AssetCount} assets", entriesRead,
            assets.Count);
        if (packageIcon is { Length: > 0 })
        {
            string iconPath = Path.Combine(root, ".icon.png");
            await File.WriteAllBytesAsync(iconPath, packageIcon, cancellationToken);
            Logger.Information("Extracted package icon to {IconPath}", iconPath);
        }

        var result = new List<ExtractedUnityPackageAsset>(assets.Count);
        int completed = 0;
        foreach (PackageEntry packageEntry in assets.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!packageEntry.Streams.TryGetValue("pathname", out byte[]? pathnameContent))
            {
                throw new InvalidDataException($"Missing pathname for GUID {packageEntry.Guid}");
            }

            string pathname = Encoding.UTF8.GetString(pathnameContent);
            ParsePathname(pathname, out string assetPath, out string? digest);
            string destination = ResolveAssetPath(root, assetPath);
            string? assetFile = null;
            string? metaFile = null;
            bool isFolder = !packageEntry.Streams.TryGetValue("asset", out byte[]? assetContent);
            if (isFolder)
            {
                Directory.CreateDirectory(destination);
                Logger.Debug("Created folder {AssetPath} ({Guid})", assetPath, packageEntry.Guid);
            }
            else
            {
                string? parent = Path.GetDirectoryName(destination);
                if (parent is not null) Directory.CreateDirectory(parent);
                await File.WriteAllBytesAsync(destination, assetContent!, cancellationToken);
                assetFile = destination;
                Logger.Debug("Extracted asset {AssetPath} ({Guid})", assetPath, packageEntry.Guid);
            }

            if (extractMetadata && packageEntry.Streams.TryGetValue("asset.meta", out byte[]? metaContent))
            {
                string metaDestination = destination + ".meta";
                string? parent = Path.GetDirectoryName(metaDestination);
                if (parent is not null) Directory.CreateDirectory(parent);
                await File.WriteAllBytesAsync(metaDestination, metaContent, cancellationToken);
                metaFile = metaDestination;
                Logger.Debug("Extracted metadata {MetaPath}", metaDestination);
            }

            result.Add(new ExtractedUnityPackageAsset(Guid: packageEntry.Guid, AssetPath: assetPath,
                AssetFile: assetFile, MetaFile: metaFile, PreviewFile: null, Md4Digest: digest, IsFolder: isFolder));
            completed++;
            Logger.Information("Extracted {Completed}/{Total}: {AssetPath}", completed, assets.Count, assetPath);
        }

        Logger.Information("Unity package extraction finished. Assets: {AssetCount}, Folders: {FolderCount}, Metadata: {MetadataCount}",
            result.Count(x => !x.IsFolder), result.Count(x => x.IsFolder), result.Count(x => x.MetaFile is not null));
        return result;
    }

    private static byte[]? ReadPackageIcon(Stream stream)
    {
        if (!stream.CanSeek) return null;
        long originalPosition = stream.Position;
        try
        {
            stream.Position = 0;
            Span<byte> header = stackalloc byte[10];
            if (!TryReadExactly(stream, header)) return null;
            if (header[0] != 0x1F || header[1] != 0x8B || (header[3] & 0x04) == 0)
            {
                return null;
            }

            Span<byte> lengthBytes = stackalloc byte[2];
            if (!TryReadExactly(stream, lengthBytes)) return null;
            int extraLength = lengthBytes[0] | (lengthBytes[1] << 8);
            if (extraLength <= 0) return null;
            byte[] extraData = new byte[extraLength];
            if (!TryReadExactly(stream, extraData)) return null;
            using var iconStream = new MemoryStream();
            int position = 0;
            while (position + 4 <= extraData.Length)
            {
                byte id1 = extraData[position];
                byte id2 = extraData[position + 1];
                int fieldLength = extraData[position + 2] | (extraData[position + 3] << 8);
                position += 4;
                if (position + fieldLength > extraData.Length) break;
                if (id1 == 'A' && id2 == '%')
                {
                    iconStream.Write(extraData, position, fieldLength);
                }

                position += fieldLength;
            }

            return iconStream.Length > 0 ? iconStream.ToArray() : null;
        }
        finally
        {
            stream.Position = originalPosition;
        }
    }

    private static bool TryReadExactly(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);
            if (read == 0) return false;
            totalRead += read;
        }

        return true;
    }

    private static bool TryReadExactly(Stream stream, byte[] buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0) return false;
            totalRead += read;
        }

        return true;
    }

    private static async Task<byte[]> ReadEntryAsync(Stream? source, CancellationToken cancellationToken)
    {
        if (source is null) return [];
        using var memory = new MemoryStream();
        await source.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }

    private static string NormalizeTarName(string name)
    {
        string normalized = name.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.TrimStart('/');
    }

    private static bool IsValidGuid(string guid)
    {
        return guid.Length == 32 && guid.All(Uri.IsHexDigit);
    }

    private static void ParsePathname(string data, out string path, out string? digest)
    {
        data = data.TrimStart('\uFEFF').TrimEnd('\0');
        int separator = data.LastIndexOf('\n');
        if (separator >= 0)
        {
            string suffix = data[(separator + 1)..].TrimEnd('\r');
            if (suffix.Length == 32 && suffix.All(Uri.IsHexDigit))
            {
                path = data[..separator].TrimEnd('\r');
                digest = suffix.ToLowerInvariant();
            }
            else
            {
                path = data.Split(['\r', '\n'], StringSplitOptions.None)[0];
                digest = null;
            }
        }
        else
        {
            path = data;
            digest = null;
        }

        if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Empty Unity asset pathname.");
        path = path.Trim();
        if (path.Contains('\r') || path.Contains('\n') || path.Contains('\0'))
            throw new InvalidDataException($"Unity asset pathname contains line breaks: {path}");
    }

    private static string ResolveAssetPath(string root, string assetPath)
    {
        string normalized = assetPath.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) || normalized.Contains(':'))
        {
            throw new InvalidDataException($"Invalid asset path: {assetPath}");
        }

        string[] segments = normalized.Split('/');
        if (segments.Length == 0 || segments[0] != "Assets" || segments.Any(x => x is "" or "." or ".."))
        {
            throw new InvalidDataException($"Invalid Unity asset path: {assetPath}");
        }

        string destination = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Asset path escapes output directory: {assetPath}");
        }

        return destination;
    }

    private sealed class PackageEntry(string guid)
    {
        public string Guid { get; } = guid;
        public Dictionary<string, byte[]> Streams { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}