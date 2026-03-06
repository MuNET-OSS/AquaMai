# AquaMai.Mods

All mod patches live here, organized by category. Each category has a README defining its scope.

## STRUCTURE

```
AquaMai.Mods/
├── General.cs          # General settings section + SectionNameOrder enum
├── DeprecationWarning.cs  # Warns about deprecated config keys
├── Fix/                # Bug fixes — defaultOn, no side-effects
│   ├── Stability/      # Crash prevention
│   └── Legacy/         # Version-specific fixes
├── GameSystem/         # Behavior changes not possible in stock game
│   ├── Assets/         # Asset loading (bundles, fonts, images, movies)
│   ├── ExclusiveTouch/ # USB touch panel support
│   └── MaimollerIO/    # Custom IO board support
├── GameSettings/       # Override existing configurable settings
├── Tweaks/             # Stability/robustness patches (no behavior change)
│   └── TimeSaving/     # Skip screens, reduce wait times
├── UX/                 # User experience improvements
│   └── PracticeMode/   # Practice mode with custom UI
├── Utils/              # Debug/diagnostic tools — no gameplay impact
├── Fancy/              # Advanced/personalization features — less tested
│   └── GamePlay/       # Gameplay-affecting fancy features
│       └── CustomNoteTypes/  # Custom slide note types (complexity hotspot)
├── Enhancement/        # Server-side enhancements (announcements, resources)
└── Types/              # Shared types (ConditionalMessage, etc.)
```

## HOW TO ADD A NEW MOD

1. Create `{Category}/{ModName}.cs`
2. Add `[ConfigSection]` attribute on class (set `name`, `en`, `zh`)
3. Add config fields as `public static readonly` with `[ConfigEntry]`
4. Write Harmony patches as static methods with `[HarmonyPrefix/Postfix]` + `[HarmonyPatch]`
5. Optional lifecycle: `OnBeforePatch()`, `OnAfterPatch()`, `OnBeforeEnableCheck()`
6. Update `SectionNameOrder` enum in `General.cs` if adding a new top-level section

## CATEGORY PLACEMENT RULES

| Category | Scope | Default | Key Rule |
|----------|-------|---------|----------|
| **Fix** | Game bugs, annoying features | ON, hidden | No negative/visual side-effects |
| **GameSystem** | New behaviors impossible in stock | OFF | Asset patches → `Assets/` subdir |
| **GameSettings** | Override existing game settings | OFF | Stock-configurable only; else → GameSystem |
| **Tweaks** | Stability, less annoying | OFF | Must NOT change game behavior |
| **TimeSaving** | Skip screens/waits | OFF | Subset of Tweaks |
| **UX** | User-triggered experience | OFF | User must perceive or trigger it |
| **Utils** | Debug/diagnostics | OFF | No gameplay impact, may show UI |
| **Fancy** | Personalization, uncommon | OFF | Not well-tested, advanced users only |
| **Enhancement** | Server integrations | OFF | Requires server-side support |

## CONVENTIONS

- One class = one feature (with nested helper classes allowed)
- Namespace matches directory: `AquaMai.Mods.Fix`, `AquaMai.Mods.Tweaks.TimeSaving`
- `[EnableIf(nameof(field))]` for conditional patches within a class
- `[EnableGameVersion]` for version-specific patches
- Lifecycle methods are static, discovered by exact name match via reflection
- Harmony `__result` + `return false` pattern for prefix patches that replace return values
