# HeroLoadoutFixer — Plan de implementación: núcleo puro y arreglo del bug de vanilla

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir el núcleo de decisión puro y testeado, y sobre él un mod instalable que arregle el bug de vanilla por el que los nobles que cumplen 18 años aparecen sin equipo.

**Architecture:** Todo lo que decide algo vive en `src/Core/` y **no referencia TaleWorlds**, de modo que compila dentro de un ejecutable de tests que corre sin el juego. Una capa fina en `src/Game/` adapta los tipos de Bannerlord a los tipos del núcleo. El mod se ensambla en `SubModule.cs` más un `CampaignBehaviorBase`.

**Tech Stack:** C# contra .NET Framework 4.x. Compilador Roslyn 4.14 de las Build Tools de Visual Studio 2022. No hay SDK de dotnet: no hay `dotnet build` ni `dotnet test`. Los tests son un ejecutable de consola cuyo código de salida es el número de fallos.

## Global Constraints

- Juego objetivo: **Mount & Blade II: Bannerlord v1.4.8** (build 119303).
- Compilador: `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe`.
- `/noconfig` **debe pasarse en la línea de comandos**, nunca dentro del fichero de respuesta: dentro se ignora con `warning CS2023`.
- `netstandard.dll` **debe referenciarse desde** `...\bin\Win64_Shipping_Client\mono\lib\mono\4.5\Facades\netstandard.dll`. Sin él todo tipo de TaleWorlds falla con `CS0012`.
- `src/Core/` **no puede referenciar ningún tipo de TaleWorlds**. Es la condición que hace testeable el núcleo.
- Ningún tipo serializable, ningún `SyncData` con contenido, listeners siempre con `AddNonSerializedListener`.
- El personaje del jugador (`Hero.MainHero`) nunca se toca.
- Nombres de eventos verificados en v1.4.8: `HeroComesOfAgeEvent`, `DailyTickHeroEvent`, `AfterSettlementEntered`, `HeroPrisonerTaken`. **No existen** `HeroComesOfAge` ni `OnHeroPrisonerTaken`.
- Idioma: código, identificadores y commits en inglés. Documentación de diseño en español.
- Raíz del proyecto: `E:\Games\Mods\Mis mods\HeroLoadoutFixer`.

---

## Estructura de ficheros

| Fichero | Responsabilidad |
|---|---|
| `src/Core/SkillKind.cs` | Enum de las siete habilidades relevantes |
| `src/Core/WeaponCategory.cs` | Enum de categorías de arma y munición |
| `src/Core/SkillProfile.cs` | Valores de habilidad y su ordenación |
| `src/Core/SlotSnapshot.cs` | Qué hay en cada ranura, en términos del núcleo |
| `src/Core/MountedRangedAvailability.cs` | Si arco y ballesta son usables a caballo |
| `src/Core/LoadoutTarget.cs` | El loadout objetivo y las ranuras planificadas |
| `src/Core/LoadoutPlanner.cs` | Arquetipo, reconciliación y relleno |
| `src/Core/TierCeiling.cs` | Techo de tier por mérito |
| `src/Core/BudgetMath.cs` | Aritmética de presupuesto, reserva y reparto |
| `tests/TestHarness.cs` | Aserciones y `Main` |
| `tests/*Tests.cs` | Un fichero por componente del núcleo |
| `build/refs.rsp` | Referencias comunes al juego |
| `build/build-tests.ps1` | Compila y ejecuta los tests |
| `build/build-mod.ps1` | Compila el DLL y lo despliega al juego |
| `src/Game/SlotMapping.cs` | `EquipSlot` del núcleo ↔ `EquipmentIndex` del juego |
| `src/Game/ItemClassifier.cs` | `ItemObject` → `WeaponCategory` |
| `src/Game/HeroAdapter.cs` | `Hero` → `SkillProfile`, `Equipment` → `SlotSnapshot` |
| `src/Game/ItemCatalog.cs` | Búsqueda de ítems en el catálogo global |
| `src/Game/GrantService.cs` | Concesión gratuita del loadout |
| `src/Game/ModLog.cs` | Registro a fichero |
| `src/Settings/ModSettings.cs` | Ajustes MCM |
| `src/SubModule.cs` | Punto de entrada |
| `src/HeroLoadoutBehavior.cs` | Suscripción a eventos y orquestación |
| `SubModule.xml` | Descriptor del módulo |

---

### Task 1: Andamiaje de compilación y arnés de tests

**Files:**
- Create: `build/refs.rsp`
- Create: `build/build-tests.ps1`
- Create: `tests/TestHarness.cs`
- Create: `src/Core/SkillKind.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `Check.Equal(int expected, int actual, string what)`, `Check.True(bool cond, string what)`, `Check.Failures` (int estático), y el enum `HeroLoadoutFixer.Core.SkillKind { OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing, Riding }`.

- [ ] **Step 1: Crear el fichero de referencias comunes**

Crear `build/refs.rsp`:

```
/r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll"
/r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll"
/r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll"
```

- [ ] **Step 2: Escribir el arnés de tests**

Crear `tests/TestHarness.cs`:

```csharp
using System;

namespace HeroLoadoutFixer.Tests
{
    public static class Check
    {
        public static int Failures;
        public static int Passes;

        public static void Equal(int expected, int actual, string what)
        {
            if (expected == actual) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected " + expected + ", got " + actual);
        }

        public static void Equal(string expected, string actual, string what)
        {
            if (expected == actual) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected '" + expected + "', got '" + actual + "'");
        }

        public static void True(bool condition, string what)
        {
            if (condition) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected true");
        }

        public static void False(bool condition, string what)
        {
            if (!condition) { Passes++; return; }
            Failures++;
            Console.WriteLine("FAIL " + what + ": expected false");
        }
    }

    public static class TestHarness
    {
        public static int Main()
        {
            SmokeTests.RunAll();

            Console.WriteLine();
            Console.WriteLine(Check.Passes + " passed, " + Check.Failures + " failed");
            return Check.Failures;
        }
    }

    public static class SmokeTests
    {
        public static void RunAll()
        {
            Check.Equal(3, (int)HeroLoadoutFixer.Core.SkillKind.Bow, "SkillKind.Bow ordinal");
        }
    }
}
```

- [ ] **Step 3: Ejecutar los tests y comprobar que fallan a compilar**

Crear `build/build-tests.ps1`:

```powershell
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc  = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$out  = Join-Path $root "build\out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$sources = @()
$sources += Get-ChildItem (Join-Path $root "src\Core") -Filter *.cs -ErrorAction SilentlyContinue
$sources += Get-ChildItem (Join-Path $root "tests")    -Filter *.cs -ErrorAction SilentlyContinue

$rsp = Join-Path $out "tests.rsp"
$lines = @("/nologo", "/target:exe", "/platform:x64", "/out:`"$out\CoreTests.exe`"")
$lines += Get-Content (Join-Path $root "build\refs.rsp")
foreach ($s in $sources) { $lines += "`"$($s.FullName)`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $csc /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 1 }

& "$out\CoreTests.exe"
exit $LASTEXITCODE
```

Ejecutar:

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `BUILD FAILED`, porque `SkillKind` no existe todavía.

- [ ] **Step 4: Crear el enum**

Crear `src/Core/SkillKind.cs`:

```csharp
namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// The seven skills the mod reasons about. The first six are weapon skills
    /// and feed the tier ceiling; Riding is used only for mount decisions.
    /// </summary>
    public enum SkillKind
    {
        OneHanded = 0,
        TwoHanded = 1,
        Polearm = 2,
        Bow = 3,
        Crossbow = 4,
        Throwing = 5,
        Riding = 6
    }
}
```

- [ ] **Step 5: Ejecutar los tests y comprobar que pasan**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `1 passed, 0 failed` y código de salida 0.

- [ ] **Step 6: Commit**

```bash
git add build tests src
git commit -m "build: add csc test harness and SkillKind enum"
```

---

### Task 2: SkillProfile

**Files:**
- Create: `src/Core/SkillProfile.cs`
- Create: `tests/SkillProfileTests.cs`
- Modify: `tests/TestHarness.cs` (añadir la llamada a `SkillProfileTests.RunAll()`)

**Interfaces:**
- Consumes: `SkillKind`.
- Produces:
  - `SkillProfile(int oneHanded, int twoHanded, int polearm, int bow, int crossbow, int throwing, int riding)`
  - `int Get(SkillKind kind)`
  - `int MaxCombatSkill` — máximo de las seis de arma, excluye Riding
  - `SkillKind[] CombatSkillsDescending()` — las seis de arma ordenadas de mayor a menor, desempate por orden del enum
  - `bool RidingInTopTwo()` — si Riding está entre las dos mejores de las siete

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/SkillProfileTests.cs`:

```csharp
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SkillProfileTests
    {
        public static void RunAll()
        {
            // oneHanded, twoHanded, polearm, bow, crossbow, throwing, riding
            SkillProfile p = new SkillProfile(120, 40, 200, 90, 10, 60, 150);

            Check.Equal(120, p.Get(SkillKind.OneHanded), "Get OneHanded");
            Check.Equal(150, p.Get(SkillKind.Riding), "Get Riding");
            Check.Equal(200, p.MaxCombatSkill, "MaxCombatSkill excludes Riding");

            SkillKind[] order = p.CombatSkillsDescending();
            Check.Equal(6, order.Length, "six weapon skills returned");
            Check.Equal((int)SkillKind.Polearm, (int)order[0], "highest is Polearm");
            Check.Equal((int)SkillKind.OneHanded, (int)order[1], "second is OneHanded");
            Check.Equal((int)SkillKind.Bow, (int)order[2], "third is Bow");
            Check.Equal((int)SkillKind.Throwing, (int)order[3], "fourth is Throwing");
            Check.Equal((int)SkillKind.TwoHanded, (int)order[4], "fifth is TwoHanded");
            Check.Equal((int)SkillKind.Crossbow, (int)order[5], "sixth is Crossbow");

            // Riding 150 is behind Polearm 200 only, so it is in the top two.
            Check.True(p.RidingInTopTwo(), "Riding in top two");

            SkillProfile footman = new SkillProfile(200, 180, 170, 20, 10, 160, 30);
            Check.False(footman.RidingInTopTwo(), "low Riding not in top two");

            // Ties break by enum order: OneHanded before TwoHanded.
            SkillProfile tied = new SkillProfile(100, 100, 0, 0, 0, 0, 0);
            SkillKind[] tiedOrder = tied.CombatSkillsDescending();
            Check.Equal((int)SkillKind.OneHanded, (int)tiedOrder[0], "tie breaks to OneHanded");
            Check.Equal((int)SkillKind.TwoHanded, (int)tiedOrder[1], "tie second is TwoHanded");
        }
    }
}
```

- [ ] **Step 2: Registrar la suite en el arnés**

En `tests/TestHarness.cs`, dentro de `Main`, añadir bajo `SmokeTests.RunAll();`:

```csharp
            SkillProfileTests.RunAll();
```

- [ ] **Step 3: Ejecutar y comprobar que falla**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `BUILD FAILED` con `error CS0246: The type or namespace name 'SkillProfile' could not be found`.

- [ ] **Step 4: Implementar**

Crear `src/Core/SkillProfile.cs`:

