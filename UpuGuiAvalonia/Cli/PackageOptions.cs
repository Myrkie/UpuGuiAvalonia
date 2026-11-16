using System.CommandLine;
using Serilog;
using UpuGuiAvalonia.Services;

namespace UpuGuiAvalonia.Cli;

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
        var extractMetadataOption = new Option<bool>("--extract-metadata") { Description = "Extract Unity .meta files" };
        
        var aboutOption = new Option<bool>("--about")
        {
            Description = "Show information about AIDungeon Downloader",
            DefaultValueFactory = _ => false
        };
        
        root.Options.Add(extractOption);
        root.Options.Add(extractMetadataOption);
        root.Options.Add(aboutOption);
        root.Arguments.Add(outputDirectoryArgument);
        root.SetAction(async (parseResult, cancellationToken) =>
        {
            string? packagePath = parseResult.GetValue(extractOption);
            string? outputDirectory = parseResult.GetValue(outputDirectoryArgument);
            bool extractMetadata = parseResult.GetValue(extractMetadataOption);
            
            if (parseResult.GetValue(aboutOption))
            {
                await AboutScreen.ShowAsync();
                return 0;
            }
            
            if (!string.IsNullOrWhiteSpace(packagePath))
            {
                return await ExtractAsync(packagePath, outputDirectory, extractMetadata, cancellationToken);
            }

            Logger.Error("No package specified. Use --extract to specify a Unity package");
            return 1;
        });
        return root;
    }

    private static async Task<int> ExtractAsync(string packagePath, string? outputDirectory, bool extractMetadata,
        CancellationToken cancellationToken)
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
            var extraction = await UnityPackageExtractor.ExtractAsync(fullPackagePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(unpackedDirectory);
            int files = 0;
            int metadata = 0;

            foreach (var asset in extraction.Assets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (asset.AssetContent is null)
                {
                    Logger.Warning("Skipping asset with no content: {AssetPath}", asset.AssetPath);
                    continue;
                }

                string destination = ResolveOutputPath(unpackedDirectory, asset.AssetPath);
                string? parent = Path.GetDirectoryName(destination);
                if (parent is not null) Directory.CreateDirectory(parent);
                await File.WriteAllBytesAsync(destination, asset.AssetContent, cancellationToken);

                files++;

                if (extractMetadata && asset.MetaContent is { } metaContent)
                {
                    await File.WriteAllBytesAsync(destination + ".meta", metaContent, cancellationToken);
                    metadata++;
                }

                Logger.Information("Extracted Asset: {AssetPath} | GUID: {Guid} | MD4: {Md4Digest}", asset.AssetPath, asset.Guid, asset.Md4Digest ?? "None");
            }
            
            bool iconExtracted = false;
            if (extraction.PackageIcon is { Length: > 0 } icon)
            {
                string iconPath = Path.Combine(unpackedDirectory, ".icon.png");
                await File.WriteAllBytesAsync(iconPath, icon, cancellationToken);
                iconExtracted = true;
                Logger.Information("Package icon extracted: {IconPath}", iconPath);
            }
            else
            {
                Logger.Information("Package does not contain an extractable icon");
            }

            Logger.Information("Unity package extraction completed");
            Logger.Information("Assets: {AssetCount}", files);
            Logger.Information("Metadata files: {MetadataCount}", metadata);
            Logger.Information("Package icon extracted: {IconExtracted}", iconExtracted);
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

    private static string ResolveOutputPath(string outputRoot, string assetPath)
    {
        string relative = assetPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string destination = Path.GetFullPath(Path.Combine(outputRoot, relative));
        string prefix = Path.TrimEndingDirectorySeparator(outputRoot) + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!destination.StartsWith(prefix, comparison) && !string.Equals(destination, outputRoot, comparison))
        {
            throw new InvalidDataException($"Asset path escapes output directory: {assetPath}");
        }

        return destination;
    }
}