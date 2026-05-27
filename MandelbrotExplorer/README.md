# Mandelbrot Explorer (Windows Edition - WinUI 3)

Natywna aplikacja systemu Windows (WinUI 3 / Windows App SDK) stworzona na potrzeby eksploracji fraktala ("Żuka") Mandelbrota.

Projekt implementuje generowanie obrazu przy użyciu akceleracji GPU za pomocą biblioteki **ILGPU**, wspierającej m.in. karty graficzne **Intel Arc Pro B50** poprzez OpenCL oraz inne układy akcelerowane (CUDA, DirectX).

## Funkcje
- Błyskawiczne generowanie i renderowanie fraktala przy użyciu akceleracji **GPU** (ILGPU). W razie braku wsparcia, system płynnie przełącza się na CPU.
- Interaktywne zaznaczanie obszarów przybliżenia bezpośrednio myszą w okienku.
- Zapamiętywanie historii powiększeń (możliwość cofania poprzez "Zoom Out").
- Pełna asynchroniczność oraz zapobieganie zawieszaniu UI podczas długotrwałych obliczeń (CancellationTokens).
- Interfejs stworzony w technologii **WinUI 3** zgodnej z designem Windows 11.

## Wymagania
- System Windows 10/11.
- .NET 10.0 SDK.

## Uruchamianie
1. Przejdź do katalogu głównego projektu `MandelbrotExplorer`.
2. Uruchom polecenie:
```bash
dotnet run --project Mandelbrot.UI/Mandelbrot.UI.csproj
```

## Uruchamianie Testów
```bash
dotnet test MandelbrotExplorer.slnx
```
