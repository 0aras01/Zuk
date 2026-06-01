# Mandelbrot Explorer Premium

Wieloplatformowa aplikacja (Avalonia) oraz dedykowana wersja na Windows (WinUI 3) stworzona na potrzeby eksploracji fraktala ("Żuka") Mandelbrota.

Projekt stanowi pełnoprawny produkt, wdrażający generowanie obrazu przy użyciu akceleracji GPU za pomocą biblioteki **ILGPU**, ze wsparciem dla układów takich jak **Intel Arc Pro B50** poprzez OpenCL oraz inne układy akcelerowane (CUDA, DirectX).

## Nowe Funkcje Premium
- Błyskawiczne generowanie i renderowanie fraktala przy użyciu akceleracji **GPU** (ILGPU).
- Interaktywne zaznaczanie obszarów przybliżenia bezpośrednio myszą w okienku.
- **Eksport 4K**: Możliwość zapisywania aktualnego widoku w bardzo wysokiej rozdzielczości (4K) do pliku PNG, wprost na dysk twardy, za pomocą silnika SkiaSharp.
- **Motywy Kolorystyczne**: Wybierz jeden z wbudowanych schematów kolorystycznych: *Classic, Fire, Neon, Gold*.
- **Zakładki (Bookmarks)**: Zapisuj swoje ulubione koordynaty, by wracać do najciekawszych fragmentów Żuka w przyszłości.
- **Max Iterations Slider**: Dynamicznie zwiększaj stopień precyzji obliczeń, przydatne przy ekstremalnych powiększeniach.

## Wymagania
- .NET 10.0 SDK.

## Uruchamianie
Aby uruchomić wersję Cross-Platform (działającą na Mac/Linux/Windows):
```bash
dotnet run --project Mandelbrot.UI.Avalonia/Mandelbrot.UI.Avalonia.csproj
```

Aby uruchomić natywną wersję dla Windows 10/11 (WinUI 3):
```bash
dotnet run --project Mandelbrot.UI.WinUI/Mandelbrot.UI.WinUI.csproj
```

## Uruchamianie Testów
```bash
dotnet test MandelbrotExplorer.slnx
```
