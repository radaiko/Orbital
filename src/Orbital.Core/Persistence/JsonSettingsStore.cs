// src/Orbital.Core/Persistence/JsonSettingsStore.cs
namespace Orbital.Core.Persistence;

using System.Text.Json;
using Orbital.Core.Models;

public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private readonly string filePath;
    private readonly JsonSerializerOptions options;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public JsonSettingsStore(string? filePath = null)
    {
        this.filePath = filePath ?? AppPaths.SettingsFile;
        options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        };
    }

    public void Dispose() => writeLock.Dispose();

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(filePath)) return new AppSettings();
        var json = await File.ReadAllTextAsync(filePath, ct);
        if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
        return JsonSerializer.Deserialize<AppSettings>(json, options) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await writeLock.WaitAsync(ct);
        try
        {
            var tmp = filePath + ".tmp";
            await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(fs, settings, options, ct);
                await fs.FlushAsync(ct);
                fs.Flush(flushToDisk: true);
            }
            File.Move(tmp, filePath, overwrite: true);
        }
        finally
        {
            writeLock.Release();
        }
    }
}