```csharp
using System;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Immutable snapshot of a hero's combat-relevant skills.
    /// Pure: never references game types.
    /// </summary>
    public sealed class SkillProfile
    {
        private const int WeaponSkillCount = 6;
        private readonly int[] _values;

        public SkillProfile(int oneHanded, int twoHanded, int polearm,
                            int bow, int crossbow, int throwing, int riding)
        {
            _values = new int[7];
            _values[(int)SkillKind.OneHanded] = oneHanded;
            _values[(int)SkillKind.TwoHanded] = twoHanded;
            _values[(int)SkillKind.Polearm] = polearm;
            _values[(int)SkillKind.Bow] = bow;
            _values[(int)SkillKind.Crossbow] = crossbow;
            _values[(int)SkillKind.Throwing] = throwing;
            _values[(int)SkillKind.Riding] = riding;
        }

        public int Get(SkillKind kind)
        {
            return _values[(int)kind];
        }

        /// <summary>Highest of the six weapon skills. Riding is excluded, matching DynamicLordGear.</summary>
        public int MaxCombatSkill
        {
            get
            {
                int best = 0;
                for (int i = 0; i < WeaponSkillCount; i++)
                {
                    if (_values[i] > best) best = _values[i];
                }
                return best;
            }
        }

        /// <summary>
        /// The six weapon skills, highest first. Equal values keep enum order,
        /// so the result is deterministic across runs.
        /// </summary>
        public SkillKind[] CombatSkillsDescending()
        {
            SkillKind[] order = new SkillKind[WeaponSkillCount];
            for (int i = 0; i < WeaponSkillCount; i++) order[i] = (SkillKind)i;

            // Insertion sort: stable, and six elements never justify anything cleverer.
            for (int i = 1; i < WeaponSkillCount; i++)
            {
                SkillKind current = order[i];
                int currentValue = _values[(int)current];
                int j = i - 1;
                while (j >= 0 && _values[(int)order[j]] < currentValue)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = current;
            }
            return order;
        }

        /// <summary>True when Riding ranks first or second among all seven skills.</summary>
        public bool RidingInTopTwo()
        {
            int riding = _values[(int)SkillKind.Riding];
            int strictlyBetter = 0;
            for (int i = 0; i < WeaponSkillCount; i++)
            {
                if (_values[i] > riding) strictlyBetter++;
            }
            return strictlyBetter <= 1;
        }
    }
}
```

- [ ] **Step 5: Ejecutar y comprobar que pasa**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `0 failed` y código de salida 0.

- [ ] **Step 6: Commit**

```bash
git add src/Core/SkillProfile.cs tests/SkillProfileTests.cs tests/TestHarness.cs
git commit -m "feat(core): add SkillProfile with deterministic skill ordering"
```

---

### Task 3: WeaponCategory y SlotSnapshot

**Files:**
- Create: `src/Core/WeaponCategory.cs`
- Create: `src/Core/SlotSnapshot.cs`
- Create: `tests/SlotSnapshotTests.cs`
- Modify: `tests/TestHarness.cs`

**Interfaces:**
- Consumes: nada del núcleo previo.
- Produces:
  - `enum WeaponCategory { None, OneHandedSword, TwoHandedSword, OneHandedAxe, TwoHandedAxe, Mace, Spear, Polearm, Bow, Crossbow, Throwing, Shield, Arrows, Bolts, Other }`
  - `static class CategoryRules` con `int SlotCost(WeaponCategory c)`, `bool IsMelee(WeaponCategory c)`, `bool IsRanged(WeaponCategory c)`, `bool IsAmmo(WeaponCategory c)`, `bool IsTwoHanded(WeaponCategory c)`, `WeaponCategory AmmoFor(WeaponCategory ranged)`
  - `SlotSnapshot(WeaponCategory[] weapons, bool hasMount, bool hasHarness, bool hasHelmet, bool hasBody, bool hasLegs, bool hasGloves, bool hasCape)` con `WeaponCategory WeaponAt(int index)`, `int EmptyWeaponSlots`, `bool Contains(WeaponCategory c)`, `bool HasMount`, `bool HasTwoHandedEquipped`

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/SlotSnapshotTests.cs`:

```csharp
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class SlotSnapshotTests
    {
        public static void RunAll()
        {
            Check.Equal(1, CategoryRules.SlotCost(WeaponCategory.OneHandedSword), "melee costs one slot");
            Check.Equal(1, CategoryRules.SlotCost(WeaponCategory.Throwing), "throwing costs one slot");
            Check.Equal(2, CategoryRules.SlotCost(WeaponCategory.Bow), "bow costs two slots");
            Check.Equal(2, CategoryRules.SlotCost(WeaponCategory.Crossbow), "crossbow costs two slots");
            Check.Equal(1, CategoryRules.SlotCost(WeaponCategory.Shield), "shield costs one slot");

            Check.True(CategoryRules.IsRanged(WeaponCategory.Bow), "bow is ranged");
            Check.False(CategoryRules.IsRanged(WeaponCategory.Throwing), "throwing is not ranged for slot purposes");
            Check.True(CategoryRules.IsAmmo(WeaponCategory.Arrows), "arrows are ammo");
            Check.True(CategoryRules.IsTwoHanded(WeaponCategory.TwoHandedAxe), "two-handed axe is two-handed");
            Check.False(CategoryRules.IsTwoHanded(WeaponCategory.Spear), "spear is not two-handed");

            Check.Equal((int)WeaponCategory.Arrows, (int)CategoryRules.AmmoFor(WeaponCategory.Bow), "bow takes arrows");
            Check.Equal((int)WeaponCategory.Bolts, (int)CategoryRules.AmmoFor(WeaponCategory.Crossbow), "crossbow takes bolts");

            // Shield + spear + one-hander, one free slot.
            SlotSnapshot s = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Spear, WeaponCategory.Shield, WeaponCategory.OneHandedSword, WeaponCategory.None },
                false, false, true, true, true, false, false);

            Check.Equal(1, s.EmptyWeaponSlots, "one empty weapon slot");
            Check.True(s.Contains(WeaponCategory.Shield), "contains shield");
            Check.False(s.Contains(WeaponCategory.Bow), "does not contain bow");
            Check.False(s.HasMount, "no mount");
            Check.False(s.HasTwoHandedEquipped, "no two-handed weapon equipped");
            Check.Equal((int)WeaponCategory.None, (int)s.WeaponAt(3), "fourth slot empty");

            SlotSnapshot naked = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, false, false, false, false, false);
            Check.Equal(4, naked.EmptyWeaponSlots, "naked hero has four empty slots");

            SlotSnapshot greatsword = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.TwoHandedSword, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                true, true, false, false, false, false, false);
            Check.True(greatsword.HasTwoHandedEquipped, "two-handed sword detected");
            Check.True(greatsword.HasMount, "mount detected");
        }
    }
}
```

- [ ] **Step 2: Registrar la suite**

En `tests/TestHarness.cs`, dentro de `Main`, añadir:

```csharp
            SlotSnapshotTests.RunAll();
```

- [ ] **Step 3: Ejecutar y comprobar que falla**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `BUILD FAILED` con `error CS0246` sobre `CategoryRules` y `SlotSnapshot`.

- [ ] **Step 4: Implementar el enum y las reglas**

Crear `src/Core/WeaponCategory.cs`:

```csharp
namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// What kind of thing occupies a weapon slot. Ammunition is split by kind
    /// because arrows cannot feed a crossbow.
    /// </summary>
    public enum WeaponCategory
    {
        None = 0,
        OneHandedSword,
        TwoHandedSword,
        OneHandedAxe,
        TwoHandedAxe,
        Mace,
        Spear,
        Polearm,
        Bow,
        Crossbow,
        Throwing,
        Shield,
        Arrows,
        Bolts,
        Other
    }

    /// <summary>Slot arithmetic and category predicates. Pure.</summary>
    public static class CategoryRules
    {
        /// <summary>
        /// How many of the four weapon slots a category consumes when planned.
        /// Bows and crossbows cost two because they need their ammunition alongside.
        /// </summary>
        public static int SlotCost(WeaponCategory category)
        {
            if (category == WeaponCategory.Bow || category == WeaponCategory.Crossbow) return 2;
            if (category == WeaponCategory.None) return 0;
            return 1;
        }

        /// <summary>True for weapons that need a separate ammunition slot.</summary>
        public static bool IsRanged(WeaponCategory category)
        {
            return category == WeaponCategory.Bow || category == WeaponCategory.Crossbow;
        }

        public static bool IsAmmo(WeaponCategory category)
        {
            return category == WeaponCategory.Arrows || category == WeaponCategory.Bolts;
        }

        public static bool IsMelee(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.OneHandedSword:
                case WeaponCategory.TwoHandedSword:
                case WeaponCategory.OneHandedAxe:
                case WeaponCategory.TwoHandedAxe:
                case WeaponCategory.Mace:
                case WeaponCategory.Spear:
                case WeaponCategory.Polearm:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True for weapons that cannot be used together with a shield.</summary>
        public static bool IsTwoHanded(WeaponCategory category)
        {
            return category == WeaponCategory.TwoHandedSword
                || category == WeaponCategory.TwoHandedAxe;
        }

        /// <summary>The ammunition a ranged weapon consumes, or None.</summary>
        public static WeaponCategory AmmoFor(WeaponCategory ranged)
        {
            if (ranged == WeaponCategory.Bow) return WeaponCategory.Arrows;
            if (ranged == WeaponCategory.Crossbow) return WeaponCategory.Bolts;
            return WeaponCategory.None;
        }
    }
}
```

- [ ] **Step 5: Implementar SlotSnapshot**

Crear `src/Core/SlotSnapshot.cs`:

```csharp
using System;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// What a hero currently has equipped, expressed only in core types.
    /// The four weapon slots mirror Bannerlord's Weapon0..Weapon3.
    /// </summary>
    public sealed class SlotSnapshot
    {
        public const int WeaponSlotCount = 4;
        private readonly WeaponCategory[] _weapons;

        public SlotSnapshot(WeaponCategory[] weapons,
                            bool hasMount, bool hasHarness,
                            bool hasHelmet, bool hasBody, bool hasLegs,
                            bool hasGloves, bool hasCape)
        {
            if (weapons == null || weapons.Length != WeaponSlotCount)
                throw new ArgumentException("weapons must have exactly four entries");

            _weapons = new WeaponCategory[WeaponSlotCount];
            Array.Copy(weapons, _weapons, WeaponSlotCount);

            HasMount = hasMount;
            HasHarness = hasHarness;
            HasHelmet = hasHelmet;
            HasBody = hasBody;
            HasLegs = hasLegs;
            HasGloves = hasGloves;
            HasCape = hasCape;
        }

        public bool HasMount { get; private set; }
        public bool HasHarness { get; private set; }
        public bool HasHelmet { get; private set; }
        public bool HasBody { get; private set; }
        public bool HasLegs { get; private set; }
        public bool HasGloves { get; private set; }
        public bool HasCape { get; private set; }

        public WeaponCategory WeaponAt(int index)
        {
            return _weapons[index];
        }

        public int EmptyWeaponSlots
        {
            get
            {
                int count = 0;
                for (int i = 0; i < WeaponSlotCount; i++)
                {
                    if (_weapons[i] == WeaponCategory.None) count++;
                }
                return count;
            }
        }

        public bool Contains(WeaponCategory category)
        {
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (_weapons[i] == category) return true;
            }
            return false;
        }

        /// <summary>True when any equipped weapon rules out using a shield.</summary>
        public bool HasTwoHandedEquipped
        {
            get
            {
                for (int i = 0; i < WeaponSlotCount; i++)
                {
                    if (CategoryRules.IsTwoHanded(_weapons[i])) return true;
                }
                return false;
            }
        }

        /// <summary>Indices of the empty weapon slots, lowest first.</summary>
        public int[] EmptySlotIndices()
        {
            int[] buffer = new int[WeaponSlotCount];
            int n = 0;
            for (int i = 0; i < WeaponSlotCount; i++)
            {
                if (_weapons[i] == WeaponCategory.None) buffer[n++] = i;
            }
            int[] result = new int[n];
            Array.Copy(buffer, result, n);
            return result;
        }
    }
}
```

- [ ] **Step 6: Ejecutar y comprobar que pasa**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `0 failed` y código de salida 0.

- [ ] **Step 7: Commit**

```bash
git add src/Core/WeaponCategory.cs src/Core/SlotSnapshot.cs tests/SlotSnapshotTests.cs tests/TestHarness.cs
git commit -m "feat(core): add WeaponCategory, CategoryRules and SlotSnapshot"
```

---

### Task 4: TierCeiling

**Files:**
- Create: `src/Core/TierCeiling.cs`
- Create: `tests/TierCeilingTests.cs`
- Modify: `tests/TestHarness.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `static int TierCeiling.Compute(int clanTier, int maxCombatSkill, float clanWeight, float skillWeight, int minimumTier)` devolviendo un entero de 1 a 6.

Fórmula, tomada de `DynamicLordGear.GearSelector.GearSelectionParams.CalculateTargetGearTier`:

