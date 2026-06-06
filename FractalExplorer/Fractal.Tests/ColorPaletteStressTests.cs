using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Fractal.Core.Models;
using Fractal.UI.ViewModels;
using Fractal.Core.Services;
using Moq;
using Avalonia;
using Avalonia.Media.Imaging;

namespace Fractal.Tests
{
    public class ColorPaletteStressTests
    {
        [Fact]
        public void GradientPalette_Interpolation_Math_Correctness()
        {
            var p2 = new GradientPalette();
            p2.Stops.Add(new GradientStop(0.8, 100, 100, 100));
            p2.Stops.Add(new GradientStop(0.9, 200, 200, 200));
            
            p2.GetColor(0.1, 0, out byte r, out byte g, out byte b);
            
            Assert.Equal(100, r);
            Assert.Equal(100, g);
            Assert.Equal(100, b);
        }

        [Fact]
        public void GradientPalette_Math_ZeroRange_CorrectlyClamps()
        {
            var p3 = new GradientPalette();
            p3.Stops.Add(new GradientStop(0.5, 10, 10, 10));
            p3.Stops.Add(new GradientStop(0.5, 200, 200, 200));
            
            var ex = Record.Exception(() => p3.GetColor(0.5, 0, out byte r2, out byte g2, out byte b2));
            
            Assert.Null(ex);
            p3.GetColor(0.5, 0, out byte r, out byte g, out byte b);
            Assert.Equal(10, r);
            Assert.Equal(10, g);
            Assert.Equal(10, b);
        }

        public ColorPaletteStressTests()
        {
            if (Application.Current == null)
            {
                try
                {
                    AppBuilder.Configure<Fractal.UI.App>()
                        .UsePlatformDetect()
                        .SetupWithoutStarting();
                }
                catch { }
            }
        }

        [Fact]
        public async Task Concurrency_ColorCycling_RaceCondition_BufferLength()
        {
            var mainVm = new MainViewModel();
            var renderingVm = mainVm.Rendering;
            renderingVm.SelectedPalette = new GradientPalette();
            renderingVm.IsColorCycling = true;
            mainVm.OnSizeChanged(100, 100);
            await mainVm.GenerateFractalCommand.ExecuteAsync(null);

            var field = typeof(RenderingViewModel).GetField("_colorCyclingPixelBuffer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(renderingVm, new byte[100 * 100 * 4]);
            
            bool stop = false;
            
            var t1 = Task.Run(async () => 
            {
                while (!stop)
                {
                    renderingVm.ApplyColorCyclingFrame(100, 100);
                    await Task.Delay(1);
                }
            });
            
            var t2 = Task.Run(async () => 
            {
                int toggle = 0;
                while (!stop)
                {
                    if (toggle % 2 == 0)
                    {
                        mainVm.OnSizeChanged(10, 10);
                    }
                    else
                    {
                        mainVm.OnSizeChanged(100, 100);
                    }
                    
                    await mainVm.GenerateFractalCommand.ExecuteAsync(null);
                    toggle++;
                    if (toggle > 20) stop = true;
                }
            });
            
            var ex = await Record.ExceptionAsync(async () => await Task.WhenAll(t1, t2));
            Assert.Null(ex);
        }
    }
}
