# STS2 Advisor

A lightweight, **read-only** mod for [Slay the Spire 2](https://store.steampowered.com/app/2868840/Slay_the_Spire_2/) that exposes game state via a simple HTTP API. Designed for AI advisors to read current state without taking control of gameplay.

## Features

- 🔒 **Read-only** - No game manipulation, just state observation
- ⚡ **Lightweight** - Minimal overhead, focused endpoints
- 🎯 **Advisory focused** - Get exactly the info needed for deck/combat advice
- 🏷️ **Multiplayer safe** - `affects_gameplay: false` means no multiplayer restrictions

## Installation

1. Download `STS2Advisor.dll` and `STS2Advisor.json` from releases
2. Copy to your game's `mods/STS2Advisor/` folder:
   - **Windows**: `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\STS2Advisor\`
   - **macOS**: `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/SlayTheSpire2.app/Contents/MacOS/mods/STS2Advisor/`
   - **Linux**: `~/.steam/steam/steamapps/common/Slay the Spire 2/mods/STS2Advisor/`
3. Launch the game and enable mods in Settings → Mod Settings

## API Endpoints

All endpoints are **GET only** and return JSON.

| Endpoint | Description |
|----------|-------------|
| `GET /` | API info and available endpoints |
| `GET /state` | Full game state snapshot |
| `GET /combat` | Combat state: hand, enemies, energy, status effects |
| `GET /deck` | Current deck with card breakdown |
| `GET /shop` | Shop inventory (when in shop) |
| `GET /card-reward` | Card reward options (when picking cards) |
| `GET /relics` | Current relics |
| `GET /map` | Map state and next options |

Default port: `15526` (configurable via `STS2Advisor.conf`)

## Example Usage

```bash
# Get current combat state
curl http://localhost:15526/combat

# Get card reward options
curl http://localhost:15526/card-reward

# Get shop inventory
curl http://localhost:15526/shop
```

### Example Response: Card Reward

```json
{
  "cards": [
    {
      "name": "Pommel Strike+",
      "type": "Attack",
      "rarity": "Common",
      "energy_cost": "1",
      "description": "Deal 10 damage. Draw 2 cards.",
      "is_upgraded": true,
      "keywords": ["Draw"]
    },
    {
      "name": "Shrug It Off",
      "type": "Skill",
      "rarity": "Common",
      "energy_cost": "1",
      "description": "Gain 8 Block. Draw 1 card.",
      "is_upgraded": false,
      "keywords": ["Block", "Draw"]
    }
  ],
  "can_skip": true
}
```

### Example Response: Combat

```json
{
  "round": 2,
  "turn": "player",
  "is_play_phase": true,
  "energy": 3,
  "max_energy": 3,
  "hp": 65,
  "max_hp": 80,
  "block": 5,
  "hand": [
    {
      "index": 0,
      "name": "Strike",
      "type": "Attack",
      "energy_cost": "1",
      "target": "AnyEnemy",
      "can_play": true
    }
  ],
  "enemies": [
    {
      "id": "jaw_worm_0",
      "name": "Jaw Worm",
      "hp": 32,
      "max_hp": 44,
      "block": 0,
      "intents": ["Attack 12"]
    }
  ],
  "draw_pile_count": 5,
  "discard_pile_count": 3
}
```

## Configuration

Create `STS2Advisor.conf` in the mod folder to change the port:

```json
{
  "port": 15526
}
```

## Building from Source

Requires:
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Slay the Spire 2 installed

```bash
# Update paths in STS2Advisor.csproj for your system, then:
dotnet build STS2Advisor.csproj
```

The DLL will be copied to your mods folder automatically.

## Integration with AI Assistants

This mod is designed to work with AI assistants like Claude. The AI can poll these endpoints to understand your current game state and provide deck-building advice, combat suggestions, and card pick recommendations—without taking control of your game.

### OpenClaw Skill Example

```yaml
# In your SKILL.md
commands:
  - curl http://localhost:15526/combat   # Get combat state
  - curl http://localhost:15526/card-reward  # Get card choices
  - curl http://localhost:15526/shop  # Get shop items
```

## License

MIT

## Credits

Based on [STS2MCP](https://github.com/Gennadiyev/STS2MCP) by Gennadiyev. Stripped down to read-only advisory functionality.
