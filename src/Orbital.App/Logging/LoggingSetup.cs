// src/Orbital.App/Logging/LoggingSetup.cs
namespace Orbital.App.Logging;

using System;
using Microsoft.Extensions.Logging;

public static class LoggingSetup
{
    public static (ILoggerFactory factory, FileLoggerProvider fileProvider) Create()
    {
        var level = ParseLevel(Environment.GetEnvironmentVariable("ORBITAL_LOG_LEVEL"));
        var fileProvider = new FileLoggerProvider(level);
        var factory = LoggerFactory.Create(b => b.SetMinimumLevel(level).AddProvider(fileProvider));
        return (factory, fileProvider);
    }

    private static LogLevel ParseLevel(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "trace" => LogLevel.Trace,
        "debug" => LogLevel.Debug,
        "info" or "information" => LogLevel.Information,
        "warn" or "warning" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Information,
    };
}
