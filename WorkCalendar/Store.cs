using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkCalendar;

public static class Store
{
    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkCalendar");

    public static string DataFile { get; } = Path.Combine(Dir, "data.json");

    private static readonly JsonSerializerOptions Opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppState Load()
    {
        try
        {
            if (File.Exists(DataFile))
            {
                var json = File.ReadAllText(DataFile);
                var s = JsonSerializer.Deserialize<AppState>(json, Opts);
                if (s != null)
                {
                    s.Days ??= new();
                    s.Settings ??= new();
                    return s;
                }
            }
        }
        catch
        {
            // повреждённый файл — начинаем с чистого состояния
        }
        return new AppState();
    }

    public static void Save(AppState state)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(state, Opts);
        File.WriteAllText(DataFile, json);
    }

    public static void Export(string path, AppState state)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(state, Opts));
    }

    public static AppState Import(string path)
    {
        var s = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path), Opts) ?? new AppState();
        s.Days ??= new();
        s.Settings ??= new();
        return s;
    }
}
