# AquaMai.Config

TOML-based configuration system with reflection-driven section/entry discovery, versioned migration, and bilingual serialization.

## STRUCTURE

```
AquaMai.Config/
├── Config.cs             # Main config class — SectionState/EntryState records
├── ConfigParser.cs       # Parses TOML ConfigView into Config object
├── ConfigSerializer.cs   # Serializes Config back to TOML (with comments, i18n)
├── ConfigView.cs         # TOML document abstraction layer
├── ApiVersion.cs         # Config API version constant
├── Utility.cs            # Shared utilities (logging delegate)
├── Attributes/           # [ConfigSection], [ConfigEntry], [ConfigCollapseNamespace], EnableCondition
├── Migration/            # Version chain: V1.0 → V2.0 → V2.1 → V2.2 → V2.3 → V2.4
├── Reflection/           # ReflectionManager, SystemReflectionProvider, MonoCecil provider
└── Types/                # Config value types (KeyCodeOrName, SoundChannel, IOKeyMap, etc.)
```

## WHERE TO LOOK

| Task | File |
|------|------|
| Add new config attribute | `Attributes/` — implement matching interface from `Config.Interfaces` |
| Add config migration | `Migration/` — new `IConfigMigration` impl + register in `ConfigMigrationManager` |
| Change TOML parsing | `ConfigParser.cs` |
| Change TOML output | `ConfigSerializer.cs` — respects `Options.Lang` for bilingual output |
| Add custom value type | `Types/` — may need parser/serializer support |
| Headless config access | `AquaMai.Config.HeadlessLoader` project (separate) |

## MIGRATION SYSTEM

- `ConfigMigrationManager` chains migrations sequentially
- Each migration implements `IConfigMigration` with source/target version
- Operates on `ConfigView` (TOML level) — not on typed Config object
- Old config backed up as `AquaMai.toml.old-v{version}.`

## REFLECTION SYSTEM

- `ReflectionManager` discovers `[ConfigSection]` classes + `[ConfigEntry]` fields from mods assembly
- `SystemReflectionProvider` — runtime reflection (used in game)
- `MonoCecilAssemblyReflectionProvider` — Mono.Cecil-based (used by HeadlessLoader + Build tools)
- Section paths derived from namespace: `AquaMai.Mods.Fix.DisableReboot` → `Fix.DisableReboot`

## NOTES

- Config is case-insensitive (uses `StringComparer.OrdinalIgnoreCase`)
- `[ConfigSection(alwaysEnabled: true)]` reserved for `General` only
- `[ConfigEntry(hideWhenDefault: true)]` — only for truly unused options
- Serializer outputs bilingual comments based on `Options.Lang`
- `Polyfills.cs` provides .NET compatibility shims for older framework target
