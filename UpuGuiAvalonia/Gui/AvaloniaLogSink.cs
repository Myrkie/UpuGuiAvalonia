using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Threading;
using Serilog.Core;
using Serilog.Events;

namespace UpuGuiAvalonia.Gui;

internal sealed class AvaloniaLogSink : ILogEventSink, IDisposable
{
    public static event Action<string>? MessageLogged;

    private readonly ConcurrentQueue<string> _messages = new();
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public AvaloniaLogSink()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        _timer.Tick += FlushMessages;
        _timer.Start();
    }

    public void Emit(LogEvent logEvent)
    {
        if (_disposed)
            return;

        var message =
            $"[{logEvent.Timestamp:HH:mm:ss} {logEvent.Level.ToString()[..3].ToUpperInvariant()}] " +
            logEvent.RenderMessage(CultureInfo.InvariantCulture);

        if (logEvent.Exception is not null)
            message += Environment.NewLine + logEvent.Exception;

        _messages.Enqueue(message + Environment.NewLine);
    }

    private void FlushMessages(object? sender, EventArgs e)
    {
        if (_messages.IsEmpty)
            return;

        var batch = new System.Text.StringBuilder();

        while (_messages.TryDequeue(out var message))
            batch.Append(message);

        if (batch.Length > 0)
            MessageLogged?.Invoke(batch.ToString());
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= FlushMessages;

        var batch = new System.Text.StringBuilder();

        while (_messages.TryDequeue(out var message))
            batch.Append(message);

        if (batch.Length > 0)
            MessageLogged?.Invoke(batch.ToString());
    }
}