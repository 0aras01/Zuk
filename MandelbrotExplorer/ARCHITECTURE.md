# Architektura Rozwiązania - Mandelbrot Explorer (WinUI 3)

Aplikacja oparta na architekturze separującej logikę matematyczną i obliczeniową od warstwy prezentacji, bazująca na wzorcu MVVM (Model-View-ViewModel).

## 1. Struktura Projektów
- **Mandelbrot.Core**: Biblioteka `.NET 10` zawierająca całą niezależną od platformy logikę (modele, interfejsy obliczeń, algorytmy zastępcze, zarządzanie historią).
- **Mandelbrot.Compute**: Moduł oparty na `.NET 10` integrujący bibliotekę `ILGPU`. Kompiluje uniwersalne jądra obliczeniowe (Kernels) i deleguje renderowanie fraktala prosto do układów karty graficznej (np. **Intel Arc Pro B50** po OpenCL, lub NVIDIA po CUDA).
- **Mandelbrot.UI**: Natywna aplikacja `WinUI 3` wykorzystująca `Microsoft.WindowsAppSDK`. Implementuje interfejs okienkowy poprzez XAML z użyciem biblioteki `CommunityToolkit.Mvvm`.
- **Mandelbrot.Tests**: Projekt testowy `xUnit` testujący Core oraz logikę ViewModel.

## 2. Architektura i Wzorce (C4 - Container/Component)
- **MVVM (Model-View-ViewModel)**: Widok komunikuje się z logiką tylko poprzez wiązanie danych i komendy (np. `MainViewModel`).
- **Dependency Injection (DI)**: Wykorzystanie kontenera `Microsoft.Extensions.DependencyInjection` do wstrzykiwania `IFractalGenerator` (`ILGPUFractalGenerator`) oraz `IZoomService` bezpośrednio do ViewModeli.
- **Command Pattern**: Akcje użytkownika wdrożono używając `[RelayCommand]` co skutkuje asynchronicznym wdrożeniem zapytań obliczeniowych poprzez wzorzec AsyncRelayCommand.

## 3. Komponenty Core i Compute
- **IFractalGenerator**: Wzorzec interfejsu zapewniający jednolity most obliczeniowy.
- **ILGPUFractalGenerator (w module Compute)**: Prawdziwy demon prędkości, przekazujący równanie zbieżności iteracyjnej do instrukcji SIMD po stronie GPU.
- **IZoomService (ZoomService)**: Moduł pełniący rolę historii powiększeń, operujący na strukturze typu `Stack` w celu magazynowania poprzednich `Viewport`.

## 4. Warstwa Prezentacji
- Aplikacja używa w klasie `MainViewModel` obiektu `SoftwareBitmapSource` pozwalającego wyświetlać na natywnej kontrolce graficznej Image przekazane struktury bajtowe pochodzące wprost z rdzenia Compute (wykorzystującego `ArrayView1D` z ILGPU).
