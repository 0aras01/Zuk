using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Fractal.Core.Models;
using Fractal.Core.Services;
using Xunit;

namespace Fractal.Tests.Core.Services;

public class PaletteServiceTests
{
    [Fact]
    public void LoadPalettes_WhenFileDoesNotExist_ReturnsDefaultsAndSavesThem()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = new PaletteService(fileSystem);

        var defaultPath = fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FractalExplorer", "palettes.json");

        // Act
        var palettes = service.LoadPalettes();

        // Assert
        palettes.Should().NotBeNullOrEmpty();
        palettes.Should().OnlyContain(p => p.IsBuiltIn);

        fileSystem.FileExists(defaultPath).Should().BeTrue();
    }

    [Fact]
    public void LoadPalettes_WhenFileExistsWithCustomPalettes_CombinesDefaultsAndCustom()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var defaultPath = fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FractalExplorer", "palettes.json");

        var customPalettes = new List<GradientPalette>
        {
            new GradientPalette
            {
                Name = "Custom 1",
                IsBuiltIn = false,
                Stops = new List<GradientStop> { new GradientStop(0.0, 0, 0, 0) }
            }
        };

        var json = JsonSerializer.Serialize(customPalettes, new JsonSerializerOptions { WriteIndented = true });
        fileSystem.AddFile(defaultPath, new MockFileData(json));

        var service = new PaletteService(fileSystem);

        // Act
        var palettes = service.LoadPalettes();

        // Assert
        palettes.Should().NotBeNullOrEmpty();
        palettes.Should().ContainSingle(p => p.Name == "Custom 1" && !p.IsBuiltIn);
        palettes.Count(p => p.IsBuiltIn).Should().BeGreaterThan(0);
    }

    [Fact]
    public void LoadPalettes_WhenFileContainsCorruptedJson_ReturnsDefaults()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var defaultPath = fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FractalExplorer", "palettes.json");

        fileSystem.AddFile(defaultPath, new MockFileData("INVALID JSON {]"));

        var service = new PaletteService(fileSystem);

        // Act
        var palettes = service.LoadPalettes();

        // Assert
        palettes.Should().NotBeNullOrEmpty();
        palettes.Should().OnlyContain(p => p.IsBuiltIn);
    }

    [Fact]
    public void SavePalettes_OnlySavesCustomPalettes()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var service = new PaletteService(fileSystem);
        var defaultPath = fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FractalExplorer", "palettes.json");

        var customPalettes = new List<GradientPalette>
        {
            new GradientPalette { Name = "Custom", IsBuiltIn = false },
            new GradientPalette { Name = "BuiltIn", IsBuiltIn = true }
        };

        // Act
        service.SavePalettes(customPalettes);

        // Assert
        fileSystem.FileExists(defaultPath).Should().BeTrue();

        var json = fileSystem.GetFile(defaultPath).TextContents;
        var savedPalettes = JsonSerializer.Deserialize<List<GradientPalette>>(json);

        savedPalettes.Should().NotBeNull();
        savedPalettes.Should().ContainSingle();
        savedPalettes![0].Name.Should().Be("Custom");
    }
}
