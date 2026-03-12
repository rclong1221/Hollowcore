# EPIC 14.6 Setup Guide - Equipment System Tooling

## Setup Wizard

### Workflow Overview
1. **First Time Setup**: Run the **Equipment Setup Wizard** (Configures slots, categories, player).
2. **Adding Content**: Run the **Weapon Creation Wizard** (Creates new weapons).
3. **Utilities**: Use utility commands for advanced/manual tweaks.

The Equipment Setup Wizard provides a guided workflow for configuring the equipment system in your project.

### How to Open
### How to Open
`DIG > Wizards > 1. Setup: Project Configuration`

### Wizard Steps

#### Step 1: Game Type Selection
Choose a template that matches your game:

| Template | Created Slots | Created Categories |
|----------|--------------|-------------------|
| Third-Person Shooter | MainHand, OffHand | Gun, Pistol, Rifle, Knife, Grenade, Shield |
| Souls-like Action | MainHand, OffHand | Sword, Katana, Shield, Magic, Bow |
| Survival Game | MainHand, OffHand, Tool | Gun, Melee, Tool, Consumable |
| Full RPG | MainHand, OffHand, Head, Chest, Hands, Legs, Feet | All types |
| Custom | None | None |

#### Step 2: Animation Backend
Select your animation system:

- **Opsive UCC**: Uses WeaponEquipVisualBridge (existing Opsive integration)
- **Standard Mecanim**: Uses MecanimAnimatorBridge for direct Animator control
- **Custom**: Creates placeholder for custom implementation

#### Step 3: Input Configuration
Configure slot input bindings:

- **Default FPS**: Main Hand = 1-9, Off Hand = Shift+1-9
- **Gamepad-Friendly**: D-Pad for main, LB+D-Pad for off-hand
- **Custom**: No default bindings

#### Step 4: Folder Structure
Creates organized content folders:
```
Assets/Content/
├── Weapons/
│   ├── Categories/
│   ├── Prefabs/
│   └── InputProfiles/
└── Equipment/
    ├── Definitions/Slots/
    ├── Definitions/Categories/
    └── Configs/
```

#### Step 5: Player Prefab Configuration
- Assign your player prefab
- Automatically adds `DIGEquipmentProvider` component
- Assigns all created slot definitions

### After Setup

1. **Verify Slots**: Check `Assets/Content/Equipment/Definitions/Slots/` for slot assets
2. **Verify Categories**: Check `Assets/Content/Equipment/Definitions/Categories/` for category assets
3. **Test Player**: Enter Play Mode and verify the Equipment Debugger shows your slots
4. **Create Weapons**: Use the Weapon Creation Wizard (Phase 2) to add weapons

### Manual Configuration

If you chose "Custom" template or need additional slots:

1. Create new slot: `Assets > Create > DIG > Equipment > Slot Definition`
2. Configure properties:
   - **SlotID**: Unique identifier (e.g., "Helmet")
   - **SlotIndex**: Numeric index for ECS buffer
   - **AttachmentBone**: Which bone items attach to
   - **UsesNumericKeys**: Enable for 1-9 hotkey selection
   - **RequiredModifier**: None, Shift, Alt, or Ctrl

3. Assign new slot to player's `DIGEquipmentProvider._slotDefinitions` array