```
tierSkill = min(6, maxCombatSkill / 40)
blend     = (clanTier * clanWeight + tierSkill * skillWeight) / (clanWeight + skillWeight)
result    = clamp(max(round(blend), minimumTier), 1, 6)
```

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/TierCeilingTests.cs`:

```csharp
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class TierCeilingTests
    {
        public static void RunAll()
        {
            // Defaults from the spec: clan weight 0.5, skill weight 1.0, minimum 1.
            const float ClanW = 0.5f;
            const float SkillW = 1.0f;
            const int Min = 1;

            // Clan tier 3, skill 240 -> tierSkill 6 -> (3*0.5 + 6*1)/1.5 = 5.0 -> 5
            Check.Equal(5, TierCeiling.Compute(3, 240, ClanW, SkillW, Min), "clan 3 skill 240");

            // Clan tier 6, skill 80 -> tierSkill 2 -> (6*0.5 + 2*1)/1.5 = 3.33 -> 3
            Check.Equal(3, TierCeiling.Compute(6, 80, ClanW, SkillW, Min), "clan 6 skill 80");

            // Median lord: clan 4, skill 210 -> tierSkill 5 -> (2 + 5)/1.5 = 4.67 -> 5
            Check.Equal(5, TierCeiling.Compute(4, 210, ClanW, SkillW, Min), "median lord");

            // Fresh eighteen-year-old: clan 3, skill 20 -> tierSkill 0 -> (1.5 + 0)/1.5 = 1 -> 1
            Check.Equal(1, TierCeiling.Compute(3, 20, ClanW, SkillW, Min), "rookie stays at one");

            // The minimum floor lifts a result that would otherwise be lower.
            Check.Equal(3, TierCeiling.Compute(0, 0, ClanW, SkillW, 3), "minimum floor applies");

            // Skill weight zero means clan tier alone decides.
            Check.Equal(6, TierCeiling.Compute(6, 0, 1.0f, 0.0f, Min), "clan-only weighting");

            // Clan weight zero means skill alone decides.
            Check.Equal(6, TierCeiling.Compute(0, 240, 0.0f, 1.0f, Min), "skill-only weighting");

            // Never exceeds six even with absurd skill.
            Check.Equal(6, TierCeiling.Compute(6, 1000, ClanW, SkillW, Min), "capped at six");

            // Never below one even with a zero minimum.
            Check.Equal(1, TierCeiling.Compute(0, 0, ClanW, SkillW, 0), "floored at one");

            // Both weights zero is degenerate input; fall back to the minimum.
            Check.Equal(2, TierCeiling.Compute(5, 240, 0.0f, 0.0f, 2), "zero weights fall back to minimum");
        }
    }
}
```

- [ ] **Step 2: Registrar la suite**

En `tests/TestHarness.cs`, dentro de `Main`, añadir:

```csharp
            TierCeilingTests.RunAll();
```

- [ ] **Step 3: Ejecutar y comprobar que falla**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `BUILD FAILED` con `error CS0246` sobre `TierCeiling`.

- [ ] **Step 4: Implementar**

Crear `src/Core/TierCeiling.cs`:

```csharp
using System;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// How good a hero's gear is allowed to get. Adapted from DynamicLordGear's
    /// CalculateTargetGearTier: a weighted blend of clan standing and personal
    /// skill, floored at a configurable minimum.
    /// </summary>
    public static class TierCeiling
    {
        public const int MinTier = 1;
        public const int MaxTier = 6;

        /// <summary>Skill points that buy one tier. DynamicLordGear uses 40.</summary>
        public const int SkillPerTier = 40;

        public static int Compute(int clanTier, int maxCombatSkill,
                                  float clanWeight, float skillWeight, int minimumTier)
        {
            if (clanTier < 0) clanTier = 0;
            if (maxCombatSkill < 0) maxCombatSkill = 0;
            if (clanWeight < 0f) clanWeight = 0f;
            if (skillWeight < 0f) skillWeight = 0f;

            int skillTier = maxCombatSkill / SkillPerTier;
            if (skillTier > MaxTier) skillTier = MaxTier;

            float totalWeight = clanWeight + skillWeight;

            int blended;
            if (totalWeight <= 0f)
            {
                // Degenerate configuration: no opinion, so the floor decides.
                blended = MinTier;
            }
            else
            {
                float weighted = (clanTier * clanWeight + skillTier * skillWeight) / totalWeight;
                blended = (int)Math.Round(weighted, MidpointRounding.AwayFromZero);
            }

            int result = blended;
            if (result < minimumTier) result = minimumTier;
            if (result < MinTier) result = MinTier;
            if (result > MaxTier) result = MaxTier;
            return result;
        }
    }
}
```

- [ ] **Step 5: Ejecutar y comprobar que pasa**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `0 failed` y código de salida 0.

- [ ] **Step 6: Commit**

```bash
git add src/Core/TierCeiling.cs tests/TierCeilingTests.cs tests/TestHarness.cs
git commit -m "feat(core): add TierCeiling blending clan standing and skill"
```

---

### Task 5: Arquetipo objetivo

**Files:**
- Create: `src/Core/MountedRangedAvailability.cs`
- Create: `src/Core/LoadoutTarget.cs`
- Create: `src/Core/LoadoutPlanner.cs` (solo `PlanTarget` en esta tarea)
- Create: `tests/ArchetypeTests.cs`
- Modify: `tests/TestHarness.cs`

**Interfaces:**
- Consumes: `SkillProfile`, `SlotSnapshot`, `WeaponCategory`, `CategoryRules`.
- Produces:
  - `struct MountedRangedAvailability { bool BowViable; bool CrossbowViable; }` con constructor `(bool bowViable, bool crossbowViable)` y `static MountedRangedAvailability All()`
  - `sealed class LoadoutTarget` con `List<WeaponCategory> Weapons` y `bool WantsMount`
  - `static LoadoutTarget LoadoutPlanner.PlanTarget(SkillProfile skills, SlotSnapshot current, MountedRangedAvailability availability, int dominanceMargin, bool cultureIsMounted)`

Reglas, de la sección 5.2 de la especificación. El sidearm de un arquero es el mayor entre OneHanded y TwoHanded; **Polearm nunca acompaña a un proyectil**.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/ArchetypeTests.cs`:

```csharp
using System.Collections.Generic;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class ArchetypeTests
    {
        private static SlotSnapshot Naked(bool mounted)
        {
            return new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                mounted, false, false, false, false, false, false);
        }

        private static int CountOf(LoadoutTarget t, WeaponCategory c)
        {
            int n = 0;
            foreach (WeaponCategory w in t.Weapons) { if (w == c) n++; }
            return n;
        }

        public static void RunAll()
        {
            MountedRangedAvailability all = MountedRangedAvailability.All();
            MountedRangedAvailability noCrossbow = new MountedRangedAvailability(true, false);

            // Dedicated foot archer with a one-handed sidearm: two ammo, no shield.
            SkillProfile dedicated = new SkillProfile(100, 20, 10, 200, 0, 0, 0);
            LoadoutTarget t1 = LoadoutPlanner.PlanTarget(dedicated, Naked(false), all, 30, false);
            Check.True(t1.Weapons.Contains(WeaponCategory.Bow), "dedicated archer takes a bow");
            Check.Equal(2, CountOf(t1, WeaponCategory.Arrows), "dedicated archer takes two quivers");
            Check.False(t1.Weapons.Contains(WeaponCategory.Shield), "dedicated archer takes no shield");
            Check.True(t1.Weapons.Contains(WeaponCategory.OneHandedSword), "sidearm is one-handed");

            // Soldier who also shoots: one ammo plus a shield.
            SkillProfile hybrid = new SkillProfile(180, 20, 10, 190, 0, 0, 0);
            LoadoutTarget t2 = LoadoutPlanner.PlanTarget(hybrid, Naked(false), all, 30, false);
            Check.Equal(1, CountOf(t2, WeaponCategory.Arrows), "hybrid takes one quiver");
            Check.True(t2.Weapons.Contains(WeaponCategory.Shield), "hybrid takes a shield");

            // Two-handed sidearm drops the shield and restores the second quiver.
            SkillProfile twoHandedArcher = new SkillProfile(20, 190, 10, 200, 0, 0, 0);
            LoadoutTarget t3 = LoadoutPlanner.PlanTarget(twoHandedArcher, Naked(false), all, 30, false);
            Check.Equal(2, CountOf(t3, WeaponCategory.Arrows), "two-handed sidearm means two quivers");
            Check.False(t3.Weapons.Contains(WeaponCategory.Shield), "no shield beside a two-hander");
            Check.True(t3.Weapons.Contains(WeaponCategory.TwoHandedSword), "sidearm is two-handed");

            // Polearm is never an archer's sidearm, however high the skill.
            SkillProfile polearmArcher = new SkillProfile(30, 20, 250, 200, 0, 0, 0);
            LoadoutTarget t4 = LoadoutPlanner.PlanTarget(polearmArcher, Naked(false), all, 30, false);
            Check.False(t4.Weapons.Contains(WeaponCategory.Polearm), "polearm never accompanies a bow");
            Check.False(t4.Weapons.Contains(WeaponCategory.Spear), "spear never accompanies a bow either");

            // Mounted crossbowman with only heavy crossbows available degrades to a bow.
            SkillProfile crossbowman = new SkillProfile(80, 20, 10, 120, 220, 0, 200);
            LoadoutTarget t5 = LoadoutPlanner.PlanTarget(crossbowman, Naked(true), noCrossbow, 30, false);
            Check.False(t5.Weapons.Contains(WeaponCategory.Crossbow), "unusable crossbow is dropped");
            Check.True(t5.Weapons.Contains(WeaponCategory.Bow), "degrades to a bow");

            // With a light crossbow available it keeps the crossbow.
            LoadoutTarget t6 = LoadoutPlanner.PlanTarget(crossbowman, Naked(true), all, 30, false);
            Check.True(t6.Weapons.Contains(WeaponCategory.Crossbow), "viable crossbow is kept");

            // Mounted archers always carry two quivers, never a shield.
            SkillProfile horseArcher = new SkillProfile(180, 20, 10, 190, 0, 0, 220);
            LoadoutTarget t7 = LoadoutPlanner.PlanTarget(horseArcher, Naked(true), all, 30, false);
            Check.Equal(2, CountOf(t7, WeaponCategory.Arrows), "mounted archer takes two quivers");
            Check.False(t7.Weapons.Contains(WeaponCategory.Shield), "mounted archer takes no shield");

            // Melee dominant with one-handed: weapon plus shield.
            SkillProfile infantry = new SkillProfile(220, 40, 60, 20, 0, 100, 0);
            LoadoutTarget t8 = LoadoutPlanner.PlanTarget(infantry, Naked(false), all, 30, false);
            Check.True(t8.Weapons.Contains(WeaponCategory.OneHandedSword), "infantry takes a one-hander");
            Check.True(t8.Weapons.Contains(WeaponCategory.Shield), "infantry takes a shield");

            // Two-handed dominant: no shield.
            SkillProfile berserker = new SkillProfile(120, 240, 40, 10, 0, 90, 0);
            LoadoutTarget t9 = LoadoutPlanner.PlanTarget(berserker, Naked(false), all, 30, false);
            Check.True(t9.Weapons.Contains(WeaponCategory.TwoHandedSword), "berserker takes a two-hander");
            Check.False(t9.Weapons.Contains(WeaponCategory.Shield), "berserker takes no shield");

            // Mount is wanted when Riding is in the top two.
            Check.True(t7.WantsMount, "high Riding wants a mount");
            Check.False(t8.WantsMount, "low Riding does not want a mount");

            // Mount is also wanted when the culture fields mounted elites.
            LoadoutTarget t10 = LoadoutPlanner.PlanTarget(infantry, Naked(false), all, 30, true);
            Check.True(t10.WantsMount, "mounted culture wants a mount");

            // The target never exceeds the four weapon slots.
            Check.True(t1.Weapons.Count <= 4, "target fits in four slots");
            Check.True(t9.Weapons.Count <= 4, "melee target fits in four slots");
        }
    }
}
```

- [ ] **Step 2: Registrar la suite**

