using Avalonia.Controls;
using Microsoft.Win32;
using Serilog;

namespace UpuGuiAvalonia.Utils
{
    public static class Utilities
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(Utilities));
        private const string ShellRegistryPath = @"Software\Classes\*\shell\UpuGuiAvalonia";
        private const string ShellRegistryMetadataPath = @"Software\Classes\*\shell\UpuGuiAvaloniaMetadata";

        public static void RegistryContext(Button btnRegisterUnregister)
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
                        key.SetValue(string.Empty, "Unpack here");
                        key.SetValue("Icon", executable);
                        using RegistryKey command = key.CreateSubKey("command");
                        command.SetValue(string.Empty, $"\"{executable}\" --extract \"%1\"");
                    }

                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(ShellRegistryMetadataPath))
                    {
                        key.SetValue(string.Empty, "Unpack here (with metadata)");
                        key.SetValue("Icon", executable);
                        using RegistryKey command = key.CreateSubKey("command");
                        command.SetValue(
                            string.Empty,
                            $"\"{executable}\" --extract \"%1\" --extract-metadata");
                    }

                    Logger.Information("Explorer context menu handlers registered");
                }

                UpdateShellButtonText(btnRegisterUnregister);
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

        public static void UpdateShellButtonText(Button btnRegisterUnregister)
        {
            btnRegisterUnregister.Content = IsShellHandlerRegistered()
                ? "Unregister Explorer Context Menu Handler"
                : "Register Context Menu Handler";
        }
    }
}