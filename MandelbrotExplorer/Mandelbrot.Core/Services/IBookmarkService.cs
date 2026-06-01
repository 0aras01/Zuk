using System.Collections.Generic;
using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

public interface IBookmarkService
{
    IReadOnlyList<Bookmark> GetBookmarks();
    void AddBookmark(Bookmark bookmark);
    void RemoveBookmark(Bookmark bookmark);
}
