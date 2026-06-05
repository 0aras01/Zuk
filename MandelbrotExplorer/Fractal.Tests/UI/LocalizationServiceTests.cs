using System.Globalization;
using FluentAssertions;
using Fractal.UI.Services;
using Xunit;

namespace Fractal.Tests.UI;

public class LocalizationServiceTests
{
    [Fact]
    public void Indexer_ReturnsCorrectString_ForEnglish()
    {
        // Arrange
        var service = LocalizationService.Instance;
        service.CurrentCulture = new CultureInfo("en");

        // Act
        string appName = service["AppName"];
        string zoom = service["Zoom"];
        string playAnim = service["PlayAnimation"];

        // Assert
        appName.Should().Be("Fractal Explorer");
        zoom.Should().Be("Zoom:");
        playAnim.Should().Be("Auto Zoom");
    }

    [Fact]
    public void Indexer_ReturnsCorrectString_ForPolish()
    {
        // Arrange
        var service = LocalizationService.Instance;
        service.CurrentCulture = new CultureInfo("pl");

        // Act
        string appName = service["AppName"];
        string zoom = service["Zoom"];
        string playAnim = service["PlayAnimation"];

        // Assert
        appName.Should().Be("Eksplorator Fraktali");
        zoom.Should().Be("Przybliżenie:");
        playAnim.Should().Be("Auto-przybliżenie");
    }

    [Fact]
    public void Indexer_ReturnsKey_ForUnknownResource()
    {
        // Arrange
        var service = LocalizationService.Instance;

        // Act
        string val = service["UnknownKey_12345"];

        // Assert
        val.Should().Be("UnknownKey_12345");
    }
}
