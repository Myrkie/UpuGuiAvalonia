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
            "cs" => "d_cs Script Icon.png",
            "js" => "d_Js Script Icon.png",
            "boo" => "d_boo Script Icon.png",
            "dll" => "dll Script Icon.png",
            "asmdef" => "d_Assembly Icon.png",
            "asmref" => "d_AssemblyDefinitionAsset Icon.png",
            "unity" => "d_SceneAsset Icon.png",
            "prefab" => "d_PrefabModel Icon.png",
            "mat" => "d_Material Icon.png",
            "shader" => "d_Shader Icon.png",
            "compute" => "d_ComputeShader Icon.png",
            "shadervariants" => "d_ShaderVariantCollection Icon.png",
            "guiskin" => "d_GUISkin Icon.png",
            "rendertexture" => "d_RenderTexture Icon.png",
            "cubemap" => "d_Cubemap Icon.png",
            "png" or "psd" or "bmp" or "exr" or "gif" or "hdr" or "iff" or "pict" or "tga" or "tiff" or "jpg" or "jpeg" => "d_Image Icon.png",
            "tif" or "webp" or "jxr" or "dds" or "ktx" or "ktx2" => "d_Texture2D Icon.png",
            "fbx" or "dae" or "3ds" or "skp" or "blend" or "obj" or "max" or "ma" or "mb" or "lxo" or "dxf" or "c4d" => "d_Mesh Icon.png",
            "anim" => "d_AnimationClip Icon.png",
            "controller" => "d_AnimatorController Icon.png",
            "overridecontroller" => "d_AnimatorOverrideController Icon.png",
            "mask" => "d_AvatarMask Icon.png",
            "avatar" => "d_Avatar Icon.png",
            "ht" => "HumanTemplate Icon.png",
            "mp3" or "ogg" or "wav" or "aiff" or "aif" or "mod" or "it" or "s3m" or "xm" => "d_AudioClip Icon.png",
            "mp4" or "mov" or "avi" or "asf" or "wmv" or "mpg" or "mpeg" => "VideoClip Icon.png",
            "ttf" or "otf" => "d_Font Icon.png",
            "lighting" => "d_LightingSettings Icon.png",
            "giparams" => "d_LightingDataAsset Icon.png",
            "lights" => "d_LightmapParameters Icon.png",
            "asset" => "d_TextAsset Icon.png",
            "bytes" or "txt" or "json" or "xml" or "csv" or "yaml" or "yml" or "md" or "html" or "htm" or "css" or "xml" or "fnt" => "d_TextAsset Icon.png",
            "spriteatlas" => "d_SpriteAtlas Icon.png", "spriteatlasv2" => "d_SpriteAtlas Icon.png",
            "tile" => "d_Tile Icon.png",
            "uxml" => "d_VisualTreeAsset Icon.png",
            "uss" => "d_StyleSheet Icon.png",
            "playable" => "d_PlayableDirector Icon.png",
            "vfx" => "d_VisualEffectAsset Icon.png",
            "terrainlayer" => "d_Terrain Icon.png",
            "navmesh" => "d_NavMeshData Icon.png",
            "physicmaterial" => "d_PhysicMaterial Icon.png",
            "physicsmaterial2d" => "d_PhysicsMaterial2D Icon.png",
            "preset" => "Preset Icon.png",

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