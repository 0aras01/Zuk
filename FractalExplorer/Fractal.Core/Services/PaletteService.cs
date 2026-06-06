using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Fractal.Core.Models;

namespace Fractal.Core.Services;

public interface IPaletteService
{
    List<GradientPalette> LoadPalettes();
    void SavePalettes(List<GradientPalette> palettes);
}

public class PaletteService : IPaletteService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public PaletteService()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FractalExplorer");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        _filePath = Path.Combine(folder, "palettes.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public List<GradientPalette> LoadPalettes()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = GetDefaultPalettes();
            SavePalettes(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<GradientPalette>>(json, _jsonOptions) ?? new List<GradientPalette>();
            var defaults = GetDefaultPalettes();
            defaults.AddRange(list);
            return defaults;
        }
        catch
        {
            return GetDefaultPalettes();
        }
    }

    public void SavePalettes(List<GradientPalette> palettes)
    {
        try
        {
            // Only save custom palettes to avoid duplication if built-ins change
            var customPalettes = new List<GradientPalette>();
            foreach (var p in palettes)
            {
                if (!p.IsBuiltIn)
                {
                    customPalettes.Add(p);
                }
            }
            string json = JsonSerializer.Serialize(customPalettes, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore write errors
        }
    }

    private List<GradientPalette> GetDefaultPalettes()
    {
        return new List<GradientPalette>
        {
            new GradientPalette
            {
                Name = "Sunset",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 7, 100),
                    new GradientStop(0.16, 32, 107, 203),
                    new GradientStop(0.42, 237, 255, 255),
                    new GradientStop(0.64, 255, 170, 0),
                    new GradientStop(0.85, 0, 2, 0),
                    new GradientStop(1.0, 0, 7, 100)
                }
            },
            new GradientPalette
            {
                Name = "Ice",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.25, 0, 50, 150),
                    new GradientStop(0.5, 150, 200, 255),
                    new GradientStop(0.75, 255, 255, 255),
                    new GradientStop(1.0, 0, 0, 0)
                }
            },
            new GradientPalette
            {
                Name = "Rainbow",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 255, 0, 0),
                    new GradientStop(0.16, 255, 127, 0),
                    new GradientStop(0.33, 255, 255, 0),
                    new GradientStop(0.5, 0, 255, 0),
                    new GradientStop(0.66, 0, 0, 255),
                    new GradientStop(0.83, 75, 0, 130),
                    new GradientStop(1.0, 255, 0, 0)
                }
            },
            new GradientPalette
            {
                Name = "Forest",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.33, 0, 100, 0),
                    new GradientStop(0.66, 100, 200, 50),
                    new GradientStop(1.0, 0, 0, 0)
                }
            },
            new GradientPalette
            {
                Name = "Fire",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.33, 255, 0, 0),
                    new GradientStop(0.66, 255, 255, 0),
                    new GradientStop(1.0, 255, 255, 255)
                }
            },
            new GradientPalette
            {
                Name = "Ocean",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 50),
                    new GradientStop(0.5, 0, 100, 200),
                    new GradientStop(1.0, 0, 255, 255)
                }
            },
            new GradientPalette
            {
                Name = "Cyberpunk",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.33, 255, 0, 255),
                    new GradientStop(0.66, 0, 255, 255),
                    new GradientStop(1.0, 255, 255, 0)
                }
            },
            new GradientPalette
            {
                Name = "Monochrome",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(1.0, 255, 255, 255)
                }
            },
            new GradientPalette
            {
                Name = "Gold",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.5, 200, 150, 0),
                    new GradientStop(1.0, 255, 255, 200)
                }
            },
            new GradientPalette
            {
                Name = "Neon",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.33, 57, 255, 20),
                    new GradientStop(0.66, 255, 20, 147),
                    new GradientStop(1.0, 0, 255, 255)
                }
            },
            new GradientPalette
            {
                Name = "Vaporwave",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.25, 255, 113, 206),
                    new GradientStop(0.5, 1, 205, 254),
                    new GradientStop(0.75, 5, 255, 161),
                    new GradientStop(1.0, 185, 103, 255)
                }
            },
            new GradientPalette
            {
                Name = "Earth",
                IsBuiltIn = true,
                Stops = new List<GradientStop>
                {
                    new GradientStop(0.0, 0, 0, 0),
                    new GradientStop(0.33, 101, 67, 33),
                    new GradientStop(0.66, 34, 139, 34),
                    new GradientStop(1.0, 135, 206, 235)
                }
            }
        };
    }
}
