using Avalonia.Controls;
using Microsoft.Win32;
using Serilog;

namespace UpuGuiAvalonia.Utils
{
    public static class ContextMenuRegistry
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ContextMenuRegistry));
        private const string ShellRegistryPath = @"Software\Classes\SystemFileAssociations\.unitypackage\shell\UpuGuiAvalonia";
        private const string ShellRegistryMetadataPath = @"Software\Classes\SystemFileAssociations\.unitypackage\shell\UpuGuiAvaloniaMetadata";
        private const string NemoExtractFile = "upugui-extract.nemo_action";
        private const string NemoMetadataFile = "upugui-extract-metadata.nemo_action";

        private enum ContextPlatform
        {
            Windows,
            Linux,
            Unsupported
        }

        public static void RegistryContext(bool? register = null, Button? btnRegisterUnregister = null)
        {
            try
            {
                switch (GetCurrentPlatform())
                {
                    case ContextPlatform.Windows:
                        RegistryContextWindows(register);
                        break;
                    case ContextPlatform.Linux:
                        RegistryContextLinux(register);
                        break;
                    default:
                        Logger.Warning("Context menu integration is not supported on this OS");
                        return;
                }

                if (btnRegisterUnregister != null)
                {
                    UpdateShellButtonText(btnRegisterUnregister);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unable to update context menu handlers");
            }
        }

        public static void UpdateShellButtonText(Button btnRegisterUnregister)
        {
            btnRegisterUnregister.Content = IsShellHandlerRegistered()
                ? "Unregister Context Menu Handler"
                : "Register Context Menu Handler";
        }

        public static bool IsShellHandlerRegistered()
        {
            return GetCurrentPlatform() switch
            {
                ContextPlatform.Windows => IsWindowsShellHandlerRegistered(),
                ContextPlatform.Linux => IsLinuxShellHandlerRegistered(),
                _ => false
            };
        }

        private static ContextPlatform GetCurrentPlatform()
        {
            if (OperatingSystem.IsWindows()) return ContextPlatform.Windows;
            return OperatingSystem.IsLinux() ? ContextPlatform.Linux : ContextPlatform.Unsupported;
        }

        private static void RegistryContextWindows(bool? register)
        {
#pragma warning disable CA1416
            bool shouldRegister = register ?? !IsWindowsShellHandlerRegistered();
            if (shouldRegister)
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
                    command.SetValue(string.Empty, $"\"{executable}\" --extract \"%1\" --extract-metadata");
                }

                Logger.Information("Explorer context menu handlers registered");
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree(ShellRegistryPath, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(ShellRegistryMetadataPath, throwOnMissingSubKey: false);
                Logger.Information("Explorer context menu handlers unregistered");
            }
#pragma warning restore CA1416
        }

        private static bool IsWindowsShellHandlerRegistered()
        {
#pragma warning disable CA1416
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"{ShellRegistryPath}\command");
            return key?.GetValue(string.Empty) is not null;
#pragma warning restore CA1416
        }

        private static void RegistryContextLinux(bool? register)
        {
            string? executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                Logger.Warning("Unable to determine application executable path");
                return;
            }

            string dataHome = GetXdgDataHome();
            string actionDirectory = Path.Combine(dataHome, "nemo", "actions");
            string extractFile = Path.Combine(actionDirectory, NemoExtractFile);
            string metadataFile = Path.Combine(actionDirectory, NemoMetadataFile);
            bool shouldRegister = register ?? !IsLinuxShellHandlerRegistered();
            if (shouldRegister)
            {
                Directory.CreateDirectory(actionDirectory);
                string quotedExecutable = QuoteDesktopArgument(executable);
                File.WriteAllText(extractFile, $"""
                                                [Nemo Action]
                                                Name=Extract here
                                                Comment=Extract Unity package
                                                Exec={quotedExecutable} --extract %F
                                                Icon-Name=archive-extract
                                                Selection=s
                                                Extensions=unitypackage;
                                                Quote=double
                                                Terminal=true
                                                """);
                File.WriteAllText(metadataFile, $"""
                                                 [Nemo Action]
                                                 Name=Extract here (with metadata)
                                                 Comment=Extract Unity package with metadata
                                                 Exec={quotedExecutable} --extract %F --extract-metadata
                                                 Icon-Name=archive-extract
                                                 Selection=s
                                                 Extensions=unitypackage;
                                                 Quote=double
                                                 Terminal=true
                                                 """);
                Logger.Information("Nemo context menu handlers registered at {Directory}", actionDirectory);
            }
            else
            {
                if (File.Exists(extractFile)) File.Delete(extractFile);
                if (File.Exists(metadataFile)) File.Delete(metadataFile);
                Logger.Information("Nemo context menu handlers unregistered");
            }
        }

        private static bool IsLinuxShellHandlerRegistered()
        {
            string actionDirectory = Path.Combine(GetXdgDataHome(), "nemo", "actions");
            return File.Exists(Path.Combine(actionDirectory, NemoExtractFile)) && File.Exists(Path.Combine(actionDirectory, NemoMetadataFile));
        }

        private static string GetXdgDataHome()
        {
            string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(dataHome) && Path.IsPathFullyQualified(dataHome))
            {
                return dataHome;
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        private static string QuoteDesktopArgument(string value)
        {
            return "\"" + value.Replace("\\", @"\\").Replace("\"", "\\\"") + "\"";
        }
    }
}