namespace UnityPackageToolsNet.Models;

public sealed record ExtractedUnityPackageAsset(
    string Guid,
    string AssetPath,
    string? AssetFile,
    string? MetaFile,
    string? PreviewFile,
    string? Md4Digest,
    bool IsFolder
);