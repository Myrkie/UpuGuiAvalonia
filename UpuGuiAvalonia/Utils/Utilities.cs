using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Microsoft.Win32;
using Serilog;

namespace UpuGuiAvalonia.Utils
{
    public static class Utilities
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(Utilities));
        private static string NativeLibraryDirectory => Path.Combine(AppContext.BaseDirectory, Constants.ConfigRoot, "Assemblies");
        private const string ShellRegistryPath = @"Software\Classes\*\shell\UpuGuiAvalonia";
        private const string ShellRegistryMetadataPath = @"Software\Classes\*\shell\UpuGuiAvaloniaMetadata";

        public static void RegistryContext(bool? register = null, Button? btnRegisterUnregister = null)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Warning("Explorer context menu integration is only available on Windows");
                return;
            }

            try
            {
                bool shouldRegister = register ?? !IsShellHandlerRegistered();
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

                if (btnRegisterUnregister != null)
                {
                    UpdateShellButtonText(btnRegisterUnregister);
                }
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

        public static void ConfigureNativeLibraries()
        {
            if (!OperatingSystem.IsLinux()) return;
            if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                throw new PlatformNotSupportedException("The currently configured native libraries support Linux x64 only.");
            }

            Directory.CreateDirectory(NativeLibraryDirectory);
            Logger.Information("Native library directory: {Directory}", NativeLibraryDirectory);

            ExtractNativeLibrary("UpuGuiAvalonia.runtimes.linux-x64.native.libHarfBuzzSharp.so", "libHarfBuzzSharp.so");
            ExtractNativeLibrary("UpuGuiAvalonia.runtimes.linux-x64.native.libSkiaSharp.so", "libSkiaSharp.so");
            string harfBuzzPath = Path.Combine(NativeLibraryDirectory, "libHarfBuzzSharp.so");
            string skiaPath = Path.Combine(NativeLibraryDirectory, "libSkiaSharp.so");

            if (!File.Exists(harfBuzzPath))
            {
                throw new FileNotFoundException("HarfBuzzSharp native library was not extracted.", harfBuzzPath);
            }

            if (!File.Exists(skiaPath))
            {
                throw new FileNotFoundException("SkiaSharp native library was not extracted.", skiaPath);
            }

            Logger.Information("HarfBuzzSharp native library found: {Path}", harfBuzzPath);
            Logger.Information("SkiaSharp native library found: {Path}", skiaPath);

            RegisterHarfBuzzResolver();
            RegisterSkiaResolver();
            Logger.Information("Native library resolvers configured successfully");
        }

        private static void RegisterHarfBuzzResolver()
        {
            Assembly harfBuzzAssembly = typeof(HarfBuzzSharp.Face).Assembly;
            NativeLibrary.SetDllImportResolver(harfBuzzAssembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName is not ("HarfBuzzSharp" or "libHarfBuzzSharp"))
                {
                    return IntPtr.Zero;
                }

                string path = Path.Combine(NativeLibraryDirectory, "libHarfBuzzSharp.so");
                if (!File.Exists(path))
                {
                    Logger.Error("HarfBuzzSharp native library not found: {Path}", path);
                    return IntPtr.Zero;
                }

                Logger.Information("Resolving HarfBuzzSharp native library: {Path}", path);
                return NativeLibrary.Load(path);
            });
            Logger.Information("HarfBuzzSharp DllImportResolver registered for {Assembly}", harfBuzzAssembly.GetName().Name);
        }

        private static void RegisterSkiaResolver()
        {
            Assembly skiaAssembly = typeof(SkiaSharp.SKBitmap).Assembly;
            NativeLibrary.SetDllImportResolver(skiaAssembly, (libraryName, assembly, searchPath) =>
            {
                if (libraryName is not ("SkiaSharp" or "libSkiaSharp"))
                {
                    return IntPtr.Zero;
                }

                string path = Path.Combine(NativeLibraryDirectory, "libSkiaSharp.so");
                if (!File.Exists(path))
                {
                    Logger.Error("SkiaSharp native library not found: {Path}", path);
                    return IntPtr.Zero;
                }

                Logger.Information("Resolving SkiaSharp native library: {Path}", path);
                return NativeLibrary.Load(path);
            });
            Logger.Information("SkiaSharp DllImportResolver registered for {Assembly}", skiaAssembly.GetName().Name);
        }

        private static void ExtractNativeLibrary(string resourceName, string fileName)
        {
            string outputPath = Path.Combine(NativeLibraryDirectory, fileName);

            if (File.Exists(outputPath))
            {
                Logger.Information("Native library already extracted: {Path}", outputPath);
                return;
            }

            Logger.Information("Extracting embedded native library: {Resource}", resourceName);
            using Stream resource = typeof(Utilities).Assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException($"Embedded native library not found: {resourceName}");
            string temporaryPath = outputPath + ".tmp";
            try
            {
                using (FileStream output = File.Create(temporaryPath))
                {
                    resource.CopyTo(output);
                }

                File.Move(temporaryPath, outputPath, overwrite: true);
#pragma warning disable CA1416
                File.SetUnixFileMode(outputPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
#pragma warning restore CA1416
                Logger.Information("Successfully extracted native library: {Path}", outputPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public static WriteableBitmap CreatePreviewBitmap(byte[] imageBytes)
        {
            using var stream = new MemoryStream(imageBytes);
            using var source = new Bitmap(stream);

            var preview = new WriteableBitmap(source.PixelSize, source.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul);

            using var framebuffer = preview.Lock();
            source.CopyPixels(framebuffer);

            int width = framebuffer.Size.Width;
            int height = framebuffer.Size.Height;
            int stride = framebuffer.RowBytes;

            byte[] pixels = new byte[stride * height];
            Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);

            bool[] visited = new bool[width * height];
            Queue<int> queue = new();

            const byte whiteThreshold = 230;

            for (int x = 0; x < width; x++)
            {
                EnqueueIfBackground(x, 0);
                EnqueueIfBackground(x, height - 1);
            }

            for (int y = 0; y < height; y++)
            {
                EnqueueIfBackground(0, y);
                EnqueueIfBackground(width - 1, y);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();

                int x = index % width;
                int y = index / width;

                int offset = y * stride + x * 4;

                pixels[offset + 3] = 0;

                EnqueueIfBackground(x - 1, y);
                EnqueueIfBackground(x + 1, y);
                EnqueueIfBackground(x, y - 1);
                EnqueueIfBackground(x, y + 1);
            }

            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);

            return preview;

            void EnqueueIfBackground(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                    return;

                int index = y * width + x;

                if (visited[index])
                    return;

                int offset = y * stride + x * 4;

                byte blue = pixels[offset];
                byte green = pixels[offset + 1];
                byte red = pixels[offset + 2];
                byte alpha = pixels[offset + 3];

                if (alpha == 0 ||
                    red < whiteThreshold ||
                    green < whiteThreshold ||
                    blue < whiteThreshold)
                {
                    return;
                }

                visited[index] = true;
                queue.Enqueue(index);
            }
        }
    }
}