En `tests/TestHarness.cs`, dentro de `Main`, añadir:

```csharp
            ArchetypeTests.RunAll();
```

- [ ] **Step 3: Ejecutar y comprobar que falla**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `BUILD FAILED` con `error CS0246` sobre `MountedRangedAvailability`, `LoadoutTarget` y `LoadoutPlanner`.

- [ ] **Step 4: Implementar los tipos de apoyo**

Crear `src/Core/MountedRangedAvailability.cs`:

```csharp
namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Whether the market or catalogue holds a bow / crossbow this hero could
    /// actually use from horseback. Computed outside the planner because it
    /// depends on item flags and perks, which the pure core must not see.
    /// </summary>
    public struct MountedRangedAvailability
    {
        public bool BowViable;
        public bool CrossbowViable;

        public MountedRangedAvailability(bool bowViable, bool crossbowViable)
        {
            BowViable = bowViable;
            CrossbowViable = crossbowViable;
        }

        /// <summary>Everything is viable. Correct for a hero on foot.</summary>
        public static MountedRangedAvailability All()
        {
            return new MountedRangedAvailability(true, true);
        }

        public bool IsViable(WeaponCategory category)
        {
            if (category == WeaponCategory.Bow) return BowViable;
            if (category == WeaponCategory.Crossbow) return CrossbowViable;
            return true;
        }
    }
}
```

Crear `src/Core/LoadoutTarget.cs`:

```csharp
using System.Collections.Generic;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// The loadout a hero deserves, before anything is reconciled against what
    /// they already carry. Weapons are listed in priority order.
    /// </summary>
    public sealed class LoadoutTarget
    {
        public LoadoutTarget()
        {
            Weapons = new List<WeaponCategory>();
        }

        public List<WeaponCategory> Weapons { get; private set; }
        public bool WantsMount { get; set; }
    }

    /// <summary>One decided placement: a category for a specific weapon slot.</summary>
    public struct PlannedSlot
    {
        public int SlotIndex;
        public WeaponCategory Category;

        public PlannedSlot(int slotIndex, WeaponCategory category)
        {
            SlotIndex = slotIndex;
            Category = category;
        }
    }
}
```

- [ ] **Step 5: Implementar PlanTarget**

Crear `src/Core/LoadoutPlanner.cs`:

```csharp
using System.Collections.Generic;

namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Decides what a hero should carry. Pure: no game types, fully testable.
    /// </summary>
    public static class LoadoutPlanner
    {
        /// <summary>
        /// Step one of the algorithm: the ideal loadout for this hero, computed
        /// from skills alone and independent of what they currently carry.
        /// </summary>
        public static LoadoutTarget PlanTarget(SkillProfile skills, SlotSnapshot current,
                                               MountedRangedAvailability availability,
                                               int dominanceMargin, bool cultureIsMounted)
        {
            LoadoutTarget target = new LoadoutTarget();
            target.WantsMount = skills.RidingInTopTwo() || cultureIsMounted;

            bool mounted = current.HasMount || target.WantsMount;

            WeaponCategory ranged = BestViableRanged(skills, availability, mounted);
            int rangedSkill = ranged == WeaponCategory.None ? 0 : skills.Get(SkillForCategory(ranged));

            WeaponCategory meleeBest = BestMelee(skills);
            int meleeSkill = meleeBest == WeaponCategory.None ? 0 : skills.Get(SkillForCategory(meleeBest));

            if (ranged != WeaponCategory.None && rangedSkill >= meleeSkill)
            {
                BuildRangedArchetype(target, skills, ranged, rangedSkill, meleeSkill, dominanceMargin, mounted);
            }
            else
            {
                BuildMeleeArchetype(target, skills, meleeBest);
            }

            TrimToSlots(target);
            return target;
        }

        /// <summary>
        /// The archer's sidearm is the better of OneHanded and TwoHanded.
        /// Polearm is deliberately excluded: two quivers and a spear is not a
        /// loadout anyone actually fields.
        /// </summary>
        private static WeaponCategory ArcherSidearm(SkillProfile skills)
        {
            return skills.Get(SkillKind.TwoHanded) > skills.Get(SkillKind.OneHanded)
                ? WeaponCategory.TwoHandedSword
                : WeaponCategory.OneHandedSword;
        }

        private static void BuildRangedArchetype(LoadoutTarget target, SkillProfile skills,
                                                 WeaponCategory ranged, int rangedSkill,
                                                 int meleeSkill, int dominanceMargin, bool mounted)
        {
            WeaponCategory ammo = CategoryRules.AmmoFor(ranged);
            WeaponCategory sidearm = ArcherSidearm(skills);

            target.Weapons.Add(ranged);
            target.Weapons.Add(ammo);

            bool sidearmIsTwoHanded = CategoryRules.IsTwoHanded(sidearm);
            bool dominant = (rangedSkill - meleeSkill) >= dominanceMargin;

            // A shield is only worth a slot beside a one-handed sidearm, and only
            // when the hero is not a dedicated shooter and is fighting on foot.
            bool takesShield = !sidearmIsTwoHanded && !dominant && !mounted;

            if (takesShield)
            {
                target.Weapons.Add(WeaponCategory.Shield);
            }
            else
            {
                target.Weapons.Add(ammo);
            }

            target.Weapons.Add(sidearm);
        }

        private static void BuildMeleeArchetype(LoadoutTarget target, SkillProfile skills, WeaponCategory best)
        {
            if (best == WeaponCategory.None) best = WeaponCategory.OneHandedSword;

            target.Weapons.Add(best);

            if (CategoryRules.IsTwoHanded(best))
            {
                // No shield: it would never come off the back.
                target.Weapons.Add(WeaponCategory.OneHandedSword);
            }
            else
            {
                target.Weapons.Add(WeaponCategory.Shield);
            }

            // Remaining slots are filled later by the gap-fill pass, which walks
            // the skill list with the same viability rules.
        }

        private static WeaponCategory BestViableRanged(SkillProfile skills,
                                                       MountedRangedAvailability availability,
                                                       bool mounted)
        {
            int bow = skills.Get(SkillKind.Bow);
            int crossbow = skills.Get(SkillKind.Crossbow);

            bool bowOk = !mounted || availability.IsViable(WeaponCategory.Bow);
            bool crossbowOk = !mounted || availability.IsViable(WeaponCategory.Crossbow);

            WeaponCategory best = WeaponCategory.None;
            int bestValue = 0;

            if (crossbowOk && crossbow > bestValue) { best = WeaponCategory.Crossbow; bestValue = crossbow; }
            if (bowOk && bow > bestValue) { best = WeaponCategory.Bow; bestValue = bow; }

            return best;
        }

        private static WeaponCategory BestMelee(SkillProfile skills)
        {
            int oneHanded = skills.Get(SkillKind.OneHanded);
            int twoHanded = skills.Get(SkillKind.TwoHanded);
            int polearm = skills.Get(SkillKind.Polearm);

            if (twoHanded >= oneHanded && twoHanded >= polearm && twoHanded > 0) return WeaponCategory.TwoHandedSword;
            if (polearm >= oneHanded && polearm > 0) return WeaponCategory.Spear;
            if (oneHanded > 0) return WeaponCategory.OneHandedSword;
            return WeaponCategory.None;
        }

        /// <summary>Maps a planned category back to the skill that governs it.</summary>
        public static SkillKind SkillForCategory(WeaponCategory category)
        {
            switch (category)
            {
                case WeaponCategory.TwoHandedSword:
                case WeaponCategory.TwoHandedAxe:
                    return SkillKind.TwoHanded;
                case WeaponCategory.Spear:
                case WeaponCategory.Polearm:
                    return SkillKind.Polearm;
                case WeaponCategory.Bow:
                case WeaponCategory.Arrows:
                    return SkillKind.Bow;
                case WeaponCategory.Crossbow:
                case WeaponCategory.Bolts:
                    return SkillKind.Crossbow;
                case WeaponCategory.Throwing:
                    return SkillKind.Throwing;
                default:
                    return SkillKind.OneHanded;
            }
        }

        /// <summary>Maps a skill to the weapon category it would buy.</summary>
        public static WeaponCategory CategoryForSkill(SkillKind skill)
        {
            switch (skill)
            {
                case SkillKind.TwoHanded: return WeaponCategory.TwoHandedSword;
                case SkillKind.Polearm: return WeaponCategory.Spear;
                case SkillKind.Bow: return WeaponCategory.Bow;
                case SkillKind.Crossbow: return WeaponCategory.Crossbow;
                case SkillKind.Throwing: return WeaponCategory.Throwing;
                default: return WeaponCategory.OneHandedSword;
            }
        }

        private static void TrimToSlots(LoadoutTarget target)
        {
            while (target.Weapons.Count > SlotSnapshot.WeaponSlotCount)
            {
                target.Weapons.RemoveAt(target.Weapons.Count - 1);
            }
        }
    }
}
```

- [ ] **Step 6: Ejecutar y comprobar que pasa**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `0 failed` y código de salida 0.

- [ ] **Step 7: Commit**

```bash
git add src/Core/MountedRangedAvailability.cs src/Core/LoadoutTarget.cs src/Core/LoadoutPlanner.cs tests/ArchetypeTests.cs tests/TestHarness.cs
git commit -m "feat(core): add archetype planning with archer sidearm rules"
```

---

### Task 6: Reconciliación y relleno

**Files:**
- Modify: `src/Core/LoadoutPlanner.cs` (añadir `Plan`)
- Create: `tests/PlannerTests.cs`
- Modify: `tests/TestHarness.cs`

