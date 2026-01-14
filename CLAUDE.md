# AutoSummon - tModLoader Mod

A tModLoader mod for Terraria that automatically summons and manages minions and sentries.

## Project Overview

- **Mod Name**: Kunaii's Auto Summon
- **Framework**: tModLoader (Terraria mod framework)
- **Target**: .NET 8.0
- **Platforms**: AnyCPU, x64

## Core Features

- **Auto-summoning**: Automatically summons configured minions and sentries on respawn/world entry
- **Draggable UI Panel**: Toggle with keybind (default: K) to configure summons
- **Slot Management**: Supports multiple minion/sentry types with quantity controls (+1, -1, Fill)
- **Sentry Respawn**: Optional feature to respawn sentries when they go off-screen
- **Persistence**: Saves/loads panel configurations to JSON file per player

## Project Structure

```
AutoSummon/
├── AutoSummon.cs           # Main ModSystem - UI initialization and keybind handling
├── AutoSummonPlayer.cs     # ModPlayer - Save/load logic, respawn handling
├── AutoSummonSystem.cs     # ModSystem - Summon maintenance and slot tracking
├── DraggableUIPanel.cs     # UI/UIState - Main UI panel with all controls
├── CustomItemSlot.cs       # Custom item slot component (uses CustomSlot library)
├── CroppedTexture2D.cs     # Texture utility
├── CustomEventArgs.cs      # Custom event types
├── Content/
│   └── Items/
│       └── autosummon.cs   # Mod item(s)
└── Assets/
    └── UI/
        ├── RefreshButton.png
        ├── RespawnSentry.png
        └── NoRespawnSentry.png
```

## Key Classes

### AutoSummon.cs (ModSystem)
- Manages UI lifecycle and keybind registration
- Handles UI drawing and updates
- Static reference to `DraggableUIPanelInstance`

### AutoSummonPlayer.cs (ModPlayer)
- Saves/loads panel data to `ModConfigs/AutoSummonPanels.json`
- Handles respawn logic to re-summon minions/sentries
- Manages sentry refresh when off-screen (if enabled)

### AutoSummonSystem.cs (ModSystem)
- `PostUpdateEverything()`: Monitors slot changes and maintains summons
- `SummonWithItem()`: Static method to spawn minion/sentry projectiles
- Handles both minion and sentry summoning logic

### DraggableUIPanel.cs (UIState)
- Main UI with header bar (draggable)
- Creates interaction panels for minions and sentries
- Quantity controls: -1, +1, Fill/Unfill buttons
- Validates items (minion vs sentry summon weapons)
- `RefreshSummons()`: Clears and re-summons all configured summons

## Dependencies

- **CustomSlot**: External library for custom item slot UI (`CustomSlot.UI`)
- **Humanizer**: Text manipulation library
- **Newtonsoft.Json**: JSON serialization for save/load

## Data Persistence

Config saved to: `{Terraria Save Path}/ModConfigs/AutoSummonPanels.json`

```json
{
  "PlayerName": "...",
  "MinionItems": [{ "Mod": "...", "Name": "...", "Type": 0, "Stack": 1, "Quantity": 3 }],
  "SentryItems": [...]
}
```

## UI Keybind

- **Toggle UI**: K (configurable in mod settings)

## Development Notes

- UI uses tModLoader's `UIState`, `UIPanel`, `UIText`, `UIImageButton` components
- Item validation distinguishes minions (`projectile.minion`) from sentries (`projectile.sentry`)
- Sentry respawn toggle is controlled via `respawnSentriesEnabled` static property
- Summons are refreshed whenever quantities change or items are added/removed
