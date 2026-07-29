using System;
using System.IO;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using Fractal.Core.Models;
using Fractal.Core.Services;

namespace Fractal.Tests.Core;

public class DoubleDoubleJsonConverterTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1E+300, 1E+150)] // Very large numbers
    [InlineData(-1E+300, -1E+150)] // Very large negative numbers
    [InlineData(1E-300, 1E-320)] // Very small numbers
    [InlineData(-1E-300, -1E-320)] // Very small negative numbers
    public void Write_ShouldCorrectlySerializeDoubleDouble(double hi, double lo)
    {
        // Arrange
        var converter = new DoubleDoubleJsonConverter();
        var options = new JsonSerializerOptions();
        var doubleDouble = new DoubleDouble(hi, lo);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        // Act
        converter.Write(writer, doubleDouble, options);
        writer.Flush();

        // Assert
        string json = Encoding.UTF8.GetString(stream.ToArray());

        // Use JsonSerializer to parse the generated JSON and verify it matches what we expect
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.ValueKind.Should().Be(JsonValueKind.Object);
        root.GetProperty("Hi").GetDouble().Should().Be(hi);
        root.GetProperty("Lo").GetDouble().Should().Be(lo);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 0.0)]
    [InlineData(double.NegativeInfinity, 0.0)]
    [InlineData(double.NaN, 0.0)]
    public void Write_WithInfinityOrNaN_ShouldThrowArgumentException(double hi, double lo)
    {
        // Arrange
        var converter = new DoubleDoubleJsonConverter();
        var options = new JsonSerializerOptions();
        var doubleDouble = new DoubleDouble(hi, lo);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        // Act & Assert
        Action act = () => converter.Write(writer, doubleDouble, options);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*.NET number values such as positive and negative infinity cannot be written as valid JSON*");
    }

    [Fact]
    public void Write_WithNullWriter_ShouldThrowNullReferenceException()
    {
        // Arrange
        var converter = new DoubleDoubleJsonConverter();
        var options = new JsonSerializerOptions();
        var doubleDouble = new DoubleDouble(0.0, 0.0);

        // Act & Assert
        // NullReferenceException occurs because the code does `writer.WriteStartObject()` directly without checking for null.
        // This test ensures we're capturing the current behavior when null is passed.
        Action act = () => converter.Write(null!, doubleDouble, options);
        act.Should().Throw<NullReferenceException>();
    }
}