**Interfaces:**
- Consumes: todo lo anterior del núcleo.
- Produces: `static List<PlannedSlot> LoadoutPlanner.Plan(SkillProfile skills, SlotSnapshot current, MountedRangedAvailability availability, int dominanceMargin, bool cultureIsMounted)`.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/PlannerTests.cs`:

```csharp
using System.Collections.Generic;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class PlannerTests
    {
        private static bool Plans(List<PlannedSlot> plan, WeaponCategory c)
        {
            foreach (PlannedSlot p in plan) { if (p.Category == c) return true; }
            return false;
        }

        private static int CountPlanned(List<PlannedSlot> plan, WeaponCategory c)
        {
            int n = 0;
            foreach (PlannedSlot p in plan) { if (p.Category == c) n++; }
            return n;
        }

        public static void RunAll()
        {
            MountedRangedAvailability all = MountedRangedAvailability.All();

            // Spec case 1: shield + spear + one-hander, one free slot, Throwing third.
            SlotSnapshot infantry = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Spear, WeaponCategory.Shield, WeaponCategory.OneHandedSword, WeaponCategory.None },
                false, false, true, true, true, true, false);
            SkillProfile throwerSkills = new SkillProfile(200, 30, 210, 20, 10, 150, 40);
            List<PlannedSlot> p1 = LoadoutPlanner.Plan(throwerSkills, infantry, all, 30, false);
            Check.Equal(1, p1.Count, "one slot planned");
            Check.True(Plans(p1, WeaponCategory.Throwing), "throwing fills the gap");

            // Spec case 2: same, but Bow is third. Cost two, only one slot free.
            SkillProfile bowThirdSkills = new SkillProfile(200, 150, 210, 160, 10, 20, 40);
            List<PlannedSlot> p2 = LoadoutPlanner.Plan(bowThirdSkills, infantry, all, 30, false);
            Check.False(Plans(p2, WeaponCategory.Bow), "bow does not fit in one slot");
            Check.True(Plans(p2, WeaponCategory.TwoHandedSword), "falls through to the next skill");

            // Spec case 3: two free slots and Bow third -> bow plus ammo.
            SlotSnapshot twoFree = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Spear, WeaponCategory.Shield, WeaponCategory.None, WeaponCategory.None },
                false, false, true, true, true, true, false);
            List<PlannedSlot> p3 = LoadoutPlanner.Plan(bowThirdSkills, twoFree, all, 30, false);
            Check.True(Plans(p3, WeaponCategory.Bow), "bow fits in two slots");
            Check.True(Plans(p3, WeaponCategory.Arrows), "ammo planned alongside");

            // Spec case 9: nothing empty means nothing planned.
            SlotSnapshot full = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.Spear, WeaponCategory.Shield, WeaponCategory.OneHandedSword, WeaponCategory.Throwing },
                false, false, true, true, true, true, false);
            List<PlannedSlot> p4 = LoadoutPlanner.Plan(throwerSkills, full, all, 30, false);
            Check.Equal(0, p4.Count, "no empty slots, no plan");

            // Spec case 11: the eighteen-year-old with the vanilla dummy set.
            SlotSnapshot dummy = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.OneHandedSword, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, false, true, true, false, false);
            SkillProfile archerSkills = new SkillProfile(60, 20, 10, 200, 0, 0, 0);
            List<PlannedSlot> p5 = LoadoutPlanner.Plan(archerSkills, dummy, all, 30, false);
            Check.True(Plans(p5, WeaponCategory.Bow), "dummy-set archer gets a bow");
            Check.Equal(2, CountPlanned(p5, WeaponCategory.Arrows), "and two quivers");
            Check.False(Plans(p5, WeaponCategory.OneHandedSword), "the spatha is not duplicated");
            Check.Equal(3, p5.Count, "exactly the three free slots are used");

            // Spec case 12: the target wants a shield but a two-hander is equipped.
            SlotSnapshot twoHander = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.TwoHandedSword, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, true, true, true, true, false);
            SkillProfile hybridSkills = new SkillProfile(190, 200, 20, 180, 0, 0, 0);
            List<PlannedSlot> p6 = LoadoutPlanner.Plan(hybridSkills, twoHander, all, 30, false);
            Check.False(Plans(p6, WeaponCategory.Shield), "shield dropped beside a two-hander");

            // Spec case 8: a completely naked hero gets a coherent four-slot loadout.
            SlotSnapshot naked = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                false, false, false, false, false, false, false);
            List<PlannedSlot> p7 = LoadoutPlanner.Plan(archerSkills, naked, all, 30, false);
            Check.Equal(4, p7.Count, "naked hero fills all four slots");

            // Spec case 14: a flagged bow on a mounted hero is not viable.
            MountedRangedAvailability noBow = new MountedRangedAvailability(false, false);
            SlotSnapshot mountedNaked = new SlotSnapshot(
                new WeaponCategory[] { WeaponCategory.None, WeaponCategory.None, WeaponCategory.None, WeaponCategory.None },
                true, true, false, false, false, false, false);
            List<PlannedSlot> p8 = LoadoutPlanner.Plan(archerSkills, mountedNaked, noBow, 30, false);
            Check.False(Plans(p8, WeaponCategory.Bow), "unusable bow is never planned");

            // No plan ever targets an occupied slot.
            foreach (PlannedSlot slot in p5)
            {
                Check.Equal((int)WeaponCategory.None, (int)dummy.WeaponAt(slot.SlotIndex), "planned slot was empty");
            }
        }
    }
}
```

- [ ] **Step 2: Registrar la suite**

En `tests/TestHarness.cs`, dentro de `Main`, añadir:

```csharp
            PlannerTests.RunAll();
```

- [ ] **Step 3: Ejecutar y comprobar que falla**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `BUILD FAILED` con `error CS0117: 'LoadoutPlanner' does not contain a definition for 'Plan'`.

- [ ] **Step 4: Implementar Plan**

Añadir a `src/Core/LoadoutPlanner.cs`, dentro de la clase `LoadoutPlanner`, justo después de `PlanTarget`:

```csharp
        /// <summary>
        /// The whole algorithm: plan the target, reconcile it against what the
        /// hero already carries, then fill the empty slots with what remains.
        /// Nothing already equipped is ever removed.
        /// </summary>
        public static List<PlannedSlot> Plan(SkillProfile skills, SlotSnapshot current,
                                             MountedRangedAvailability availability,
                                             int dominanceMargin, bool cultureIsMounted)
        {
            List<PlannedSlot> plan = new List<PlannedSlot>();

            int[] freeSlots = current.EmptySlotIndices();
            if (freeSlots.Length == 0) return plan;

            LoadoutTarget target = PlanTarget(skills, current, availability, dominanceMargin, cultureIsMounted);
            List<WeaponCategory> wanted = Reconcile(target, current);

            int nextFree = 0;
            bool mounted = current.HasMount || target.WantsMount;

            // Step three: place what survived reconciliation.
            foreach (WeaponCategory category in wanted)
            {
                if (nextFree >= freeSlots.Length) break;
                if (!IsPlaceable(category, availability, mounted)) continue;

                int cost = CategoryRules.IsRanged(category) ? 2 : 1;
                if (freeSlots.Length - nextFree < cost) continue;

                plan.Add(new PlannedSlot(freeSlots[nextFree++], category));
                if (cost == 2)
                {
                    plan.Add(new PlannedSlot(freeSlots[nextFree++], CategoryRules.AmmoFor(category)));
                }
            }

            // Still room: walk the skill list for anything not yet represented.
            if (nextFree < freeSlots.Length)
            {
                nextFree = FillFromSkills(plan, skills, current, availability, mounted, freeSlots, nextFree);
            }

            // Still room: courtesy filling.
            if (nextFree < freeSlots.Length)
            {
                FillCourtesy(plan, current, freeSlots, nextFree);
            }

            return plan;
        }

        /// <summary>
        /// Step two: drop target entries the hero already satisfies, and entries
        /// the current equipment contradicts.
        /// </summary>
        private static List<WeaponCategory> Reconcile(LoadoutTarget target, SlotSnapshot current)
        {
            List<WeaponCategory> remaining = new List<WeaponCategory>();

            // Count what the hero already has so duplicated target entries (two
            // quivers) are only satisfied once each.
            Dictionary<WeaponCategory, int> have = new Dictionary<WeaponCategory, int>();
            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                WeaponCategory c = current.WeaponAt(i);
                if (c == WeaponCategory.None) continue;
                have[c] = have.ContainsKey(c) ? have[c] + 1 : 1;
            }

            foreach (WeaponCategory category in target.Weapons)
            {
                if (have.ContainsKey(category) && have[category] > 0)
                {
                    have[category] = have[category] - 1;
                    continue;
                }

                // A shield is pointless next to a two-handed weapon.
                if (category == WeaponCategory.Shield && current.HasTwoHandedEquipped) continue;

                remaining.Add(category);
            }

            return remaining;
        }

        private static bool IsPlaceable(WeaponCategory category,
                                        MountedRangedAvailability availability, bool mounted)
        {
            if (category == WeaponCategory.None) return false;
            if (mounted && !availability.IsViable(category)) return false;
            return true;
        }

        private static int FillFromSkills(List<PlannedSlot> plan, SkillProfile skills, SlotSnapshot current,
                                          MountedRangedAvailability availability, bool mounted,
                                          int[] freeSlots, int nextFree)
        {
            SkillKind[] order = skills.CombatSkillsDescending();

            foreach (SkillKind skill in order)
            {
                if (nextFree >= freeSlots.Length) break;
                if (skills.Get(skill) <= 0) continue;

                WeaponCategory category = CategoryForSkill(skill);
                if (current.Contains(category)) continue;
                if (AlreadyPlanned(plan, category)) continue;
                if (!IsPlaceable(category, availability, mounted)) continue;

                int cost = CategoryRules.IsRanged(category) ? 2 : 1;
                if (freeSlots.Length - nextFree < cost) continue;

                plan.Add(new PlannedSlot(freeSlots[nextFree++], category));
                if (cost == 2)
                {
                    plan.Add(new PlannedSlot(freeSlots[nextFree++], CategoryRules.AmmoFor(category)));
                }
            }

            return nextFree;
        }

        /// <summary>
        /// Last resort: a shield, then spare ammunition, then nothing. Leaving a
        /// slot empty is a valid outcome and beats equipping something incoherent.
        /// </summary>
        private static void FillCourtesy(List<PlannedSlot> plan, SlotSnapshot current,
                                         int[] freeSlots, int nextFree)
        {
            bool shieldPossible = !current.Contains(WeaponCategory.Shield)
                                  && !AlreadyPlanned(plan, WeaponCategory.Shield)
                                  && !current.HasTwoHandedEquipped
                                  && !PlanIntroducesTwoHanded(plan);

            if (shieldPossible && nextFree < freeSlots.Length)
            {
                plan.Add(new PlannedSlot(freeSlots[nextFree++], WeaponCategory.Shield));
            }

            WeaponCategory ammo = SpareAmmoKind(plan, current);
            while (ammo != WeaponCategory.None && nextFree < freeSlots.Length && CountPlanned(plan, ammo) < 2)
            {
                plan.Add(new PlannedSlot(freeSlots[nextFree++], ammo));
            }
        }

        private static bool PlanIntroducesTwoHanded(List<PlannedSlot> plan)
        {
            foreach (PlannedSlot p in plan)
            {
                if (CategoryRules.IsTwoHanded(p.Category)) return true;
            }
            return false;
        }

        /// <summary>The ammunition kind matching whatever ranged weapon is in play, or None.</summary>
        private static WeaponCategory SpareAmmoKind(List<PlannedSlot> plan, SlotSnapshot current)
        {
            if (current.Contains(WeaponCategory.Bow) || AlreadyPlanned(plan, WeaponCategory.Bow))
                return WeaponCategory.Arrows;
            if (current.Contains(WeaponCategory.Crossbow) || AlreadyPlanned(plan, WeaponCategory.Crossbow))
                return WeaponCategory.Bolts;
            return WeaponCategory.None;
        }

        private static bool AlreadyPlanned(List<PlannedSlot> plan, WeaponCategory category)
        {
            foreach (PlannedSlot p in plan)
            {
                if (p.Category == category) return true;
            }
            return false;
        }

        private static int CountPlanned(List<PlannedSlot> plan, WeaponCategory category)
        {
            int n = 0;
            foreach (PlannedSlot p in plan)
            {
                if (p.Category == category) n++;
            }
            return n;
        }
```

- [ ] **Step 5: Ejecutar y comprobar que pasa**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `0 failed` y código de salida 0.

- [ ] **Step 6: Commit**

```bash
git add src/Core/LoadoutPlanner.cs tests/PlannerTests.cs tests/TestHarness.cs
git commit -m "feat(core): add reconcile and fill passes to the planner"
```

---

### Task 7: BudgetMath

**Files:**
- Create: `src/Core/BudgetMath.cs`
- Create: `tests/BudgetMathTests.cs`
- Modify: `tests/TestHarness.cs`

**Interfaces:**
- Consumes: nada.
- Produces:
  - `static int BudgetMath.Reserve(int warPartyCount, float multiplier)`
  - `static int BudgetMath.Available(int heroGold, int clanGold, int pendingClanSpend, int reserve)`
  - `static float BudgetMath.ClanShare(bool isClanLeader, int clanTier, int heroGold, int clanGold)`
  - `const int BudgetMath.PartyGoldLowerThreshold = 5000`

Esta tarea se adelanta a la fase de compra porque es aritmética pura y pertenece al núcleo testeable. Se consumirá en el plan siguiente.

- [ ] **Step 1: Escribir el test que falla**

Crear `tests/BudgetMathTests.cs`:

```csharp
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer.Tests
{
    public static class BudgetMathTests
    {
        public static void RunAll()
        {
            Check.Equal(5000, BudgetMath.PartyGoldLowerThreshold, "threshold matches the game constant");

            Check.Equal(20000, BudgetMath.Reserve(4, 1.0f), "four parties reserve twenty thousand");
            Check.Equal(0, BudgetMath.Reserve(0, 1.0f), "no parties, no reserve");
            Check.Equal(10000, BudgetMath.Reserve(4, 0.5f), "multiplier halves the reserve");

            // Spec case 15: clan with 100k and four parties.
            int reserve = BudgetMath.Reserve(4, 1.0f);
            Check.Equal(85000, BudgetMath.Available(5000, 100000, 0, reserve), "first lord sees hero plus clan minus reserve");
            Check.Equal(35000, BudgetMath.Available(5000, 100000, 50000, reserve), "pending spend reduces what the next lord sees");
            Check.Equal(5000, BudgetMath.Available(5000, 100000, 90000, reserve), "at the reserve only hero gold remains");
            Check.Equal(5000, BudgetMath.Available(5000, 100000, 200000, reserve), "over-committed never goes negative");

            // A poor clan contributes nothing but the hero can still spend his own.
            Check.Equal(300, BudgetMath.Available(300, 1000, 0, reserve), "poor clan contributes nothing");

            // Spec case 17: the cost split.
            Check.Equal(30, (int)(BudgetMath.ClanShare(false, 2, 5000, 10000) * 100f), "rich hero pays most of it");
            Check.Equal(20, (int)(BudgetMath.ClanShare(false, 2, 5000, 50000) * 100f), "rich clan covers less as the hero is rich too");
            Check.Equal(70, (int)(BudgetMath.ClanShare(true, 0, 100, 100) * 100f), "poor clan leader is covered by the house");
            Check.Equal(40, (int)(BudgetMath.ClanShare(false, 3, 100, 100) * 100f), "established clan covers less than base");
            Check.Equal(60, (int)(BudgetMath.ClanShare(false, 0, 100, 100) * 100f), "base share");

            // The share never leaves the ten to eighty band.
            float low = BudgetMath.ClanShare(false, 6, 999999, 999999);
            Check.True(low >= 0.10f, "share never below ten percent");
            float high = BudgetMath.ClanShare(true, 0, 0, 0);
            Check.True(high <= 0.80f, "share never above eighty percent");
        }
    }
}
```

- [ ] **Step 2: Registrar la suite**

En `tests/TestHarness.cs`, dentro de `Main`, añadir:

```csharp
            BudgetMathTests.RunAll();
