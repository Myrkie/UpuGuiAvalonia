using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace UpuGuiAvalonia.Gui;

internal static class IconCatalog
{
    private const string ResourceRoot = "avares://UpuGuiAvalonia/Assets/Icons/";
    private static readonly Dictionary<string, IImage?> Cache = new(StringComparer.OrdinalIgnoreCase);
    public static IImage? Folder => Load("d_Folder Icon.png");

    public static IImage? ForExtension(string? extension)
    {
        string icon = (extension ?? string.Empty).TrimStart('.').ToLowerInvariant() switch
        {
            "anim" => "d_AnimationClip Icon.png",
            "dll" => "d_Assembly Icon.png",
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
        return Load(icon);
    }

    private static IImage? Load(string fileName)
    {
        if (Cache.TryGetValue(fileName, out IImage? cached)) return cached;
        try
        {
            var uri = new Uri(ResourceRoot + Uri.EscapeDataString(fileName));
            using Stream stream = AssetLoader.Open(uri);
            var bitmap = new Bitmap(stream);
            Cache[fileName] = bitmap;
            return bitmap;
        }
        catch
        {
            Cache[fileName] = null;
            return null;
        }
    }
}