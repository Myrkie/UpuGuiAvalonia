using System.CommandLine;
using Serilog;
using UnityPackageToolsNet.Services;

namespace UnityPackageToolsNet.Cli;

internal static class PackageOptions
{
    private static readonly ILogger Logger = Log.ForContext(typeof(PackageOptions));

    public static RootCommand CreateCommand()
    {
        var root = new RootCommand("Unity package extraction tool");
        var extractOption = new Option<string?>("--extract") { Description = "Unity package to extract" };
        var outputDirectoryArgument = new Argument<string?>("output-directory")
        {
            Description = "Optional parent directory for the unpacked package", Arity = ArgumentArity.ZeroOrOne
        };
        var extractMetadataOption = new Option<bool>("--extract-metadata")
        {
            Description = "Extract Unity .meta files"
        };
        root.Options.Add(extractOption);
        root.Options.Add(extractMetadataOption);
        root.Arguments.Add(outputDirectoryArgument);
        root.SetAction(async (parseResult, cancellationToken) =>
        {
            string? packagePath = parseResult.GetValue(extractOption);
            string? outputDirectory = parseResult.GetValue(outputDirectoryArgument);
            bool extractMetadata = parseResult.GetValue(extractMetadataOption);
            if (!string.IsNullOrWhiteSpace(packagePath))
                return await ExtractAsync(packagePath, outputDirectory, extractMetadata, cancellationToken);
            Logger.Error("No package specified. Use --extract to specify a Unity package");
            return 1;
        });
        return root;
    }

    private static async Task<int> ExtractAsync(string packagePath, string? outputDirectory, bool extractMetadata, CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath))
        {
            Logger.Error("Unity package not found: {PackagePath}", packagePath);
            return 1;
        }

        if (!string.Equals(Path.GetExtension(packagePath), ".unitypackage", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Error("The input file is not a Unity package: {PackagePath}", packagePath);
            return 1;
        }

        string fullPackagePath = Path.GetFullPath(packagePath);
        string packageDirectory = Path.GetDirectoryName(fullPackagePath)!;
        string packageName = Path.GetFileName(fullPackagePath);
        string outputParent = string.IsNullOrWhiteSpace(outputDirectory)
            ? packageDirectory
            : Path.GetFullPath(outputDirectory);
        string unpackedDirectory = Path.Combine(outputParent, packageName + "_unpacked");
        Logger.Information("Starting Unity package extraction");
        Logger.Information("Package: {PackagePath}", fullPackagePath);
        Logger.Information("Output: {OutputDirectory}", unpackedDirectory);
        Logger.Information("Extract metadata: {ExtractMetadata}", extractMetadata);
        try
        {
            var assets = await UnityPackageExtractor.ExtractAsync(
                fullPackagePath,
                unpackedDirectory,
                extractMetadata,
                cancellationToken);

            int files = assets.Count(x => !x.IsFolder);
            int folders = assets.Count(x => x.IsFolder);
            int metadata = assets.Count(x => x.MetaFile is not null);

            string iconPath = Path.Combine(unpackedDirectory, ".icon.png");
            bool iconExtracted = File.Exists(iconPath);

            Logger.Information("Unity package extraction completed");

            foreach (var asset in assets)
            {
                Logger.Information(
                    "Extracted {AssetType}: {AssetPath} | GUID: {Guid} | MD4: {Md4Digest}",
                    asset.IsFolder ? "Folder" : "Asset",
                    asset.AssetPath,
                    asset.Guid,
                    asset.Md4Digest ?? "None");
            }

            Logger.Information("Assets: {AssetCount}", files);
            Logger.Information("Folders: {FolderCount}", folders);
            Logger.Information("Metadata files: {MetadataCount}", metadata);

            if (iconExtracted)
                Logger.Information("Package icon extracted: {IconPath}", iconPath);
            else
                Logger.Information("Package does not contain an extractable icon");

            Logger.Information("Output directory: {OutputDirectory}", unpackedDirectory);

            return 0;
        }
        catch (OperationCanceledException)
        {
            Logger.Warning("Unity package extraction was cancelled");
            return 130;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unity package extraction failed");
            return 1;
        }
    }
}