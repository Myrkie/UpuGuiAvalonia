using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace UpuGuiAvalonia.Gui;

public sealed class PackageNode(string name, string assetPath, bool isDirectory, IImage? icon, byte[]? assetContent = null, byte[]? metaContent = null) : INotifyPropertyChanged
{
    public string Name { get; } = name;
    public string AssetPath { get; } = assetPath;
    public bool IsDirectory { get; } = isDirectory;
    public IImage? Icon { get; } = icon;
    public byte[]? AssetContent { get; } = assetContent;
    public byte[]? MetaContent { get; } = metaContent;
    public ObservableCollection<PackageNode> Children { get; } = [];
    public event Action? CheckedChanged;
    private bool _isChecked = true;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetChecked(value);
    }

    public void SetChecked(bool value)
    {
        if (_isChecked != value)
        {
            _isChecked = value;
            OnPropertyChanged(nameof(IsChecked));
            CheckedChanged?.Invoke();
        }

        foreach (PackageNode child in Children)
            child.SetChecked(value);
    }

    public bool IsExpanded
    {
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}