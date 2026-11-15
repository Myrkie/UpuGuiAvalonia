using System.Text;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using UnityPackageToolsNet.Cli;

namespace UnityPackageToolsNet;

internal static class Program
{
    private static readonly ILogger Logger = Log.ForContext(typeof(Program));

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Log.Logger = new LoggerConfiguration().WriteTo
            .Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}",
                theme: AnsiConsoleTheme.Code).CreateLogger();
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            if (cancellation.IsCancellationRequested) return;
            Logger.Information("Cancellation requested...");
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var root = PackageOptions.CreateCommand();
            return await root.Parse(args).InvokeAsync(cancellationToken: cancellation.Token);
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
        }
    }
}