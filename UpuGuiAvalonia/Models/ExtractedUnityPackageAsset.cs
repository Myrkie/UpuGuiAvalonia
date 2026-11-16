namespace UpuGuiAvalonia.Models;

public sealed record ExtractedUnityPackageAsset(string Guid, string AssetPath, byte[]? AssetContent, byte[]? MetaContent, string? Md4Digest);