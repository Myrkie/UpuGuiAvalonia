
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;
using UpuGuiAvalonia.Cli;
using UpuGuiAvalonia.Gui;

namespace UpuGuiAvalonia;

internal static class Program
{
    private static ILogger Logger => Log.ForContext(typeof(Program));

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        // GUI mode
        if (args.Length == 0)
        {
            if (OperatingSystem.IsWindows())
                FreeConsole();

            ConfigureGuiLogging();

            try
            {
                return BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        // CLI mode
        ConfigureCliLogging();

        using var cancellation = new CancellationTokenSource();

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;

            if (cancellation.IsCancellationRequested)
                return;

            Logger.Information("Cancellation requested...");
            cancellation.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            var root = PackageOptions.CreateCommand();

            return await root.Parse(args)
                .InvokeAsync(cancellationToken: cancellation.Token);
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
            Console.CancelKeyPress -= cancelHandler;
            await Log.CloseAndFlushAsync();
        }
    }

    private static void ConfigureGuiLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new AvaloniaLogSink())
            .CreateLogger();
    }

    private static void ConfigureCliLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(
                outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                theme: AnsiConsoleTheme.Code)
            .CreateLogger();
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
