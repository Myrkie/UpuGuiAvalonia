
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using UpuGuiAvalonia.Cli;
using UpuGuiAvalonia.Gui;

namespace UpuGuiAvalonia;

internal static partial class Program
{
    private static ILogger Logger => Log.ForContext(typeof(Program));
    private const string ConfigRoot = "UpuGuiAvalonia.conf";
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, ConfigRoot, "Logs", $"log-{DateTime.Now:M-d-yyyy-HH-mm}.txt");

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void FreeConsole();

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            if (OperatingSystem.IsWindows())
                FreeConsole();

            ConfigureGuiLogging(); 

            try
            {
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        ConfigureCliLogging();

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            var root = PackageOptions.CreateCommand();

            return await root.Parse(args).InvokeAsync();
        }
        catch (OperationCanceledException ex)
        {
            Logger.Error(ex, "Extraction cancelled");
            return 130;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unhandled exception");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void ConfigureGuiLogging()
    {
        var levelSwitch = new LoggingLevelSwitch
        {
#if DEBUG
    MinimumLevel = LogEventLevel.Debug
#else
            MinimumLevel = LogEventLevel.Information
#endif
        };
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.Sink(new AvaloniaLogSink())
            .WriteTo.File(
                path: LogPath,
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 4,
                shared: true)
            .CreateLogger();
    }

    private static void ConfigureCliLogging()
    {
        var levelSwitch = new LoggingLevelSwitch
        {
#if DEBUG
    MinimumLevel = LogEventLevel.Debug
#else
            MinimumLevel = LogEventLevel.Information
#endif
        };
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.Console(
                outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                theme: AnsiConsoleTheme.Code)
            .WriteTo.File(
                path: LogPath,
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                retainedFileCountLimit: 4,
                shared: true)
            .CreateLogger();
    }

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
