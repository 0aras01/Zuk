# Mandelbrot Explorer

Wieloplatformowa aplikacja stworzona na potrzeby eksploracji fraktala ("Żuka") Mandelbrota.
Aplikacja została zaprojektowana w środowisku Linux, dlatego wykorzystuje technologię `AvaloniaUI` na systemie `.NET 10` z uwzględnieniem zasad MVVM tak, aby proces przeportowania aplikacji na `WinUI 3` na środowiskach natywnych Windows był banalnie prosty.

Dodatkowo, projekt implementuje generowanie obrazu przy użyciu akceleracji GPU za pomocą biblioteki **ILGPU**, wspierającej m.in. karty graficzne **Intel Arc Pro B50** poprzez OpenCL oraz inne układy akcelerowane (CUDA, DirectX).

## Funkcje
- Błyskawiczne generowanie i renderowanie fraktala przy użyciu akceleracji **GPU** (ILGPU). W razie braku wsparcia, system płynnie przełącza się na CPU.
- Interaktywne zaznaczanie obszarów przybliżenia bezpośrednio myszą w okienku.
- Zapamiętywanie historii powiększeń (możliwość cofania poprzez "Zoom Out").
- Pełna asynchroniczność oraz zapobieganie zawieszaniu UI podczas długotrwałych obliczeń (CancellationTokens).

## Wymagania
- .NET 10.0 SDK lub nowszy.

## Uruchamianie aplikacji w środowisku Cross-Platform (Avalonia)
1. Przejdź do katalogu głównego projektu `MandelbrotExplorer`.
2. Uruchom polecenie:
```bash
dotnet run --project Mandelbrot.UI/Mandelbrot.UI.csproj
```

## Przeportowanie logiki na natywne WinUI 3 (Windows App SDK)
Wszelka logika w `Mandelbrot.Core`, moduł `Mandelbrot.Compute` (ILGPU) oraz `MainViewModel` w `Mandelbrot.UI` są w 100% obojętne wobec platformy UI (korzystają z powszechnego `CommunityToolkit.Mvvm`).
Aby zmienić Avalonię na WinUI 3:
1. Utwórz nowy projekt WinUI 3 - Blank App, Packaged (C#).
2. Przenieś katalogi `Core` i `Compute` oraz sam `MainViewModel.cs`.
3. Przepisz plik `MainWindow.axaml` na `MainWindow.xaml` podmieniając kontrolkę `Canvas` z Avalonia na `CanvasControl` z Win2D, oraz używając `SoftwareBitmapSource`. Komendy z ViewModelu mogą pozostać dokładnie w tej samej strukturze.
4. Zarejestruj klasy DI w pliku `App.xaml.cs` i zaciągnij gotowy projekt.

## Uruchamianie Testów
```bash
dotnet test MandelbrotExplorer.slnx
```
