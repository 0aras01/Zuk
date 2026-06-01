using System.Collections.Generic;
using Mandelbrot.Core.Models;

namespace Mandelbrot.Core.Services;

public class BookmarkService : IBookmarkService
{
    private readonly List<Bookmark> _bookmarks = new();

    public IReadOnlyList<Bookmark> GetBookmarks() => _bookmarks;

    public void AddBookmark(Bookmark bookmark)
    {
        _bookmarks.Add(bookmark);
    }

    public void RemoveBookmark(Bookmark bookmark)
    {
        _bookmarks.Remove(bookmark);
    }
}
