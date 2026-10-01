# ProceduralDungeonGenerator

← [Core](README.md) · Tip : `a6edd821` · #33 scaffolding stub

**Pas un moteur procédural.** Même `seed` → mêmes 2–4 salles alignées (6×6, pas i×8).

**Signature :** `generate(seed)` → `ProceduralDungeonLayout(Seed, RoomCount, Rooms)`

**Exemple :**
```csharp
var layout = ProceduralDungeonGenerator.Generate(seed: 42);
// même seed → mêmes 2–4 salles (stub, pas un moteur procédural)
// var layout = ProceduralDungeonGenerator.Generate(seed: 42);
```

**Honnêteté :** placeholder pour runs in-memory ; loot / couloirs / rencontres = hors scope.
