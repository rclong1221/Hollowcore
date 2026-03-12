# Setup Guide: EPIC 13.8 (Interaction System)

## Overview
EPIC 13.8 implements a generic world interaction system for doors, levers, pickups, and other interactables.

## Step 1: Enable Player Interaction

Add to player prefab:
1. Add `InteractAbility` component (via authoring or code)
2. Add `InteractRequest` component
3. Add `InteractionPrompt` component
4. Add `CanInteract` tag
5. Configure detection range and angle

## Step 2: Create Interactables

### Basic Interactable
1. Add `InteractableAuthoring` to object
2. Set **Type**: Instant, Timed, Toggle, Animated, Continuous
3. Set **Message**: "Press E to Open"
4. Set **Interaction Radius**: 2m default

### Door
1. Add `InteractableAuthoring` (Type = Animated)
2. Add `DoorAuthoring`
3. Configure:
   - **Open/Closed Angle**: 90/0 degrees
   - **Animation Duration**: 0.5s
   - **Auto-Close**: optional

### Lever
1. Add `InteractableAuthoring` (Type = Toggle)
2. Add `LeverAuthoring`
3. Configure:
   - **Target Object**: door or mechanism
   - **Target Event**: "Toggle"

## Interaction Types

| Type | Behavior |
|------|----------|
| Instant | Immediate effect on press |
| Timed | Hold for duration |
| Toggle | On/off state |
| Animated | Animation-driven (doors) |
| Continuous | Hold to use |

## UI Setup

1. Create Canvas with `InteractionPromptUI` component
2. Assign references: promptPanel, messageText, progressBar
3. Set fade speed and world offset

## Verification

1. Approach door, see "Open Door" prompt
2. Press interact, door swings open
3. Test timed interaction (hold button)
4. Test lever toggling connected door
