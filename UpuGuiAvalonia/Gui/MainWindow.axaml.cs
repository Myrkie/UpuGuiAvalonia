using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Serilog;
using UpuGuiAvalonia.Models;
using UpuGuiAvalonia.Services;
using UpuGuiAvalonia.Utils;

namespace UpuGuiAvalonia.Gui;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly ILogger Logger = Log.ForContext<MainWindow>();

    private string? _loadedPackage;
    private bool _selectAll;
    private bool _updatingSelectAll;
    private IReadOnlyList<ExtractedUnityPackageAsset> _assets = [];
    public ObservableCollection<PackageNode> RootNodes { get; } = [];

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
        SelectAll = true;

        ContextMenuRegistry.UpdateShellButtonText(BtnRegisterUnregister);

        AvaloniaLogSink.MessageLogged += OnMessageLogged;

        Closed += (_, _) =>
        {
            AvaloniaLogSink.MessageLogged -= OnMessageLogged;
            _assets = [];
            PackageIcon = null;
            _loadedPackage = null;
        };
    }

    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once UnusedParameter.Local
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (IsBusy || !e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        bool hasValidFile = files?.Any(file =>
        {
            string path = file.Path.LocalPath;
            return !string.IsNullOrWhiteSpace(path) && string.Equals(Path.GetExtension(path), ".unitypackage",
                StringComparison.OrdinalIgnoreCase);
        }) == true;
        e.DragEffects = hasValidFile ? DragDropEffects.Copy : DragDropEffects.None;
    }

    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once UnusedParameter.Local
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (IsBusy || e.DataTransfer.TryGetFiles() is not { } files) return;
            string? packagePath = files.Select(file => file.Path.LocalPath).FirstOrDefault(path =>
                !string.IsNullOrWhiteSpace(path) && string.Equals(Path.GetExtension(path), ".unitypackage", StringComparison.OrdinalIgnoreCase));
            if (packagePath is null) return;
            await OpenPackageAsync(packagePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open dropped Unity package");
        }
    }

    public string PackageHeader
    {
        get;
        private set => SetField(ref field, value);
    } = "Unitypackage File";

    public Bitmap? PackageIcon
    {
        get;
        private set
        {
            if (!SetField(ref field, value)) return;
            OnPropertyChanged(nameof(HasPackageIcon));
        }
    }

    public bool HasPackageIcon => PackageIcon is not null;

    public bool SelectAll
    {
        get => _selectAll;
        set
        {
            if (!SetField(ref _selectAll, value)) return;
            if (_updatingSelectAll) return;

            _updatingSelectAll = true;
            try
            {
                foreach (PackageNode node in RootNodes)
                    node.SetChecked(value);
            }
            finally
            {
                _updatingSelectAll = false;
            }
        }
    }

    private void SubscribeToNode(PackageNode node)
    {
        node.CheckedChanged += OnNodeCheckedChanged;

        foreach (PackageNode child in node.Children)
            SubscribeToNode(child);
    }

    private void OnNodeCheckedChanged()
    {
        if (_updatingSelectAll) return;

        bool allChecked = EnumerateNodes(RootNodes).All(node => node.IsChecked);

        if (_selectAll == allChecked) return;

        SetField(ref _selectAll, allChecked, nameof(SelectAll));
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
        try
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
        catch (Exception ex)
        {
            Logger.Fatal(ex, "Failed selection");
        }
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
            UnityPackageExtractionResult extraction = await UnityPackageExtractor.ExtractAsync(fullPath, CancellationToken.None);
            
            _assets = extraction.Assets;
            if (extraction.PackageIcon is { Length: > 0 } iconBytes)
            {
                PackageIcon = Utilities.CreatePreviewBitmap(iconBytes); 
            }

            foreach (PackageNode node in BuildTree(_assets))
            {
                SubscribeToNode(node);
                RootNodes.Add(node);
            }

            HasPackage = true;
            SelectAll = true;
            Logger.Information("Package loaded into memory. Assets: {AssetCount}; Icon: {HasIcon}", _assets.Count(x => !x.IsFolder), HasPackageIcon);
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

    private static List<PackageNode> BuildTree(IReadOnlyList<ExtractedUnityPackageAsset> assets)
    {
        var roots = new List<PackageNode>();
        var rootLookup = new Dictionary<string, PackageNode>(StringComparer.OrdinalIgnoreCase);
        var directoryLookup = new Dictionary<string, PackageNode>(StringComparer.OrdinalIgnoreCase);
        foreach (ExtractedUnityPackageAsset asset in assets.OrderBy(x => x.AssetPath, StringComparer.OrdinalIgnoreCase))
        {
            string[] segments = asset.AssetPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2) continue;
            string hierarchy = segments[0];
            if (!string.Equals(hierarchy, "Assets", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(hierarchy, "Packages", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!rootLookup.TryGetValue(hierarchy, out PackageNode? root))
            {
                root = new PackageNode(hierarchy, hierarchy, true, IconCatalog.Folder);
                rootLookup.Add(hierarchy, root);
                directoryLookup.Add(hierarchy, root);
                roots.Add(root);
            }

            PackageNode parent = root;
            string currentPath = hierarchy;

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
                string folderPath = string.Join('/', segments);
                if (!directoryLookup.ContainsKey(folderPath))
                {
                    var folder = new PackageNode(segments[^1], folderPath, true, IconCatalog.Folder,
                        metaContent: asset.MetaContent);
                    directoryLookup.Add(folderPath, folder);
                    parent.Children.Add(folder);
                }

                continue;
            }

            string fileName = segments[^1];
            string filePath = string.Join('/', segments);
            var fileNode = new PackageNode(fileName, filePath, isDirectory: false, IconCatalog.ForExtension(Path.GetExtension(fileName)), asset.AssetContent, asset.MetaContent);
            parent.Children.Add(fileNode);
        }

        return roots;
    }

    private async void OnUnpackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
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
        catch (Exception ex)
        {
            Logger.Fatal(ex, "Failed unpack click");
        }
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
            List<PackageNode> selectedFiles = [.. nodes.Where(x => x is { IsDirectory: false, IsChecked: true, AssetContent: not null })];
            
            var exportedAssets = new List<string>();
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

                exportedAssets.Add(file.AssetPath);
            }

            Logger.Information("Exported assets:{NewLine}{AssetPaths:l}", Environment.NewLine, string.Join(Environment.NewLine, exportedAssets));

            Logger.Information("Export completed. Assets: {AssetCount}; metadata: {MetadataCount}; output: {OutputDirectory}", exportedAssets.Count, metadataCopied, outputRoot);
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
        ContextMenuRegistry.RegistryContext(btnRegisterUnregister: BtnRegisterUnregister);
    }

    private void OnAboutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Constants.AppUrl) { UseShellExecute = true });
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