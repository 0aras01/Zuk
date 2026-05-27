# Architektura Rozwiązania - Mandelbrot Explorer

Aplikacja oparta na architekturze separującej logikę matematyczną i obliczeniową od warstwy prezentacji, bazująca na wzorcu MVVM (Model-View-ViewModel).

## 1. Struktura Projektów
- **Mandelbrot.Core**: Biblioteka `.NET 10` zawierająca całą niezależną od platformy logikę (modele, interfejsy obliczeń, algorytmy zastępcze, zarządzanie historią).
- **Mandelbrot.Compute**: Moduł oparty na `.NET 10` integrujący bibliotekę `ILGPU`. Kompiluje uniwersalne jądra obliczeniowe (Kernels) i deleguje renderowanie fraktala prosto do układów karty graficznej (np. **Intel Arc Pro B50** po OpenCL, lub NVIDIA po CUDA).
- **Mandelbrot.UI**: Aplikacja `AvaloniaUI` korzystająca z `.NET 10` pełniąca rolę warstwy widoku i interakcji z użytkownikiem. Wykorzystuje `CommunityToolkit.Mvvm`. Dzięki pełnemu wdrożeniu wzorca MVVM, wymiana AvaloniaUI na WinUI 3 dla platformy Windows ogranicza się do przepisania warstwy widoku (`MainWindow.xaml`) bez konieczności ruszania logiki `MainViewModel.cs`.
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
- Aplikacja rysuje bezpośrednio do bufora wyjściowego wygenerowanego jako `WriteableBitmap` przechwytując szybkie tablice jednowymiarowe z karty graficznej.
- Po interakcji użytkownika i obrysowaniu prostokąta, system mapuje współrzędne pikselowe z Avalonia na odpowiedni przedział z Płaszczyzny Zespolonej i puszcza sygnał przez `ZoomService`.
