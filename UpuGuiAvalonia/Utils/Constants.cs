using System.Reflection;

namespace UpuGuiAvalonia.Utils
{
    public abstract class Constants
    {
        public const string AppName = "UpuGuiAvalonia";
        public const string AppTitle = "Unitypackage Unpacker for Unity";
        public const string AppUrl = "https://github.com/Myrkie/UpuGuiAvalonia";
        public const string ConfigRoot = "UpuGuiAvalonia.conf";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version is { } version
                ? $"{version.Major}.{version.Minor}.{version.Build}" : "Unknown";
        
        public static readonly string AppTitleVersion = $"{AppTitle} - V{Version}";
    }
}