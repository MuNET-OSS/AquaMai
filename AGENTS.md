# PROJECT KNOWLEDGE BASE

## OVERVIEW

AquaMai is a comprehensive Harmony-based mod suite for the Sinmai (maimai DX) arcade game, loaded via MelonLoader. C# / .NET Framework 4.7.2. Multi-project solution with embedded assembly loading.

## STRUCTURE

```
AquaMai/
├── AquaMai/                  # Entry point — MelonMod subclass, assembly loader
├── AquaMai.Core/             # Startup, helpers, lifecycle, i18n, shared state
├── AquaMai.Mods/             # All mod patches — categorized (Fix, UX, Tweaks, etc.)
├── AquaMai.Config/           # TOML config system — parse, serialize, migrate, reflect
├── AquaMai.Config.Interfaces/  # Abstraction layer for config (used by HeadlessLoader)
├── AquaMai.Config.HeadlessLoader/  # Config loading without game runtime
├── AquaMai.Build/            # Build-time tools — example config gen, post-build patching
├── AquaMai.ErrorReport/      # Standalone crash report WinForms app
├── MuMod/                    # Separate auto-updater mod (downloads + signature-verifies AquaMai)
├── MelonLoader.TinyJSON/     # Vendored JSON lib (empty source, likely reference)
├── Libs/                     # Game DLLs (Assembly-CSharp, UnityEngine, etc.) — gitignored contents
├── Output/                   # Build artifacts
└── tools/                    # NuGet/Cake build tooling
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| Add new mod/feature | `AquaMai.Mods/{Category}/` | See category READMEs for placement rules |
| Modify startup/init | `AquaMai.Core/Startup.cs` | Patch collection + lifecycle orchestration |
| Config system changes | `AquaMai.Config/` | Has its own AGENTS.md |
| Add config entry | Target mod class — use `[ConfigEntry]` on `static readonly` field |
| Add config section | Add `[ConfigSection]` class + entry in `SectionNameOrder` enum in `General.cs` |
| Conditional enable | `[EnableIf]`, `[EnableGameVersion]` attributes from `AquaMai.Core.Attributes` |
| i18n strings | `AquaMai.Core/Resources/Locale.resx` + `Locale.zh.resx` |
| Build the project | `./build.ps1` → outputs `Output/AquaMai.dll` |
| Config migration | `AquaMai.Config/Migration/` — implement `IConfigMigration` |

## ARCHITECTURE

**Assembly loading**: `AquaMai.dll` is the MelonLoader entry. It embeds `Config.Interfaces`, `Config`, `Core`, `Mods` as compressed resources. `AssemblyLoader.cs` extracts and loads them at runtime via `AppDomain.CurrentDomain.Load()`.

**Mod lifecycle** (defined in `Startup.cs`):
1. `OnBeforeEnableCheck` — init fields for `[EnableIf]`
2. Config-based collection — sections enabled in `AquaMai.toml`
3. `OnBeforeAllPatch` → `OnBeforePatch` → `_harmony.PatchAll(type)` → `OnAfterPatch` → `OnAfterAllPatch`
4. `OnPatchError` on failure

**Config flow**: `AquaMai.toml` (TOML) → `ConfigView` → `ConfigMigrationManager` (version upgrade) → `ConfigParser.Parse()` → reflection-based field population → re-serialize on save.

## CONVENTIONS

- **Mod pattern**: One static class per feature, decorated with `[ConfigSection]`. Harmony patches as nested `[HarmonyPatch]` methods. Config as `static readonly` fields with `[ConfigEntry]`.
- **Lifecycle hooks**: Static methods named exactly `OnBeforePatch`, `OnAfterPatch`, etc. — discovered by reflection.
- **Namespace**: `AquaMai.Mods.{Category}` matching directory structure.
- **Config bilingual**: All `[ConfigEntry]` and `[ConfigSection]` should have both `en:` and `zh:` descriptions.
- **SectionNameOrder**: When adding/removing sections, update the enum in `General.cs`.
- **Fix patches**: Must be `defaultOn: true, exampleHidden: true` — no negative side-effects.
- **No DI**: Static classes + shared instances (`SharedInstances` helper). No IoC container.
- **Fody**: Used in `AquaMai.Mods` for IL weaving (`FodyWeavers.xml`).

## ANTI-PATTERNS (THIS PROJECT)

- **Don't patch in General.cs** — settings only, no Harmony patches.
- **Don't add GameSettingsManager/JvsSwitchHook to startup collection** — patch on-demand only (see comment in `Startup.cs` line 198-200).
- **Tweaks must not change game behavior** — if they do, move to `GameSystem`.
- **GameSettings vs GameSystem**: GameSettings = override existing configurable settings; GameSystem = new behavior not possible in stock.
- **Fix patches must have no visual side-effects** on the original game.
- **Fancy patches are not well-tested** — enable only if you know what you're doing.
- **Don't use `hideWhenDefault` to hide useful options** — only truly unused ones.
- **Don't use `alwaysEnabled`** — reserved for `General` section only.

## COMMANDS

```bash
# Build (Release)
./build.ps1

# Build (Debug)
./build.ps1 -Configuration Debug

# Build + copy to game + launch
./build-run.ps1

# Config env override
$env:AQUAMAI_CONFIG = "path/to/AquaMai.toml"
```

## NOTES

- Target framework is .NET Framework 4.7.2 (Unity Mono runtime)
- Game DLLs in `Libs/` are gitignored — copy from game installation manually
- `BuildInfo.g.cs` is auto-generated from `git describe` — don't edit manually
- CI builds on Windows via GitHub Actions, signs + uploads to cloud on main branch push
- No unit tests — testing is manual
- Config versions are migrated automatically (V1.0 → V2.4 chain in `Migration/`)
- `configSort.yaml` in `AquaMai/` defines TOML key ordering; `checkSort.py` validates it
