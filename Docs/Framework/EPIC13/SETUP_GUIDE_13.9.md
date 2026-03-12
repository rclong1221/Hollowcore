# Setup Guide: EPIC 13.9 (Resource Migration)

## Overview
EPIC 13.9 unifies the resource interaction system with the generic interaction system from EPIC 13.8. This guide explains how to set up resource nodes using the new unified components.

## Migration Steps

If you have existing resource node prefabs:

1. **Remove Legacy Components**:
   - Remove any old `Interactable` authoring/components related to *Resources* (check `DIG.Survival.Resources` namespace).

2. **Add New Authoring**:
   - Add `InteractableAuthoring`
   - Add `ResourceAuthoring`

## Setting up a New Resource Node

1. Create or open your Resource Node prefab (e.g., `Rock_Node`).
2. Add **`InteractableAuthoring`**:
   - **Type**: `Timed` (Resources typically require holding a button)
   - **Message**: "Hold E to Collect" (or similar)
   - **Interaction Radius**: `2` (or appropriate size)
   - **Requires Hold**: `True`
   - **Hold Duration**: `1.0` (This acts as a fallback, but `ResourceAuthoring` collection time usually overrides logic in the system)

3. Add **`ResourceAuthoring`**:
   - **Resource Type**: Select the type (e.g., `Stone`)
   - **Max Amount**: `10` (Total yield)
   - **Amount Per Collection**: `1`
   - **Collection Time**: `1.0` (Time per unit)
   - **Requires Tool**: `True`/`False` based on design
   - **Respawn Time**: `0` for no respawn, or `>0` for respawning nodes (e.g. `60`)

## Verification

1. **In-Editor**:
   - Ensure the entity has `ResourceInteractable` and `Interactable` components baked.
   
2. **Play Mode**:
   - Approach the node.
   - You should see the interaction prompt (from EPIC 13.8 UI).
   - Hold Interaction Key.
   - Verify resources are added to specific inventory buffer.
   - Verify node depletes and prompt disappears when empty.
   - If respawn time is set, wait and verify it replenishes.
