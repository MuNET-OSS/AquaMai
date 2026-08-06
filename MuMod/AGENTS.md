# MuMod

Separate MelonLoader auto-updater mod. Downloads, signature-verifies, and loads the latest AquaMai.dll at boot — independent of the AquaMai solution. Bilingual error messages (zh/en) via `ErrorOverlay`.

## STRUCTURE

```
MuMod/
├── Main.cs                # MelonMod entry — early-init fetch/verify/cache/load pipeline
├── Models/                # AquaMaiVersionInfo (API payload), MuModConfig (MuMod.toml)
├── Utils/
│   ├── VersionApi.cs      # Dual-source (COS + Cloudflare) version fetch, race to fastest
│   ├── AquaMaiSignatureV2.cs  # ECDSA-P521 signature verify via raw BCrypt (Unity Mono shim)
│   ├── ConfigManager.cs   # MuMod.toml load + path/channel resolution
│   ├── ErrorOverlay.cs    # Full-screen error block + IMGUI render
│   └── TomletShim.cs      # Tomlet wrapper
└── MuMod.toml             # Runtime config (Channel, CachePath); see MuMod.example.toml
```

## PIPELINE (`Main.OnEarlyInitializeMelon`)

1. `ConfigManager.Load()` → resolve channel (`fast`→`ci`, `slow`→`slow`)
2. `VersionApi.GetVersionInfo(channel)` — races COS + CF, first response wins
3. With version: load cache → mismatch/absent → download → `VerifySignature` → cache
4. Without version: try cache (signature-validated), else show error
5. `LoadAssembly`: `Assembly.Load` → `MelonAssembly` → register melons

## CONVENTIONS

- **Cache**: `LocalAssets\MuMod.cache` (configurable). Signature must be valid — else deleted + re-downloaded.
- **Config** via `Samboy063.Tomlet` (only NuGet dep) — no shared AquaMai config types.
- **Errors**: always bilingual, `ErrorOverlay.SetError(...)`; `ErrorOverlay.BlockGame` halts boot on failure.
- **Channel values**: `fast` = CI builds, `slow` = stable.

## KEY RULES

- **Do NOT** reference AquaMai source types — fully independent project.
- `codePage 65001` set at early init so Chinese output isn't garbled.
- `AquaMaiSignatureV2` bypasses .NET `ECDsa` (unreliable on Unity Mono) → P/Invoke `bcrypt.dll` ECDSA_P521 directly.
- Signature is a trailing `AquaMaiSignatureBlock` (magic `"AquaMaiSig"`, version 1, KeyId + 132-byte sig) attached by the CI signing step.