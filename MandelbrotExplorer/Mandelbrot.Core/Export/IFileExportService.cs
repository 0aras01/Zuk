using System.Threading.Tasks;

namespace Mandelbrot.Core.Export;

public interface IFileExportService
{
    Task ExportImageAsync(byte[] pixelData, int width, int height, string filePath);
}
