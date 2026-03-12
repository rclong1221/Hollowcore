# EPIC 15.21 Setup Guide: Input System & Keybinds

## 1. Input Asset Configuration
**Location**: `Assets/Settings/Input/DIGInputActions.inputactions`

This is the central configuration for all game inputs.

### Action Maps
- **Core**: Actions available in ALL gameplay modes (Movement, Jump, Interact).
- **Combat_Shooter**: Actions specific to Shooter/FPS mode.
- **Combat_MMO**: Actions specific to MMO/RPG mode.
- **Combat_ARPG**: Actions specific to Isometric ARPG mode.
- **UI**: Menu navigation inputs.

### Editing Inputs
1. Double-click `DIGInputActions.inputactions` to open the editor.
2. Select the relevant Map.
3. Add/Rename Actions or Change Bindings.
4. **Important**: If you add a new Action, you must regenerate the Keybind UI (see below).

## 2. Keybind UI Generator
We have a custom tool to verify input actions and generate the Keybind Settings UI.

**Menu Item**: `Tools > DIG > Generate Keybind UI`

### When to use it
- After adding a new Action to the Input Asset.
- After changing the name of an Action.
- If the Keybind UI Prefab (`Assets/Prefabs/UI/Keybinds/KeybindPanel.prefab`) is broken or needs refreshing.

### What it does
1. Scans `DIGInputActions` for all actions.
2. Creates `BindableAction` metadata.
3. Spawns a `KeybindRow` for each action in the `KeybindPanel` prefab.
4. Assigns them to the correct Paradigm Tab (Shooter/MMO/etc) based on which Map they belong to.

## 3. Scene Architecture
To ensure inputs work in a scene:

1. **ParadigmInputManager**: Ensure this component is on your `GameEntry` or `Player` object. It handles switching Action Maps when the game mode changes.
2. **PlayerInputReader**: Replaced the legacy version. Ensure the player entity has the `PlayerInput` component initialized by the ECS system.

## 4. Workflows for Designers

### Adding a New Ability Key
1. **Define**: Open `DIGInputActions`. Add "CastFireball" to `Combat_MMO` map. Bind to `key 'R'`.
2. **Generate**: Run `Tools > DIG > Generate Keybind UI`.
3. **Verify**: Run the game. Open Settings > Keybinds > MMO. You should see "CastFireball" listed with 'R'.
4. **Result**: The Input System now tracks this action using standard Unity events.

### Rebinding a Key
1. Open the game.
2. Press **F1** (or your Settings menu key).
3. Navigate to the Paradigm Tab (e.g., MMO).
4. Click the button next to the Action.
5. Press the new key.
6. The binding is saved to PlayerPrefs automatically.
