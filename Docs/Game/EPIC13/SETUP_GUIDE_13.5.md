# EPIC 13.5 Setup Guide: POI Prefab Creation, Landmark & Interactable Setup

**Status:** Planned
**Requires:** 13.1 (DistrictDefinitionSO), 13.2 (zone generation, ZoneSpawnPoint), Framework: Interaction/ (InteractableAuthoring, StationAuthoring)

---

## Overview

Configure Points of Interest that give each district its memorable locations and environmental storytelling. Two tiers: Landmark POIs (5-6 per district, named, fixed positions per topology variant) and Micro POIs (procedural environmental details from a weighted pool). This guide covers creating composition prefabs, configuring interactable POIs, setting up discovery radius, and authoring the MicroPOIPoolSO.

---

## Quick Start

### Prerequisites
| Object | Component | Purpose |
|--------|-----------|---------|
| DistrictDefinitionSO | EPIC 13.1 | References LandmarkPOIs and MicroPOIPool |
| Zone generation | EPIC 13.2 | Provides ZoneSpawnPoint (type=POI) and ZoneBoundary |
| Framework Interaction/ | InteractableAuthoring | Powers vendor, heal, lore terminal interactions |

### New Setup Required

| Asset | Location | Type |
|-------|----------|------|
| POIEnums.cs | `Assets/Scripts/District/POI/` | C# (enums) |
| LandmarkPOIDefinition.cs | `Assets/Scripts/District/POI/` | C# (struct) |
| MicroPOIPoolSO.cs | `Assets/Scripts/District/POI/` | C# (ScriptableObject) |
| POIComponents.cs | `Assets/Scripts/District/POI/` | C# (ECS) |
| LandmarkPlacementSystem.cs | `Assets/Scripts/District/POI/Systems/` | C# (ISystem) |
| MicroPOIPlacementSystem.cs | `Assets/Scripts/District/POI/Systems/` | C# (ISystem) |
| POIDiscoverySystem.cs | `Assets/Scripts/District/POI/Systems/` | C# (ISystem) |
| Landmark composition prefabs | `Assets/Prefabs/Districts/[Name]/Landmarks/` | Prefab |
| MicroPOIPoolSO per district | `Assets/Data/Districts/[Name]/POIs/` | ScriptableObject |

---

## 1. Author Landmark POI Definitions

Landmarks are defined inline in `DistrictDefinitionSO.LandmarkPOIs` array.

### 1.1 LandmarkPOIDefinition Fields
| Field | Type | Description | Default | Range |
|-------|------|-------------|---------|-------|
| LandmarkName | string | Memorable name (e.g., "Hologram Shrine Plaza") | "" | Required |
| Description | string | Short flavor text | "" | Recommended |
| Icon | Sprite | HUD/map icon | null | 64x64 |
| MinimapIcon | Sprite | Minimap variant | null | 32x32 |
| ZoneIndex | int | Zone where this landmark is located | 0 | Must be valid zone index |
| CompositionPrefab | GameObject | Scene composition prefab instantiated at anchor | null | Required |
| InteractionType | POIInteractionType | What happens when player interacts | None | Enum |
| InteractablePrefab | GameObject | Interactable object prefab (null = non-interactable) | null | Required if InteractionType != None |
| LoreEntryIds | int[] | Lore unlocked on first visit | [] | EPIC 9 integration |
| MinDistanceFromOtherLandmarks | float | Composition rule spacing | 20.0 | 5.0-100.0 |

