namespace UpuGuiAvalonia.Models;

public sealed record UnityPackageExtractionResult(IReadOnlyList<ExtractedUnityPackageAsset> Assets, byte[]? PackageIcon);