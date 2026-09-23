# ZPG – Semestrální práce

Semestrální práce z předmětu Základy počítačové grafiky (ZPG), FAV ZČU.

Jednoduchá 3D hra/vizualizace postavená na OpenGL (přes [OpenTK](https://opentk.net/)) v C# (.NET 8). Obsahuje procedurálně generovaný svět z textových map, kolizní systém, kameru s pohybem hráče, teleporty, dynamické osvětlení (baterku) a minimapu.

## Technologie

- .NET 8 (Windows Forms + OpenGL)
- [OpenTK](https://opentk.net/) 4.9
- Vlastní GLSL shadery (`src/Shaders`)

## Struktura projektu

```
src/
├── Source/     # C# zdrojové soubory (herní logika, renderování, kamera, svět...)
├── Shaders/    # GLSL vertex/fragment shadery
├── Assets/     # Textové definice map a 3D modely
├── semestral.csproj
└── semestral.sln
```

## Spuštění

```
cd src
dotnet run
```

## Ovládání

- `W` / `A` / `S` / `D` – pohyb
- Myš – rozhlížení
- `Mezerník` – skok
- `Esc` – ukončení