### 1.2 POIInteractionType Reference
| Value | Name | Prefab Needs | Description |
|-------|------|-------------|-------------|
| 0 | None | No interactable | Visual only, environmental storytelling |
| 1 | Vendor | InteractableAuthoring + ShopUI | Opens shop UI |
| 2 | BodyShop | InteractableAuthoring + ChassisUI | Chassis repair / limb install |
| 3 | LoreTerminal | InteractableAuthoring + LoreUI | Displays lore text, grants intel |
| 4 | Workbench | StationAuthoring + CraftingUI | Crafting station |
| 5 | Stash | InteractableAuthoring + StorageUI | Player storage access |
| 6 | HealStation | InteractableAuthoring + HealLogic | Restores health/resources |
| 7 | QuestGiver | InteractableAuthoring + QuestUI | NPC quest interaction |
| 8 | EnvironmentHazard | InteractableAuthoring + HazardLogic | Lever, valve, etc. |

---

## 2. Create Landmark Composition Prefabs

### 2.1 Composition Prefab Structure
```
Landmark_HologramShrinePlaza (root)
  Visual/
    MainStructure (MeshRenderer)
    Decorations (MeshRenderer)
    Lighting (Light components)
    FX (ParticleSystem, optional)
  Interactable/                    (only if InteractionType != None)
    LoreTerminal_Interactable.prefab (nested prefab with InteractableAuthoring)
  Anchors/
    POIAnchor (empty, position marker)
    SpawnPoint_POI (SpawnPointMarker, for micro-POI avoidance)
  Discovery/
    DiscoveryTrigger (empty + POIDiscoveryRadius authoring)
```

### 2.2 Recommended Dimensions
| POI Size | Footprint | Height | Use For |
|----------|-----------|--------|---------|
| Small | 5x5m | 3m | Terminal, small shrine |
| Medium | 10x10m | 5m | Chapel, forge, market stall |
| Large | 20x20m | 10m | Plaza, temple, vault complex |

### 2.3 Art Guidelines
- Landmarks should be **visually distinct** from regular zone geometry
- Use unique materials, lighting, or color accents
- Landmark should be recognizable from 30m+ (player uses it as mental waypoint)
- Avoid reusing landmark geometry across districts

---

## 3. Create Interactable POI Prefabs

For landmarks with InteractionType != None, create a separate interactable prefab:

### 3.1 Interactable Prefab Stack
| Component | Required For | Notes |
|-----------|-------------|-------|
| InteractableAuthoring | All types | Framework interaction system |
| Collider (trigger) | All types | Player proximity detection |
| InteractionPromptUI reference | All types | "Press E to [action]" prompt |
| Type-specific logic | Per type | ShopManager, HealLogic, LoreDisplay, etc. |

### 3.2 Interaction Radius
| Setting | Default | Range | Notes |
|---------|---------|-------|-------|
| InteractionRadius | 3.0m | 1.0-5.0 | Distance at which "Press E" appears |
| InteractionDuration | 0.0s | 0.0-5.0 | 0 = instant, >0 = hold-to-interact |

---

## 4. Create MicroPOIPoolSO
**Create:** `Assets > Create > Hollowcore/District/Micro POI Pool`
**Recommended location:** `Assets/Data/Districts/[Name]/POIs/[Name]_MicroPOIPool.asset`

### 4.1 Pool-Level Fields
| Field | Type | Description | Default | Range |
|-------|------|-------------|---------|-------|
| PoolName | string | Identifier for debugging | "" | -- |
| DensityByZoneType | float[10] | Micro-POIs per 100 sq meters, indexed by ZoneType | all 0 | 0.0-5.0 |

### 4.2 Recommended Density Values
| ZoneType Index | Type | Density | Rationale |
|----------------|------|---------|-----------|
| 0 (Combat) | Combat | 1.0 | Moderate detail |
| 1 (Elite) | Elite | 0.8 | Slightly less clutter for readability |
| 2 (Boss) | Boss | 0.3 | Minimal, boss arena should be clear |
| 3 (Shop) | Shop | 2.0 | Rich environmental detail |
| 4 (Event) | Event | 1.5 | Atmospheric |
| 5 (Rest) | Rest | 2.5 | Highest detail, player lingers here |