```

- [ ] **Step 3: Ejecutar y comprobar que falla**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `BUILD FAILED` con `error CS0246` sobre `BudgetMath`.

- [ ] **Step 4: Implementar**

Crear `src/Core/BudgetMath.cs`:

```csharp
namespace HeroLoadoutFixer.Core
{
    /// <summary>
    /// Money arithmetic, kept pure so the economy can be reasoned about without
    /// a running campaign. Adapted from Lords Gear, plus a safety reserve that
    /// neither Lords Gear nor NoblesBuyStuff applies.
    /// </summary>
    public static class BudgetMath
    {
        /// <summary>
        /// DefaultClanFinanceModel.PartyGoldLowerThreshold. Below this a party is
        /// in financial trouble, so it is the floor we refuse to spend past.
        /// </summary>
        public const int PartyGoldLowerThreshold = 5000;

        public static int Reserve(int warPartyCount, float multiplier)
        {
            if (warPartyCount <= 0) return 0;
            if (multiplier < 0f) multiplier = 0f;
            return (int)(warPartyCount * PartyGoldLowerThreshold * multiplier);
        }

        /// <summary>
        /// What a hero may spend: his own gold, plus whatever of the clan purse
        /// is not already committed this cycle and not protected by the reserve.
        /// </summary>
        public static int Available(int heroGold, int clanGold, int pendingClanSpend, int reserve)
        {
            if (heroGold < 0) heroGold = 0;

            int clanContribution = clanGold - pendingClanSpend - reserve;
            if (clanContribution < 0) clanContribution = 0;

            return heroGold + clanContribution;
        }

        /// <summary>
        /// The fraction of a purchase the clan covers. The richer the hero, the
        /// more he pays himself; the richer the house, the less it needs to.
        /// </summary>
        public static float ClanShare(bool isClanLeader, int clanTier, int heroGold, int clanGold)
        {
            float share = 0.60f;

            if (isClanLeader) share = 0.70f;
            if (clanTier >= 1) share = 0.40f;
            if (heroGold > 2000) share = 0.30f;
            if (clanGold > 40000) share = 0.20f;

            if (share < 0.10f) share = 0.10f;
            if (share > 0.80f) share = 0.80f;
            return share;
        }
    }
}
```

- [ ] **Step 5: Ejecutar y comprobar que pasa**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `0 failed` y código de salida 0.

- [ ] **Step 6: Commit**

```bash
git add src/Core/BudgetMath.cs tests/BudgetMathTests.cs tests/TestHarness.cs
git commit -m "feat(core): add BudgetMath with clan reserve and cost split"
```

---

### Task 8: Esqueleto del módulo que carga en el juego

**Files:**
- Create: `SubModule.xml`
- Create: `src/SubModule.cs`
- Create: `src/Game/ModLog.cs`
- Create: `build/build-mod.ps1`

**Interfaces:**
- Consumes: nada del núcleo.
- Produces: `HeroLoadoutFixer.ModLog.Info(string)`, `ModLog.Error(string)`, y un módulo que arranca sin excepciones.

- [ ] **Step 1: Escribir el descriptor del módulo**

Crear `SubModule.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Module>
  <Name value="Hero Loadout Fixer"/>
  <Id value="HeroLoadoutFixer"/>
  <Version value="v0.1.0"/>
  <SingleplayerModule value="true"/>
  <MultiplayerModule value="false"/>
  <DependedModules>
    <DependedModule Id="Native" DependentVersion="v1.4.8" MinVersion="v1.4.8"/>
    <DependedModule Id="SandBoxCore" DependentVersion="v1.4.8" MinVersion="v1.4.8"/>
    <DependedModule Id="Sandbox" DependentVersion="v1.4.8" MinVersion="v1.4.8"/>
    <DependedModule Id="StoryMode" DependentVersion="v1.4.8" MinVersion="v1.4.8"/>
  </DependedModules>
  <SubModules>
    <SubModule>
      <Name value="HeroLoadoutFixer"/>
      <DLLName value="HeroLoadoutFixer.dll"/>
      <SubModuleClassType value="HeroLoadoutFixer.SubModule"/>
      <Tags>
        <Tag key="DedicatedServerType" value="none"/>
        <Tag key="IsNoRenderModeElement" value="false"/>
      </Tags>
      <Official value="false"/>
    </SubModule>
  </SubModules>
</Module>
```

- [ ] **Step 2: Escribir el registro a fichero**

Crear `src/Game/ModLog.cs`:

```csharp
using System;
using System.IO;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Rotating file log under %LocalAppData%. Every call is guarded: logging
    /// must never be the reason a campaign fails.
    /// </summary>
    public static class ModLog
    {
        private const string FileName = "hlf.log";
        private static readonly object Gate = new object();
        public static bool Enabled = true;

        public static void Info(string message) { Write("INFO", message); }
        public static void Error(string message) { Write("ERROR", message); }

        private static void Write(string level, string message)
        {
            if (!Enabled) return;
            try
            {
                lock (Gate)
                {
                    string folder = GetFolder();
                    if (folder == null) return;
                    string path = Path.Combine(folder, FileName);
                    string line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(path, line);
                }
            }
            catch
            {
                // Swallowed on purpose: a failed log write is not worth a crash.
            }
        }

        private static string GetFolder()
        {
            try
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(root)) return null;
                string folder = Path.Combine(Path.Combine(root, "Mount and Blade II Bannerlord"), "logs");
                Directory.CreateDirectory(folder);
                return folder;
            }
            catch
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 3: Escribir el punto de entrada**

Crear `src/SubModule.cs`:

```csharp
using TaleWorlds.MountAndBlade;

namespace HeroLoadoutFixer
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("SubModule loaded.");
        }
    }
}
```

- [ ] **Step 4: Escribir el script de compilación y despliegue**

Crear `build/build-mod.ps1`:

