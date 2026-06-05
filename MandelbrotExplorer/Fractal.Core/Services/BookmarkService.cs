using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fractal.Core.Models;

namespace Fractal.Core.Services;

public class DoubleDoubleJsonConverter : JsonConverter<DoubleDouble>
{
    public override DoubleDouble Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        double hi = 0;
        double lo = 0;
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string? propName = reader.GetString();
                    reader.Read();
                    if (string.Equals(propName, "Hi", StringComparison.OrdinalIgnoreCase))
                    {
                        hi = reader.GetDouble();
                    }
                    else if (string.Equals(propName, "Lo", StringComparison.OrdinalIgnoreCase))
                    {
                        lo = reader.GetDouble();
                    }
                }
            }
        }
        return new DoubleDouble(hi, lo);
    }

    public override void Write(Utf8JsonWriter writer, DoubleDouble value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Hi", value.Hi);
        writer.WriteNumber("Lo", value.Lo);
        writer.WriteEndObject();
    }
}

public class BookmarkService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public BookmarkService()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FractalExplorer");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        _filePath = Path.Combine(folder, "bookmarks.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        _jsonOptions.Converters.Add(new DoubleDoubleJsonConverter());
    }

    public List<BookmarkEntry> LoadBookmarks()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = GetDefaultBookmarks();
            SaveBookmarks(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<BookmarkEntry>>(json, _jsonOptions);
            return list ?? GetDefaultBookmarks();
        }
        catch
        {
            return GetDefaultBookmarks();
        }
    }

    public void SaveBookmarks(List<BookmarkEntry> bookmarks)
    {
        try
        {
            string json = JsonSerializer.Serialize(bookmarks, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    private List<BookmarkEntry> GetDefaultBookmarks()
    {
        return new List<BookmarkEntry>
        {
            new()
            {
                Name = "Seahorse Valley",
                FractalType = FractalType.Mandelbrot,
                Plane = new ComplexPlane(-0.75, -0.7, 0.05, 0.10),
                Palette = PaletteType.Sunset,
                Iterations = 1500
            },
            new()
            {
                Name = "Elephant Valley",
                FractalType = FractalType.Mandelbrot,
                Plane = new ComplexPlane(0.25, 0.30, -0.05, 0.0),
                Palette = PaletteType.Ice,
                Iterations = 1000
            },
            new()
            {
                Name = "Triple Spiral Valley",
                FractalType = FractalType.Mandelbrot,
                Plane = new ComplexPlane(-0.09, -0.08, 0.65, 0.66),
                Palette = PaletteType.Rainbow,
                Iterations = 2000
            },
            new()
            {
                Name = "Julia Default",
                FractalType = FractalType.Julia,
                Plane = new ComplexPlane(-1.5, 1.5, -1.5, 1.5),
                Palette = PaletteType.Sunset,
                Iterations = 500,
                JuliaCReal = -0.7,
                JuliaCImag = 0.27015
            }
        };
    }
}
