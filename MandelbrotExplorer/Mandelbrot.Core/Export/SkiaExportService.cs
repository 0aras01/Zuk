using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using SkiaSharp;

namespace Mandelbrot.Core.Export;

public class SkiaExportService : IFileExportService
{
    public async Task ExportImageAsync(byte[] pixelData, int width, int height, string filePath)
    {
        await Task.Run(() =>
        {
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info);

            GCHandle pinnedArray = GCHandle.Alloc(pixelData, GCHandleType.Pinned);
            try
            {
                IntPtr pointer = pinnedArray.AddrOfPinnedObject();
                using var bitmap = new SKBitmap();
                bitmap.InstallPixels(info, pointer, info.RowBytes);
                surface.Canvas.DrawBitmap(bitmap, 0, 0);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(filePath);
                data.SaveTo(stream);
            }
            finally
            {
                pinnedArray.Free();
            }
        });
    }
}