```powershell
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc  = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$game = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
$bin  = Join-Path $game "bin\Win64_Shipping_Client"
$fw   = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
$out  = Join-Path $root "build\out"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$refs = @(
  "$fw\mscorlib.dll", "$fw\System.dll", "$fw\System.Core.dll",
  "$bin\mono\lib\mono\4.5\Facades\netstandard.dll",
  "$bin\TaleWorlds.CampaignSystem.dll", "$bin\TaleWorlds.Core.dll",
  "$bin\TaleWorlds.Library.dll", "$bin\TaleWorlds.ObjectSystem.dll",
  "$bin\TaleWorlds.Localization.dll", "$bin\TaleWorlds.MountAndBlade.dll"
)
foreach ($r in $refs) {
  if (-not (Test-Path $r)) { Write-Host "MISSING REFERENCE: $r"; exit 1 }
}

$sources = Get-ChildItem (Join-Path $root "src") -Recurse -Filter *.cs

$rsp = Join-Path $out "mod.rsp"
$lines = @("/nologo", "/target:library", "/platform:x64", "/optimize+", "/out:`"$out\HeroLoadoutFixer.dll`"")
foreach ($r in $refs)    { $lines += "/r:`"$r`"" }
foreach ($s in $sources) { $lines += "`"$($s.FullName)`"" }
Set-Content -Path $rsp -Value $lines -Encoding UTF8

& $csc /noconfig "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 1 }

$dest = Join-Path $game "Modules\HeroLoadoutFixer"
New-Item -ItemType Directory -Force -Path (Join-Path $dest "bin\Win64_Shipping_Client") | Out-Null
Copy-Item (Join-Path $root "SubModule.xml") $dest -Force
Copy-Item "$out\HeroLoadoutFixer.dll" (Join-Path $dest "bin\Win64_Shipping_Client") -Force
Write-Host "DEPLOYED to $dest"
```

- [ ] **Step 5: Compilar y desplegar**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-mod.ps1"
```

Esperado: `DEPLOYED to D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\HeroLoadoutFixer`.

- [ ] **Step 6: Comprobar que carga en el juego**

Arrancar el launcher, activar «Hero Loadout Fixer» al final de la lista, entrar a una campaña y salir. Luego:

```bash
type "%LocalAppData%\Mount and Blade II Bannerlord\logs\hlf.log"
```

Esperado: una línea `[INFO] SubModule loaded.`

- [ ] **Step 7: Commit**

```bash
git add SubModule.xml src/SubModule.cs src/Game/ModLog.cs build/build-mod.ps1
git commit -m "feat: add loadable module skeleton with file logging"
```

---

### Task 9: Adaptadores del juego al núcleo

**Files:**
- Create: `src/Game/SlotMapping.cs`
- Create: `src/Game/ItemClassifier.cs`
- Create: `src/Game/HeroAdapter.cs`

**Interfaces:**
- Consumes: `SkillProfile`, `SlotSnapshot`, `WeaponCategory`.
- Produces:
  - `static EquipmentIndex SlotMapping.WeaponSlot(int index)` para 0..3
  - `static WeaponCategory ItemClassifier.Classify(ItemObject item)`
  - `static bool ItemClassifier.IsUsableMounted(ItemObject item, Hero hero)`
  - `static SkillProfile HeroAdapter.ReadSkills(Hero hero)`
  - `static SlotSnapshot HeroAdapter.ReadEquipment(Equipment equipment)`
  - `static bool HeroAdapter.CultureFieldsMountedElites(Hero hero)`

**Nota sobre `WeaponFlags`.** En v1.4.8, `WeaponComponentData.WeaponFlags` es un **campo**, no una propiedad: no existe `get_WeaponFlags`. La sintaxis de acceso en C# es la misma, así que el código compila igual, pero conviene saberlo al leer el informe de `verify-api.ps1`, que lista campos y propiedades por separado. Este es justo el tipo de cambio que dejó inservible a DynamicLordGear, donde `MBEquipmentRoster.EquipmentCulture` pasó de campo a propiedad entre versiones.

No hay tests unitarios aquí: estos tipos tocan la API del juego y no pueden compilarse en el ejecutable de tests. Se verifican en el juego mediante el log de la tarea 11.

- [ ] **Step 1: Escribir el mapeo de ranuras**

Crear `src/Game/SlotMapping.cs`:

```csharp
using TaleWorlds.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Translation between core slot indices and Bannerlord's EquipmentIndex.</summary>
    public static class SlotMapping
    {
        public static EquipmentIndex WeaponSlot(int index)
        {
            switch (index)
            {
                case 0: return EquipmentIndex.Weapon0;
                case 1: return EquipmentIndex.Weapon1;
                case 2: return EquipmentIndex.Weapon2;
                default: return EquipmentIndex.Weapon3;
            }
        }

        public static readonly EquipmentIndex[] ArmorSlots =
        {
            EquipmentIndex.Head,
            EquipmentIndex.Body,
            EquipmentIndex.Leg,
            EquipmentIndex.Gloves,
            EquipmentIndex.Cape
        };
    }
}
```

- [ ] **Step 2: Escribir el clasificador de ítems**

Crear `src/Game/ItemClassifier.cs`:

```csharp
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Maps game items onto the core's category vocabulary.</summary>
    public static class ItemClassifier
    {
        public static WeaponCategory Classify(ItemObject item)
        {
            if (item == null) return WeaponCategory.None;

            if (item.ItemType == ItemObject.ItemTypeEnum.Shield) return WeaponCategory.Shield;
            if (item.ItemType == ItemObject.ItemTypeEnum.Arrows) return WeaponCategory.Arrows;
            if (item.ItemType == ItemObject.ItemTypeEnum.Bolts) return WeaponCategory.Bolts;
            if (item.ItemType == ItemObject.ItemTypeEnum.Bow) return WeaponCategory.Bow;
            if (item.ItemType == ItemObject.ItemTypeEnum.Crossbow) return WeaponCategory.Crossbow;
            if (item.ItemType == ItemObject.ItemTypeEnum.Thrown) return WeaponCategory.Throwing;

            if (!item.HasWeaponComponent) return WeaponCategory.None;

            WeaponComponentData weapon = item.PrimaryWeapon;
            if (weapon == null) return WeaponCategory.Other;

            switch (weapon.WeaponClass)
            {
                case WeaponClass.OneHandedSword: return WeaponCategory.OneHandedSword;
                case WeaponClass.TwoHandedSword: return WeaponCategory.TwoHandedSword;
                case WeaponClass.OneHandedAxe: return WeaponCategory.OneHandedAxe;
                case WeaponClass.TwoHandedAxe: return WeaponCategory.TwoHandedAxe;
                case WeaponClass.Mace: return WeaponCategory.Mace;
                case WeaponClass.TwoHandedMace: return WeaponCategory.Mace;
                case WeaponClass.OneHandedPolearm: return WeaponCategory.Spear;
                case WeaponClass.TwoHandedPolearm: return WeaponCategory.Polearm;
                case WeaponClass.LowGripPolearm: return WeaponCategory.Polearm;
                case WeaponClass.Bow: return WeaponCategory.Bow;
                case WeaponClass.Crossbow: return WeaponCategory.Crossbow;
                case WeaponClass.Arrow: return WeaponCategory.Arrows;
                case WeaponClass.Bolt: return WeaponCategory.Bolts;
                case WeaponClass.Javelin: return WeaponCategory.Throwing;
                case WeaponClass.ThrowingAxe: return WeaponCategory.Throwing;
                case WeaponClass.ThrowingKnife: return WeaponCategory.Throwing;
                case WeaponClass.Stone: return WeaponCategory.Throwing;
                default: return WeaponCategory.Other;
            }
        }

        /// <summary>
        /// Whether this hero could actually use the weapon from horseback.
        /// Decided per item, never per class: light crossbows reload mounted and
        /// heavy ones do not, and a modded bow may carry the flag too.
        /// </summary>
        public static bool IsUsableMounted(ItemObject item, Hero hero)
        {
            if (item == null || hero == null) return false;
            if (!item.HasWeaponComponent) return true;

            WeaponComponentData weapon = item.PrimaryWeapon;
            if (weapon == null) return true;

            if ((weapon.WeaponFlags & WeaponFlags.CantReloadOnHorseback) == 0) return true;

            // Only the crossbow perk is documented to lift the restriction:
            // "You can reload any crossbow on horseback."
            if (Classify(item) == WeaponCategory.Crossbow)
            {
                return hero.GetPerkValue(DefaultPerks.Crossbow.MountedCrossbowman);
            }

            return false;
        }

        /// <summary>The skill that gates using this item at all, via its difficulty.</summary>
        public static bool MeetsDifficulty(ItemObject item, SkillProfile skills)
        {
            if (item == null) return false;
            if (item.Difficulty <= 0f) return true;

            WeaponCategory category = Classify(item);
            SkillKind skill = LoadoutPlanner.SkillForCategory(category);

            if (item.ItemType == ItemObject.ItemTypeEnum.Horse) skill = SkillKind.Riding;

            return skills.Get(skill) >= (int)item.Difficulty;
        }
    }
}
```

- [ ] **Step 3: Escribir el adaptador de héroe**

Crear `src/Game/HeroAdapter.cs`:

```csharp
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Reads game state into the core's pure types.</summary>
    public static class HeroAdapter
    {
        public static SkillProfile ReadSkills(Hero hero)
        {
            if (hero == null) return new SkillProfile(0, 0, 0, 0, 0, 0, 0);

            return new SkillProfile(
                hero.GetSkillValue(DefaultSkills.OneHanded),
                hero.GetSkillValue(DefaultSkills.TwoHanded),
                hero.GetSkillValue(DefaultSkills.Polearm),
                hero.GetSkillValue(DefaultSkills.Bow),
                hero.GetSkillValue(DefaultSkills.Crossbow),
                hero.GetSkillValue(DefaultSkills.Throwing),
                hero.GetSkillValue(DefaultSkills.Riding));
        }

        public static SlotSnapshot ReadEquipment(Equipment equipment)
        {
            WeaponCategory[] weapons = new WeaponCategory[SlotSnapshot.WeaponSlotCount];

            if (equipment == null)
            {
                for (int i = 0; i < weapons.Length; i++) weapons[i] = WeaponCategory.None;
                return new SlotSnapshot(weapons, false, false, false, false, false, false, false);
            }

            for (int i = 0; i < weapons.Length; i++)
            {
                EquipmentElement element = equipment[SlotMapping.WeaponSlot(i)];
                weapons[i] = ItemClassifier.Classify(element.Item);
            }

            return new SlotSnapshot(
                weapons,
                equipment[EquipmentIndex.Horse].Item != null,
                equipment[EquipmentIndex.HorseHarness].Item != null,
                equipment[EquipmentIndex.Head].Item != null,
                equipment[EquipmentIndex.Body].Item != null,
                equipment[EquipmentIndex.Leg].Item != null,
                equipment[EquipmentIndex.Gloves].Item != null,
                equipment[EquipmentIndex.Cape].Item != null);
        }

        /// <summary>
        /// True when the hero's culture fields mounted elite troops, walking the
        /// elite line's upgrade targets. Works with modded cultures because it
        /// reads the troop tree rather than hardcoding faction names.
        /// </summary>
        public static bool CultureFieldsMountedElites(Hero hero)
        {
            if (hero == null) return false;

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;
            if (culture == null) return false;

            CharacterObject troop = culture.EliteBasicTroop;
            int guard = 0;

            while (troop != null && guard < 8)
            {
                guard++;

                MBReadOnlyList<Equipment> sets = troop.BattleEquipments;
                if (sets != null)
                {
                    foreach (Equipment set in sets)
                    {
                        if (set != null && set[EquipmentIndex.Horse].Item != null) return true;
                    }
                }

                CharacterObject[] upgrades = troop.UpgradeTargets;
                if (upgrades == null || upgrades.Length == 0) break;
                troop = upgrades[upgrades.Length - 1];
            }

            return false;
        }
    }
}
```

- [ ] **Step 4: Compilar**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-mod.ps1"
```

Esperado: `DEPLOYED`. Si aparece `CS0117` o `CS1061` sobre algún miembro de TaleWorlds, ese miembro no existe con ese nombre en v1.4.8: comprobarlo antes de inventar un sustituto.

- [ ] **Step 5: Comprobar que los tests del núcleo siguen pasando**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-tests.ps1"
```

Esperado: `0 failed` y código de salida 0. El núcleo no debe haberse tocado.

- [ ] **Step 6: Commit**

```bash
git add src/Game/SlotMapping.cs src/Game/ItemClassifier.cs src/Game/HeroAdapter.cs
git commit -m "feat(game): add adapters from Bannerlord types to core types"
```

---

### Task 10: Catálogo de ítems y concesión gratuita

**Files:**
- Create: `src/Game/ItemCatalog.cs`
- Create: `src/Game/GrantService.cs`

**Interfaces:**
- Consumes: adaptadores de la tarea 9, `LoadoutPlanner`, `TierCeiling`.
- Produces:
  - `static ItemObject ItemCatalog.FindBest(WeaponCategory category, CultureObject culture, int maxTier, SkillProfile skills, Hero hero, bool mounted)`
  - `static ItemObject ItemCatalog.FindBestArmor(ItemObject.ItemTypeEnum wanted, CultureObject culture, int maxTier)`
  - `static MountedRangedAvailability ItemCatalog.RangedAvailability(Hero hero, CultureObject culture, int maxTier)`
  - `static bool GrantService.NeedsGrant(Hero hero)`
  - `static void GrantService.Grant(Hero hero, float clanWeight, float skillWeight, int minimumTier, int dominanceMargin)`

- [ ] **Step 1: Escribir el catálogo**

Crear `src/Game/ItemCatalog.cs`:

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>Finds concrete items for a planned category.</summary>
    public static class ItemCatalog
    {
        /// <summary>
        /// The best item of a category the hero may have: within the tier ceiling,
        /// usable given their skills, culture-appropriate, and mount-compatible.
        /// Returns null when nothing qualifies; callers must tolerate that.
        /// </summary>
        public static ItemObject FindBest(WeaponCategory category, CultureObject culture,
                                          int maxTier, SkillProfile skills, Hero hero, bool mounted)
        {
            if (category == WeaponCategory.None) return null;

            ItemObject best = null;
            int bestTier = -1;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (!IsEligible(item, category, culture, maxTier, skills, hero, mounted)) continue;

                int tier = (int)item.Tier;
                if (tier > bestTier)
                {
                    bestTier = tier;
                    best = item;
                }
            }

            return best;
        }

        /// <summary>
        /// The filters every catalogue lookup shares: not a quest or crafted
        /// item, within the tier ceiling, and either the hero's culture or
        /// unassigned. One policy, so armour and weapons cannot drift apart.
        /// </summary>
        private static bool PassesCommonFilters(ItemObject item, CultureObject culture, int maxTier)
        {
            if (item == null) return false;
            if (item.NotMerchandise) return false;
            if (item.IsCraftedByPlayer) return false;
            if ((int)item.Tier > maxTier) return false;
            if (item.Culture != null && culture != null && item.Culture.StringId != culture.StringId) return false;
            return true;
        }

        private static bool IsEligible(ItemObject item, WeaponCategory category, CultureObject culture,
                                       int maxTier, SkillProfile skills, Hero hero, bool mounted)
        {
            if (!PassesCommonFilters(item, culture, maxTier)) return false;
            if (ItemClassifier.Classify(item) != category) return false;
            if (!ItemClassifier.MeetsDifficulty(item, skills)) return false;
            if (mounted && !ItemClassifier.IsUsableMounted(item, hero)) return false;
            return true;
        }

        /// <summary>
        /// The best armour of a given slot type within the ceiling. Armour has
        /// no difficulty gate and no mounted restriction, so it needs only the
        /// common filters.
        /// </summary>
        public static ItemObject FindBestArmor(ItemObject.ItemTypeEnum wanted, CultureObject culture, int maxTier)
        {
            ItemObject best = null;
            int bestTier = -1;

            MBReadOnlyList<ItemObject> all = MBObjectManager.Instance.GetObjectTypeList<ItemObject>();
            for (int i = 0; i < all.Count; i++)
            {
                ItemObject item = all[i];
                if (item == null || item.ItemType != wanted) continue;
                if (!PassesCommonFilters(item, culture, maxTier)) continue;

                int tier = (int)item.Tier;
                if (tier > bestTier) { bestTier = tier; best = item; }
            }

            return best;
        }

        /// <summary>
        /// Whether a bow or crossbow this hero could use mounted exists at all.
        /// Feeds the planner so an unusable category falls through to the next
        /// skill instead of leaving an empty slot.
        /// </summary>
        public static MountedRangedAvailability RangedAvailability(Hero hero, CultureObject culture, int maxTier)
        {
            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            bool bow = FindBest(WeaponCategory.Bow, culture, maxTier, skills, hero, true) != null;
            bool crossbow = FindBest(WeaponCategory.Crossbow, culture, maxTier, skills, hero, true) != null;
            return new MountedRangedAvailability(bow, crossbow);
        }
    }
}
```

- [ ] **Step 2: Escribir el servicio de concesión**

Crear `src/Game/GrantService.cs`:

```csharp
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using HeroLoadoutFixer.Core;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Repairs the vanilla come-of-age bug by handing a hero a coherent loadout
    /// for free. This is fixing a defect, not economy: it must work for a lord
    /// with no gold who never visits a town.
    /// </summary>
    public static class GrantService
    {
        /// <summary>The one-handed sword vanilla's dummy fallback hands out.</summary>
        private const string DummySwordId = "iron_spatha_sword_t2";

        /// <summary>
        /// True when the hero's battle equipment is empty, or is the vanilla
        /// dummy set: a lone spatha and nothing else in the weapon slots.
        /// </summary>
        public static bool NeedsGrant(Hero hero)
        {
            if (hero == null || hero.BattleEquipment == null) return false;

            int weapons = 0;
            bool onlyDummySword = true;

            for (int i = 0; i < SlotSnapshot.WeaponSlotCount; i++)
            {
                ItemObject item = hero.BattleEquipment[SlotMapping.WeaponSlot(i)].Item;
                if (item == null) continue;
                weapons++;
                if (item.StringId != DummySwordId) onlyDummySword = false;
            }

            if (weapons == 0) return true;
            return weapons == 1 && onlyDummySword;
        }

        public static void Grant(Hero hero, float clanWeight, float skillWeight,
                                 int minimumTier, int dominanceMargin)
        {
            if (hero == null || hero.BattleEquipment == null) return;

            SkillProfile skills = HeroAdapter.ReadSkills(hero);
            SlotSnapshot current = HeroAdapter.ReadEquipment(hero.BattleEquipment);

            int clanTier = hero.Clan != null ? hero.Clan.Tier : 0;
            int ceiling = TierCeiling.Compute(clanTier, skills.MaxCombatSkill, clanWeight, skillWeight, minimumTier);

            CultureObject culture = hero.Culture;
            if (culture == null && hero.Clan != null) culture = hero.Clan.Culture;

            bool cultureMounted = HeroAdapter.CultureFieldsMountedElites(hero);
            MountedRangedAvailability availability = ItemCatalog.RangedAvailability(hero, culture, ceiling);

            List<PlannedSlot> plan = LoadoutPlanner.Plan(skills, current, availability, dominanceMargin, cultureMounted);
            bool mounted = current.HasMount;

            int granted = 0;
            foreach (PlannedSlot slot in plan)
            {
                ItemObject item = ItemCatalog.FindBest(slot.Category, culture, ceiling, skills, hero, mounted);
                if (item == null) continue;

                hero.BattleEquipment[SlotMapping.WeaponSlot(slot.SlotIndex)] =
                    new EquipmentElement(item, null, null, false);
                granted++;
            }

            granted += GrantArmor(hero, culture, ceiling);

            ModLog.Info("GRANT hero=" + hero.Name + " tier=" + ceiling
                        + " planned=" + plan.Count + " granted=" + granted);
        }

        private static int GrantArmor(Hero hero, CultureObject culture, int ceiling)
        {
            int granted = 0;

            foreach (EquipmentIndex slot in SlotMapping.ArmorSlots)
            {
                if (hero.BattleEquipment[slot].Item != null) continue;

                ItemObject item = FindArmorFor(slot, culture, ceiling);
                if (item == null) continue;

                hero.BattleEquipment[slot] = new EquipmentElement(item, null, null, false);
                granted++;
            }

            return granted;
        }

        /// <summary>Maps an armour slot to its item type and defers to the catalogue.</summary>
        private static ItemObject FindArmorFor(EquipmentIndex slot, CultureObject culture, int ceiling)
        {
            ItemObject.ItemTypeEnum wanted;
            switch (slot)
            {
                case EquipmentIndex.Head: wanted = ItemObject.ItemTypeEnum.HeadArmor; break;
                case EquipmentIndex.Body: wanted = ItemObject.ItemTypeEnum.BodyArmor; break;
                case EquipmentIndex.Leg: wanted = ItemObject.ItemTypeEnum.LegArmor; break;
                case EquipmentIndex.Gloves: wanted = ItemObject.ItemTypeEnum.HandArmor; break;
                default: wanted = ItemObject.ItemTypeEnum.Cape; break;
            }

            return ItemCatalog.FindBestArmor(wanted, culture, ceiling);
        }
    }
}
```

- [ ] **Step 3: Compilar**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-mod.ps1"
```

