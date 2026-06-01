# Architektura Rozwiązania - Mandelbrot Explorer Premium

Aplikacja oparta na architekturze separującej logikę matematyczną i obliczeniową od warstwy prezentacji, bazująca na wzorcu MVVM (Model-View-ViewModel).

## 1. Struktura Projektów
- **Mandelbrot.Core**: Biblioteka `.NET 10` zawierająca logikę, modele (Bookmark), interfejsy obliczeniowe (IFileExportService), oraz operacje eksportowania obrazów za pomocą silnika `SkiaSharp`.
- **Mandelbrot.Compute**: Moduł oparty na `.NET 10` i bibliotece `ILGPU`. Odpowiada za bezpośrednie wysyłanie skompilowanych jąder obliczeniowych do karty graficznej (np. **Intel Arc Pro B50** po OpenCL).
- **Mandelbrot.UI.Avalonia**: Wieloplatformowy frontend oparty na frameworku Avalonia.
- **Mandelbrot.UI.WinUI**: Natywny frontend dla platformy Windows korzystający z `WinUI 3` / `WindowsAppSDK`. Posiada natywny menedżer okien plików (`FileSavePicker`).
- **Mandelbrot.Tests**: Projekt testowy `xUnit`.

## 2. Architektura i Wzorce (C4 - Container/Component)
- Projekt ściśle korzysta z Dependency Injection. Główne usługi to `IFractalGenerator` (ILGPU), `IZoomService`, `IBookmarkService` (przechowywanie historii) oraz `IFileExportService`.
- Całość widoku (zarówno WinUI jak i Avalonia) łączy się z logiką przez bibliotekę `CommunityToolkit.Mvvm`.

## 3. Generowanie Plików i Integracja SkiaSharp
- Do zapisu widoków 4K zastosowano bibliotekę `SkiaSharp`. Pozwala to na uniknięcie problemów ze specyficznymi dla platformy bibliotekami takimi jak System.Drawing.
- Bajty obrazu generowane na GPU trafiają do wskaźnika systemowego (GCHandle) który następnie alokuje pamięć na `SKPixmap` i generuje szybki `SKImage` do pliku.
