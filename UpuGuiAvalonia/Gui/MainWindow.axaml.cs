using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Win32;
using Serilog;
using UpuGuiAvalonia.Models;
using UpuGuiAvalonia.Services;

namespace UpuGuiAvalonia.Gui;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly ILogger Logger = Log.ForContext<MainWindow>();
    
    private const string ShellRegistryPath = @"Software\Classes\*\shell\UpuGuiAvalonia";
    private const string ShellRegistryMetadataPath = @"Software\Classes\*\shell\UpuGuiAvaloniaMetadata";
    private string? _loadedPackage;
    private IReadOnlyList<ExtractedUnityPackageAsset> _assets = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        SelectAll = true;
        UpdateShellButtonText();
        AvaloniaLogSink.MessageLogged += OnMessageLogged;
        Closed += (_, _) =>
        {
            AvaloniaLogSink.MessageLogged -= OnMessageLogged;
            _assets = [];
            PackageIcon = null;
            _loadedPackage = null;
        };
    }

    public ObservableCollection<PackageNode> RootNodes { get; } = new();

    public string PackageHeader
    {
        get;
        private set => SetField(ref field, value);
    } = "Unitypackage File";

    private byte[]? PackageIcon
    {
        get;
        set => SetField(ref field, value);
    }

    public bool SelectAll
    {
        get;
        set
        {
            if (!SetField(ref field, value)) return;
            foreach (PackageNode node in RootNodes) node.IsChecked = value;
        }
    }

    public bool ExportMeta
    {
        get;
        set => SetField(ref field, value);
    }

    public bool IsBusy
    {
        get;
        private set
        {
            if (!SetField(ref field, value)) return;
            OnPropertyChanged(nameof(CanUnpack));
        }
    }

    public bool HasPackage
    {
        get;
        private set
        {
            if (!SetField(ref field, value)) return;
            OnPropertyChanged(nameof(CanUnpack));
        }
    }

    public bool CanUnpack => HasPackage && !IsBusy;
    public new event PropertyChangedEventHandler? PropertyChanged;

    private async void OnSelectInputFileClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Unity package",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Unitypackage Files") { Patterns = ["*.unitypackage"] }]
        });
        if (files.Count == 0) return;
        string? path = files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) await OpenPackageAsync(path);
    }

    private async Task OpenPackageAsync(string filePathName)
    {
        string fullPath = Path.GetFullPath(filePathName);
        if (string.Equals(fullPath, _loadedPackage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(fullPath))
        {
            Logger.Error("Unity package not found: {PackagePath}", fullPath);
            return;
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".unitypackage", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Error("The input file is not a Unity package: {PackagePath}", fullPath);
            return;
        }

        IsBusy = true;
        HasPackage = false;
        _loadedPackage = fullPath;
        PackageHeader = Path.GetFileName(fullPath);
        RootNodes.Clear();
        _assets = [];
        PackageIcon = null;
        try
        {
            Logger.Information("Opening Unity package {PackagePath}", fullPath);
            UnityPackageExtractionResult extraction =
                await UnityPackageExtractor.ExtractAsync(fullPath, CancellationToken.None);
            _assets = extraction.Assets;
            PackageIcon = extraction.PackageIcon;
            foreach (PackageNode node in BuildTree(_assets)) RootNodes.Add(node);
            HasPackage = true;
            SelectAll = true;
            Logger.Information("Package loaded into memory. Assets: {AssetCount}; Icon: {HasIcon}",
                _assets.Count(x => !x.IsFolder), PackageIcon is { Length: > 0 });
            Logger.Information("Select the folders and files to export, then choose Unpack now");
        }
        catch (Exception ex)
        {
            _loadedPackage = null;
            _assets = [];
            PackageIcon = null;
            RootNodes.Clear();
            HasPackage = false;
            Logger.Error(ex, "Unable to open Unity package {PackagePath}", fullPath);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IReadOnlyList<PackageNode> BuildTree(IReadOnlyList<ExtractedUnityPackageAsset> assets)
    {
        var root = new PackageNode("Assets", "Assets", true, IconCatalog.Folder);
        var directoryLookup =
            new Dictionary<string, PackageNode>(StringComparer.OrdinalIgnoreCase) { ["Assets"] = root };
        foreach (ExtractedUnityPackageAsset asset in assets.OrderBy(x => x.AssetPath, StringComparer.OrdinalIgnoreCase))
        {
            string[] segments = asset.AssetPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            PackageNode parent = root;
            string currentPath = "Assets";
            int directoryEnd = asset.IsFolder ? segments.Length : segments.Length - 1;
            for (int i = 1; i < directoryEnd; i++)
            {
                currentPath += "/" + segments[i];
                if (!directoryLookup.TryGetValue(currentPath, out PackageNode? directory))
                {
                    directory = new PackageNode(segments[i], currentPath, true, IconCatalog.Folder);
                    directoryLookup.Add(currentPath, directory);
                    parent.Children.Add(directory);
                }

                parent = directory;
            }

            if (asset.IsFolder)
            {
                if (segments.Length > 1)
                {
                    string folderPath = string.Join('/', segments);
                    if (!directoryLookup.ContainsKey(folderPath))
                    {
                        var folder = new PackageNode(segments[^1], folderPath, true, IconCatalog.Folder, metaContent: asset.MetaContent);
                        directoryLookup.Add(folderPath, folder);
                        parent.Children.Add(folder);
                    }
                }

                continue;
            }

            string fileName = segments[^1];
            var fileNode = new PackageNode(fileName, string.Join('/', segments), isDirectory: false,
                IconCatalog.ForExtension(Path.GetExtension(fileName)), asset.AssetContent, asset.MetaContent);
            parent.Children.Add(fileNode);
        }

        return [root];
    }

    private async void OnUnpackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!HasPackage || IsBusy) return;
        IReadOnlyList<IStorageFolder> folders =
            await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select output folder", AllowMultiple = false
            });
        if (folders.Count == 0) return;
        string? outputDirectory = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputDirectory)) return;
        await ExportSelectedAsync(outputDirectory);
    }

    private async Task ExportSelectedAsync(string outputDirectory)
    {
        IsBusy = true;
        try
        {
            string outputRoot = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputRoot);
            List<PackageNode> nodes = [.. EnumerateNodes(RootNodes)];
            List<PackageNode> selectedDirectories = [.. nodes.Where(x => x is { IsDirectory: true, IsChecked: true })];
            List<PackageNode> selectedFiles = nodes
                .Where(x => x is { IsDirectory: false, IsChecked: true, AssetContent: not null }).ToList();
            int copied = 0;
            int metadataCopied = 0;
            foreach (PackageNode directory in selectedDirectories)
            {
                string destination = ResolveOutputPath(outputRoot, directory.AssetPath);
                Directory.CreateDirectory(destination);
                if (!ExportMeta || directory.MetaContent is not { } directoryMeta) continue;
                await File.WriteAllBytesAsync(destination + ".meta", directoryMeta);
                metadataCopied++;
            }

            foreach (PackageNode file in selectedFiles)
            {
                if (file.AssetContent is null) continue;
                string destination = ResolveOutputPath(outputRoot, file.AssetPath);
                string? parent = Path.GetDirectoryName(destination);
                if (parent is not null) Directory.CreateDirectory(parent);
                await File.WriteAllBytesAsync(destination, file.AssetContent);
                if (ExportMeta && file.MetaContent is { } metaContent)
                {
                    await File.WriteAllBytesAsync(destination + ".meta", metaContent);
                    metadataCopied++;
                }

                copied++;
                Logger.Information("Exported {AssetPath}", file.AssetPath);
            }

            if (PackageIcon is { Length: > 0 } icon)
            {
                string iconPath = Path.Combine(outputRoot, ".icon.png");
                await File.WriteAllBytesAsync(iconPath, icon);
                Logger.Information("Exported package icon to {IconPath}", iconPath);
            }

            Logger.Information("Export completed. Files: {FileCount}; metadata: {MetadataCount}; output: {OutputDirectory}", copied,
                metadataCopied, outputRoot);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unity package export failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string ResolveOutputPath(string outputRoot, string assetPath)
    {
        string relative = assetPath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
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

    private static IEnumerable<PackageNode> EnumerateNodes(IEnumerable<PackageNode> nodes)
    {
        foreach (PackageNode node in nodes)
        {
            yield return node;
            foreach (PackageNode child in EnumerateNodes(node.Children)) yield return child;
        }
    }

    private void OnExpandClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        foreach (PackageNode node in EnumerateNodes(RootNodes)) node.IsExpanded = true;
        ExpandContainers(TreeViewContents, RootNodes, true);
    }

    private void OnCollapseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        foreach (PackageNode node in EnumerateNodes(RootNodes)) node.IsExpanded = false;
        ExpandContainers(TreeViewContents, RootNodes, false);
    }

    private static void ExpandContainers(ItemsControl owner, IEnumerable<PackageNode> nodes, bool expanded)
    {
        foreach (PackageNode node in nodes)
        {
            if (owner.ContainerFromItem(node) is not TreeViewItem item) continue;
            item.IsExpanded = expanded;
            if (expanded) item.UpdateLayout();
            ExpandContainers(item, node.Children, expanded);
        }
    }

    private void OnRegisterUnregisterClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!OperatingSystem.IsWindows())
        {
            Logger.Warning("Explorer context menu integration is only available on Windows");
            return;
        }

        try
        {
            if (IsShellHandlerRegistered())
            {
                Registry.CurrentUser.DeleteSubKeyTree(ShellRegistryPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(ShellRegistryMetadataPath, throwOnMissingSubKey: false);
                Logger.Information("Explorer context menu handlers unregistered");
            }
            else
            {
                string? executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable) ||
                    !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning("Context menu registration requires the packaged .exe application");
                    return;
                }
                
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ShellRegistryPath))
                {
                    key.SetValue(string.Empty, "Extract with UpuGuiAvalonia");
                    key.SetValue("Icon", executable);
                    using RegistryKey command = key.CreateSubKey("command");
                    command.SetValue(string.Empty, $"\"{executable}\" --extract \"%1\"");
                }
                
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ShellRegistryMetadataPath))
                {
                    key.SetValue(string.Empty, "Extract with UpuGuiAvalonia (with metadata)");
                    key.SetValue("Icon", executable);
                    using RegistryKey command = key.CreateSubKey("command");
                    command.SetValue(string.Empty, $"\"{executable}\" --extract-metadata \"%1\"");
                }

                Logger.Information("Explorer context menu handlers registered");
            }

            UpdateShellButtonText();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unable to update the Explorer context menu handlers");
        }
    }

    private static bool IsShellHandlerRegistered()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ShellRegistryPath);
        return key is not null;
    }

    private void UpdateShellButtonText()
    {
        BtnRegisterUnregister.Content = IsShellHandlerRegistered()
            ? "Unregister Explorer Context Menu Handler"
            : "Register Context Menu Handler";
    }

    private void OnAboutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/Myrkie/UpuGuiAvalonia") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unable to open the About page");
        }
    }

    private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private void OnMessageLogged(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ConsoleTextBox.Text += message;
            ConsoleTextBox.CaretIndex = ConsoleTextBox.Text?.Length ?? 0;
        });
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}