using System.Reflection;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Serilog;

namespace UpuGuiAvalonia.Gui;

internal static class IconCatalog
{
    private static readonly ILogger Logger = Log.ForContext(typeof(IconCatalog));
    private static readonly Assembly Assembly = typeof(IconCatalog).Assembly;
    private const string ResourceRoot = "UpuGuiAvalonia.Icons.";
    private static readonly Dictionary<string, IImage?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Lock CacheLock = new();
    public static IImage? Folder => Load("d_Folder Icon.png");

    public static IImage? ForExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return Load("d__Help.png");
        string iconName = extension.Trim().TrimStart('.').ToLowerInvariant();
        string fileName = iconName switch
        {
            "anim" => "d_AnimationClip Icon.png",
            "asmdef" => "d_Assembly Icon.png",
            "cs" => "d_cs Script Icon.png",
            "prefab" => "d_PrefabModel Icon.png",
            "mat" => "d_Material Icon.png",
            "png" or "psd" or "bmp" or "exr" or "gif" or "hdr" or "iff" or "pict" or "tga" or "tiff" or "jpg" or "jpeg"
                => "d_Image Icon.png",
            "shader" => "d_Shader Icon.png",
            "asset" => "d_TextAsset Icon.png",
            "controller" => "d_AnimatorController Icon.png",
            "mask" => "d_AvatarMask Icon.png",
            "fbx" or "dae" or "3ds" or "skp" or "blend" or "obj" => "d_Mesh Icon.png",
            "unity" => "d_SceneAsset Icon.png",
            "mp3" or "off" or "wav" or "aiff" or "aif" or "mod" or "it" or "s3m" or "xm" => "d_AudioSource Icon.png",
            _ => "d__Help.png"
        };
        return Load(fileName);
    }

    private static IImage? Load(string fileName)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(fileName, out IImage? cached)) return cached;
            string resourceName = ResourceRoot + fileName;
            try
            {
                Logger.Debug("Loading embedded icon: {ResourceName}", resourceName);
                using Stream? stream = Assembly.GetManifestResourceStream(resourceName);
                if (stream is null)
                {
                    Logger.Error("Embedded icon resource not found: {ResourceName}", resourceName);
                    Cache[fileName] = null;
                    return null;
                }

                var bitmap = new Bitmap(stream);
                Cache[fileName] = bitmap;
                Logger.Debug("Successfully loaded embedded icon: {FileName}", fileName);
                return bitmap;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load embedded icon: {ResourceName}", resourceName);
                Cache[fileName] = null;
                return null;
            }
        }
    }
}