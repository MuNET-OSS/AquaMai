# AquaMai.Core

Runtime core — startup orchestration, shared helpers, i18n, and infrastructure patches.

## STRUCTURE

```
AquaMai.Core/
├── Startup.cs          # Main init — config load, patch collection, lifecycle orchestration
├── ConfigLoader.cs     # Loads AquaMai.toml, triggers migration, saves config
├── Extensions.cs       # Extension methods
├── BuildInfo.cs        # Runtime build info fields (populated via reflection from AquaMai.BuildInfo)
├── Attributes/         # [EnableIf], [EnableGameVersion], [EnableImplicitlyIf]
├── Helpers/            # Shared infrastructure (see below)
├── Resources/          # i18n — Locale.resx (en) + Locale.zh.resx, single-assembly hook
└── Types/              # Interfaces: IPersistentStorage, IPlayerSettingsItem
```

## KEY HELPERS

| Helper | Purpose | Notes |
|--------|---------|-------|
| `EnableConditionHelper` | Evaluates `[EnableIf]`/`[EnableGameVersion]` | Patched first in startup |
| `SharedInstances` | Caches game manager references | Static shared state |
| `MessageHelper` | In-game message display | |
| `MusicDirHelper` | Music directory resolution | |
| `GuiSizes` | IMGUI sizing/styling | |
| `KeyListener` | Keyboard input hooks | |
| `Shim` | Compatibility shims across game versions | |
| `NetPacketHook` | Network packet interception | |
| `ErrorFrame` | Error display frame | |
| `GameSettingsManager` | Game settings override | **Patch on-demand only** — not in startup |
| `JvsSwitchHook` | JVS cabinet switch hooks | **Patch on-demand only** — not in startup |
| `JsonHelper` | JSON serialization utilities | |
| `FileSystem` | File system helpers | |
| `GameInfo` | Game version/info detection | |

## WHERE TO LOOK

| Task | File |
|------|------|
| Change startup order | `Startup.cs` — `Initialize()` method |
| Add core helper | `Helpers/` — follow existing static class pattern |
| Add i18n string | `Resources/Locale.resx` + `Resources/Locale.zh.resx` |
| Add enable condition | `Attributes/` — new attribute + check in `EnableConditionHelper` |
| Modify config loading | `ConfigLoader.cs` |

## NOTES

- Helpers listed in `Startup.cs` lines 188-200 are patched before mod patches — order matters
- `GameSettingsManager` and `JvsSwitchHook` must NOT be added to startup collection (patch lazily)
- `BuildInfo` fields are set via reflection from `AquaMai/Main.cs` — not compiled directly
- Lifecycle method parameter injection only supports `HarmonyLib.Harmony` type
