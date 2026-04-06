# STS2 Advisor - Agent Instructions

## Overview

This is a **read-only** mod for Slay the Spire 2 that exposes game state via HTTP API. It's designed for AI assistants to observe game state and provide advice, without taking control of gameplay.

## Key Files

- `STS2Advisor.cs` - HTTP server setup, endpoint handlers
- `STS2Advisor.StateBuilder.cs` - Game state extraction logic
- `STS2Advisor.csproj` - Build configuration
- `mod_manifest_advisor.json` - Mod metadata for StS2

## API Design Principles

1. **Read-only** - No POST endpoints, no game manipulation
2. **Lightweight** - Return only essential data for advisory use
3. **Multiplayer safe** - `affects_gameplay: false` so it works in multiplayer
4. **Focused endpoints** - Specific endpoints for specific contexts (combat, shop, cards)

## Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/state` | Full state snapshot - use when context is unknown |
| `/combat` | Hand, enemies, energy - for combat advice |
| `/shop` | Shop inventory - for purchase decisions |
| `/card-reward` | Card choices - for pick recommendations |
| `/deck` | Full deck list - for deck analysis |
| `/relics` | Current relics - for synergy analysis |
| `/map` | Map and path options - for routing decisions |

## Building

The project targets .NET 8 and uses Godot.NET.Sdk. Paths in the csproj need to be updated for each developer's system.

```bash
dotnet build STS2Advisor.csproj
```

Build automatically copies the DLL to the game's mods folder.

## Testing

1. Build and ensure DLL is in `mods/STS2Advisor/`
2. Launch Slay the Spire 2
3. Enable mods in Settings → Mod Settings
4. Start a run
5. `curl http://localhost:15526/state` to verify

## Code Style

- Use regions to organize code sections
- All game state access happens on main thread via `RunOnMainThread<T>()`
- Strip BBCode tags from text with `StripRichTextTags()`
- Use `SafeGetText()` for null-safe localized string access
