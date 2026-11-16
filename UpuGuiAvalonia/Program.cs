using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using UpuGuiAvalonia.Cli;
using UpuGuiAvalonia.Gui;
using Constants = UpuGuiAvalonia.Utils.Constants;

namespace UpuGuiAvalonia;

internal static partial class Program
{
    private static ILogger Logger => Log.ForContext(typeof(Program));
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, Constants.ConfigRoot, "Logs", $"log-{DateTime.Now:M-d-yyyy-HH-mm}.txt");
    private const string OutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial void FreeConsole();

    [LibraryImport("kernel32.dll")]
    private static partial nint GetConsoleWindow();

    [STAThread]
    private static int Main(string[] args)
    {
        bool hasConsole = OperatingSystem.IsWindows() && GetConsoleWindow() != nint.Zero;

        ConfigureLogging(logToConsole: args.Length != 0 || hasConsole, logToGui: args.Length == 0);
        try
        {
            if (args.Length != 0) return RunCli(args);
            if (OperatingSystem.IsWindows() && !hasConsole) FreeConsole();
            if (OperatingSystem.IsLinux()) Utils.Utilities.ConfigureNativeLibraries();
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (OperationCanceledException ex)
        {
            Logger.Error(ex, "Operation cancelled");
            return 130;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unhandled exception");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static int RunCli(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        var root = PackageOptions.CreateCommand();
        return root.Parse(args).Invoke();
    }

    private static void ConfigureLogging(bool logToConsole, bool logToGui)
    {
        var levelSwitch = new LoggingLevelSwitch
        {
#if DEBUG
            MinimumLevel = LogEventLevel.Debug
#else
            MinimumLevel = LogEventLevel.Information
#endif
        };
        var configuration = new LoggerConfiguration().MinimumLevel.ControlledBy(levelSwitch).WriteTo.File(path: LogPath,
            outputTemplate: OutputTemplate, retainedFileCountLimit: 4, shared: true);
        if (logToConsole)
        {
            configuration.WriteTo.Console(outputTemplate: OutputTemplate, theme: AnsiConsoleTheme.Code);
        }

        if (logToGui)
        {
            configuration.WriteTo.Sink(new AvaloniaLogSink());
        }

        Log.Logger = configuration.CreateLogger();
        Logger.Information("Application starting");
    }

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}