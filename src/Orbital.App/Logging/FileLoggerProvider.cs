// src/Orbital.App/Logging/FileLoggerProvider.cs
namespace Orbital.App.Logging;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Orbital.Core.Persistence;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> loggers = new();
    private readonly StreamWriter writer;
    private readonly object writeLock = new();

    public LogLevel MinimumLevel { get; }

    public FileLoggerProvider(LogLevel minimumLevel)
    {
        MinimumLevel = minimumLevel;
        var dir = Path.Combine(AppPaths.DataDirectory, "logs");
        Directory.CreateDirectory(dir);

        // Rotation: keep only the most recent 10 log files (including the one
        // we're about to create).
        var existing = new DirectoryInfo(dir).GetFiles("*.log")
            .OrderByDescending(f => f.CreationTimeUtc).ToArray();
        foreach (var old in existing.Skip(9))
        {
            try { old.Delete(); } catch { /* best-effort */ }
        }

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var path = Path.Combine(dir, $"{stamp}.log");
        writer = new StreamWriter(path, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, c => new FileLogger(c, this));

    internal void Write(string line)
    {
        lock (writeLock)
        {
            writer.Write(line);
        }
    }

    public void Dispose()
    {
        lock (writeLock)
        {
            writer.Dispose();
        }
    }
}