Esperado: `DEPLOYED`.

- [ ] **Step 4: Commit**

```bash
git add src/Game/ItemCatalog.cs src/Game/GrantService.cs
git commit -m "feat(game): add item catalogue lookup and free grant service"
```

---

### Task 11: Comportamiento de campaña y verificación en juego

**Files:**
- Create: `src/HeroLoadoutBehavior.cs`
- Modify: `src/SubModule.cs`
- Create: `build/verify-api.ps1`

**Interfaces:**
- Consumes: `GrantService`.
- Produces: un mod que arregla el bug de vanilla en partida.

- [ ] **Step 1: Escribir el comportamiento**

Crear `src/HeroLoadoutBehavior.cs`:

```csharp
using TaleWorlds.CampaignSystem;

namespace HeroLoadoutFixer
{
    /// <summary>
    /// Subscribes to the two events that matter for the grant path and does the
    /// per-hero work. Every hero is wrapped in try/catch: one bad hero must
    /// never take down the tick.
    /// </summary>
    public class HeroLoadoutBehavior : CampaignBehaviorBase
    {
        // Defaults from the design spec, section 6 and 11.
        private const float ClanWeight = 0.5f;
        private const float SkillWeight = 1.0f;
        private const int MinimumTier = 1;
        private const int DominanceMargin = 30;

        public override void RegisterEvents()
        {
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
            CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, OnDailyTickHero);
        }

        /// <summary>Nothing is stored in the save. Deliberately empty.</summary>
        public override void SyncData(IDataStore dataStore) { }

        private void OnHeroComesOfAge(Hero hero)
        {
            TryRepair(hero, "came_of_age");
        }

        private void OnDailyTickHero(Hero hero)
        {
            TryRepair(hero, "daily_tick");
        }

        private void TryRepair(Hero hero, string reason)
        {
            try
            {
                if (!IsEligible(hero)) return;
                if (!GrantService.NeedsGrant(hero)) return;

                ModLog.Info("REPAIR hero=" + hero.Name + " reason=" + reason);
                GrantService.Grant(hero, ClanWeight, SkillWeight, MinimumTier, DominanceMargin);
            }
            catch (System.Exception ex)
            {
                ModLog.Error("TryRepair failed for "
                             + (hero == null ? "<null>" : hero.Name.ToString())
                             + ": " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static bool IsEligible(Hero hero)
        {
            if (hero == null) return false;
            if (hero.IsDead) return false;
            if (hero.IsHumanPlayerCharacter) return false;
            if (hero == Hero.MainHero) return false;
            if (hero.IsChild) return false;
            if (!hero.IsLord) return false;
            return true;
        }
    }
}
```

- [ ] **Step 2: Registrar el comportamiento**

Reemplazar `src/SubModule.cs` por:

```csharp
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace HeroLoadoutFixer
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            ModLog.Info("SubModule loaded.");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            CampaignGameStarter starter = gameStarterObject as CampaignGameStarter;
            if (starter == null) return;

            starter.AddBehavior(new HeroLoadoutBehavior());
            ModLog.Info("HeroLoadoutBehavior registered.");
        }
    }
}
```

- [ ] **Step 3: Escribir el verificador de API**

Este paso es obligatorio: DynamicLordGear quedó inservible en v1.4.8 exactamente por saltárselo.

Crear `build/verify-api.ps1`:

```powershell
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$game = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord"
$dll  = Join-Path $root "build\out\HeroLoadoutFixer.dll"

if (-not (Test-Path $dll)) { Write-Host "Build the mod first."; exit 1 }

Add-Type -AssemblyName System.Reflection.Metadata | Out-Null
Add-Type -AssemblyName System.Collections.Immutable | Out-Null

function Get-Members([string]$path) {
  $fs = [System.IO.File]::OpenRead($path)
  try {
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($fs)
    $mr = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    foreach ($th in $mr.TypeDefinitions) {
      $td = $mr.GetTypeDefinition($th)
      $ns = $mr.GetString($td.Namespace); $tn = $mr.GetString($td.Name)
      $full = if ($ns) { "$ns.$tn" } else { $tn }
      foreach ($mh in $td.GetMethods()) { "$full::" + $mr.GetString($mr.GetMethodDefinition($mh).Name) }
      foreach ($fh in $td.GetFields())  { "$full::" + $mr.GetString($mr.GetFieldDefinition($fh).Name) }
    }
    $pe.Dispose()
  } finally { $fs.Dispose() }
}

function Get-References([string]$path) {
  $fs = [System.IO.File]::OpenRead($path)
  try {
    $pe = [System.Reflection.PortableExecutable.PEReader]::new($fs)
    $mr = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    foreach ($h in $mr.MemberReferences) {
      $m = $mr.GetMemberReference($h)
      $p = $m.Parent
      if ($p.Kind -ne [System.Reflection.Metadata.HandleKind]::TypeReference) { continue }
      $tr = $mr.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$p)
      $ns = $mr.GetString($tr.Namespace); $tn = $mr.GetString($tr.Name)
      $owner = if ($ns) { "$ns.$tn" } else { $tn }
      if ($owner -notmatch '^TaleWorlds') { continue }
      "$owner::" + $mr.GetString($m.Name)
    }
    $pe.Dispose()
  } finally { $fs.Dispose() }
}

$known = @{}
Get-ChildItem (Join-Path $game "bin\Win64_Shipping_Client") -Filter "TaleWorlds*.dll" |
  ForEach-Object { Get-Members $_.FullName } |
  ForEach-Object { $known[$_] = $true }

$missing = Get-References $dll | Sort-Object -Unique | Where-Object { -not $known.ContainsKey($_) }

if ($missing) {
  Write-Host "MISSING FROM v1.4.8:"
  $missing | ForEach-Object { Write-Host "  $_" }
  exit 1
}
Write-Host "API OK: every TaleWorlds member referenced exists in v1.4.8"
exit 0
```

- [ ] **Step 4: Compilar y verificar la API**

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\build-mod.ps1"
```

```bash
pwsh -File "E:\Games\Mods\Mis mods\HeroLoadoutFixer\build\verify-api.ps1"
```

Esperado: `API OK: every TaleWorlds member referenced exists in v1.4.8`. Si sale `MISSING FROM v1.4.8`, corregir cada miembro listado antes de continuar: eso es exactamente el fallo que dejó inservible a DynamicLordGear.

- [ ] **Step 5: Comprobar en juego**

Arrancar una campaña con el mod activado y avanzar unos días. Luego:

```bash
findstr /C:"REPAIR" /C:"GRANT" /C:"ERROR" "%LocalAppData%\Mount and Blade II Bannerlord\logs\hlf.log"
```

Esperado: líneas `REPAIR` seguidas de `GRANT` para los héroes que estaban con el set dummy, y ninguna línea `ERROR`. Un `granted=0` repetido significa que `ItemCatalog.FindBest` no encuentra nada: revisar el filtro de cultura y el techo de tier.

- [ ] **Step 6: Commit**

```bash
git add src/HeroLoadoutBehavior.cs src/SubModule.cs build/verify-api.ps1
git commit -m "feat: wire the grant path to campaign events with API verification"
```

---

## Alcance de este plan

Este plan cubre las **fases 1 y 2** de la especificación y termina con un mod instalable que arregla el bug de vanilla por sí solo.

Las fases 3 y 4 —el motor de compra y venta, el ledger de gasto por clan, la reserva de seguridad aplicada en juego, y los ajustes MCM— van en un plan aparte, que se escribirá cuando este esté ejecutado y probado en campaña. Motivo: el diseño del motor de compra depende de cómo se comporte `ItemCatalog` con datos reales, y escribirlo ahora sería especular.

`BudgetMath` se adelantó a la tarea 7 porque es aritmética pura y pertenece al núcleo testeable; queda listo y probado para que el plan siguiente lo consuma.
