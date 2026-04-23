// src/Orbital.App/Logging/FileLogger.cs
namespace Orbital.App.Logging;

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

internal sealed class FileLogger : ILogger
{
    private readonly string category;
    private readonly FileLoggerProvider provider;

    public FileLogger(string category, FileLoggerProvider provider)
    {
        this.category = category;
        this.provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= provider.MinimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        var message = formatter(state, exception);
        var sb = new StringBuilder();
        sb.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", System.Globalization.CultureInfo.InvariantCulture));
        sb.Append(" [").Append(LevelTag(logLevel)).Append("] ");
        sb.Append(category).Append(": ").Append(message);
        if (exception is not null) sb.Append(Environment.NewLine).Append(exception);
        sb.Append(Environment.NewLine);
        provider.Write(sb.ToString());
    }

    private static string LevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace       => "TRC",
        LogLevel.Debug       => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning     => "WRN",
        LogLevel.Error       => "ERR",
        LogLevel.Critical    => "CRT",
        _                    => "---",
    };
}