### 4.3 MicroPOIEntry Fields
| Field | Type | Description | Default | Range |
|-------|------|-------------|---------|-------|
| Prefab | GameObject | Micro-POI prefab | null | Required |
| DisplayName | string | Debug name | "" | -- |
| SelectionWeight | float | Relative frequency | 1.0 | 0.1-10.0 |
| RequiresGround | bool | Raycast down to surface | true | -- |
| CanAttachToWall | bool | Raycast to wall surface | false | -- |
| MinSpacing | float | Minimum distance from same-type micro-POIs | 5.0 | 1.0-20.0 |
| AllowedZoneTypes | ZoneType[] | Zone type filter (empty = all) | [] | -- |

### 4.4 Example Micro-POI Entries (Necrospire)
| Name | Weight | RequiresGround | CanAttachToWall | MinSpacing |
|------|--------|----------------|-----------------|------------|
| Broken Terminal | 3.0 | true | false | 5.0 |
| Grief Totem | 2.0 | true | false | 8.0 |
| Biometric Scanner | 1.5 | false | true | 10.0 |
| Drone Nest | 1.0 | false | true | 15.0 |
| Flickering Hologram | 4.0 | true | false | 3.0 |

---

## 5. Configure POI Discovery

### 5.1 POIDiscoveryRadius Component
| Field | Description | Default | Range |
|-------|-------------|---------|-------|
| Radius | Distance at which POI transitions Undiscovered -> Discovered | 10.0 | 5.0-30.0 |

Add `POIDiscoveryRadius` authoring to landmark composition prefabs. When any player enters the radius, the POI is discovered and appears on the minimap.

### 5.2 POI State Machine
```
Undiscovered -> Discovered (player enters discovery radius)
Discovered -> Visited (player reaches POI position)
Visited -> Looted (player interacts and takes loot)
Visited -> Completed (player finishes interaction)
```

---

## Scene & Subscene Checklist

- [ ] 5-6 LandmarkPOIDefinition entries in DistrictDefinitionSO.LandmarkPOIs
- [ ] Composition prefab created per landmark with visual structure
- [ ] Interactable prefab created and wired for landmarks with InteractionType != None
- [ ] MicroPOIPoolSO created with 4-8 entries per district
- [ ] DensityByZoneType configured (non-zero for occupied zone types)
- [ ] POIDiscoveryRadius authoring on landmark composition prefabs
- [ ] MinDistanceFromOtherLandmarks set to prevent clustering

---

## Common Mistakes
| Mistake | Symptom | Fix |
|---------|---------|-----|
| InteractionType != None but InteractablePrefab is null | Validator ERROR, player sees landmark but cannot interact | Create and wire the interactable prefab |
| Two landmarks in same zone too close | Composition rule violation, landmarks overlap | Increase MinDistanceFromOtherLandmarks or move to different zones |
| ZoneIndex out of range | Validator ERROR, landmark placed in nonexistent zone | Ensure ZoneIndex < zone graph length |
| All DensityByZoneType values = 0 | No micro-POIs spawn anywhere | Set density > 0 for desired zone types |
| MinSpacing too large | Very few micro-POIs spawn (spacing constraint eliminates candidates) | Reduce MinSpacing or increase zone area |
| Missing POIDiscoveryRadius | POI never transitions to Discovered, never appears on map | Add POIDiscoveryRadius authoring to composition prefab |

---

## Verification

- [ ] 5-6 landmark POIs instantiated at district load with correct composition prefabs
- [ ] Landmark composition rule enforced (no two landmarks closer than MinDistance)
- [ ] Micro-POIs placed with density scaling by zone type
- [ ] MinSpacing enforced between same-type micro-POIs
- [ ] POIDiscoverySystem transitions Undiscovered -> Discovered when player approaches
- [ ] Discovered POIs appear on minimap with correct icon
- [ ] Interactable POIs respond to framework Interaction/ system
- [ ] POIPersistenceEntry buffer saved and restored on zone transitions
- [ ] Re-entering district restores POI states
- [ ] Lore entries unlocked on first landmark visit
