# EPIC 7.4: Strife Visual & Audio Identity

**Status**: Planning
**Epic**: EPIC 7 — Strife System
**Priority**: Medium — Polish layer for Strife presentation
**Dependencies**: EPIC 7.1 (Strife Card Data Model), EPIC 7.2 (Strife Application System)

---

## Overview

Each Strife card carries a distinct visual and audio identity so the player always knows which macro-crisis is active. This includes a per-card color theme applied to UI borders and world markers, a persistent ambient audio layer, particle effects in affected districts, district interaction markers for spatial awareness, and a boss clause preview displayed in the arena pre-fight staging area. All visual/audio systems run client-side only and bridge from ECS state to managed presentation.

---

## Component Definitions

### StrifeVisualState (IComponentData)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeVisualState.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Client-side singleton holding the resolved visual parameters for the active Strife card.
    /// Updated by StrifeVisualBridge when the active card changes.
    /// Not ghost-replicated — derived from ActiveStrifeState on each client.
    /// </summary>
    public struct StrifeVisualState : IComponentData
    {
        /// <summary>Active card ID (cached from ActiveStrifeState for client systems).</summary>
        public StrifeCardId ActiveCardId;

        /// <summary>Theme color (linear space) for UI tinting and world markers.</summary>
        public float4 ThemeColor;

        /// <summary>
        /// Hash of the particle effect prefab to spawn. Resolved by VFX pipeline.
        /// 0 = no particle effect.
        /// </summary>
        public int ParticleEffectHash;

        /// <summary>
        /// Hash of the UI border sprite. Resolved by managed UI system.
        /// 0 = default border.
        /// </summary>
        public int UIBorderHash;

        /// <summary>Whether the ambient audio layer is currently playing.</summary>
        public bool AmbientPlaying;

        /// <summary>Fade progress for card transition (0 = old card, 1 = new card).</summary>
        public float TransitionFade;
    }
}
```

### StrifeDistrictMarker (IComponentData)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeDistrictMarker.cs
using Unity.Entities;
using Unity.Mathematics;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Placed on world-space marker entities within districts that have an active
    /// Strife interaction. Spawned by StrifeMarkerSpawnSystem on district entry,
    /// destroyed on district exit.
    /// </summary>
    public struct StrifeDistrictMarker : IComponentData
    {
        public StrifeCardId CardId;
        public StrifeInteractionType InteractionType;
        public float3 WorldPosition;

        /// <summary>Pulse animation phase (0–1). Driven by StrifeMarkerAnimationSystem.</summary>
        public float PulsePhase;
    }
}
```

### StrifeBossPreview (IEnableableComponent)

```csharp
// File: Assets/Scripts/Strife/Components/StrifeBossPreview.cs
using Unity.Entities;

namespace Hollowcore.Strife
{
    /// <summary>
    /// Enableable component on the boss arena staging entity. Baked disabled.
    /// Enabled when the player enters the boss arena pre-fight zone.
    /// Read by StrifeBossPreviewBridge to display the clause preview UI.
    /// </summary>
    public struct StrifeBossPreview : IComponentData, IEnableableComponent
    {
        public StrifeBossClause Clause;
        public StrifeCardId CardId;
    }
}
```

---

## Per-Card Visual Identity Table

| Card | Theme Color (Hex) | Particle Effect | UI Border Style | Audio Layer |
|---|---|---|---|---|
| Succession War | #FF4444 (Crimson) | Sparks + bullet tracers | Jagged metallic | Distant gunfire, radio chatter |
| Signal Schism | #00FFCC (Cyan-Teal) | Static discharge, glitch particles | Flickering digital | Signal interference, data corruption hum |
| Plague Armada | #88FF00 (Toxic Green) | Drifting spore clouds | Organic membrane | Labored breathing, distant sirens |
| Gravity Storm | #AA66FF (Violet) | Floating debris, gravity distortions | Warped/bent frame | Deep resonant drone, metallic groaning |
| Quiet Crusade | #888888 (Grey) | Subtle static, muted sparks | Matte, understated | Near-silence, muffled ambience |
| Data Famine | #FF8800 (Amber) | Fading data fragments | Cracked/degraded | Empty static, resource-warning tones |
| Black Budget | #222244 (Dark Navy) | Shadow wisps, cloaking shimmer | Shadow-edged | Whispered comms, stealth drones |
| Market Panic | #FFD700 (Gold) | Floating credit symbols, price tags | Gilded/ornate | Market bell, frantic trading floor chatter |
| Memetic Wild | #FF00FF (Magenta) | Thought bubbles, psychedelic swirls | Shifting kaleidoscope | Whispered phrases, reverse audio |
| Nanoforge Bloom | #00FF88 (Neon Green) | Crawling nanite streams | Organic-tech hybrid | Mechanical growth sounds, clicking |
| Sovereign Raid | #FF6600 (Orange-Red) | Explosive debris, drop pod trails | Military stencil | Raid sirens, drop pod impacts |
| Time Fracture | #4488FF (Bright Blue) | Temporal echoes, clock fragments | Fractured/split | Reversed sounds, ticking clocks |

---

## Systems

### StrifeVisualBridge (Managed SystemBase)

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeVisualBridge.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Managed system that bridges ECS Strife state to MonoBehaviour presentation layer.
// Follows the CombatUIBridgeSystem pattern from the DIG framework.
//
// OnUpdate:
//   1. Read ActiveStrifeState singleton → ActiveCardId
//   2. If ActiveCardId changed since last frame:
//      a. Look up StrifeCardDefinitionSO from registry (managed, by CardId)
//      b. Update StrifeVisualState singleton with new theme color, hashes
//      c. Begin card transition:
//         - Start ambient audio crossfade (old layer fade out, new layer fade in)
//         - Start UI border transition animation
//         - Start particle effect swap
//      d. Notify registered IStrifeVisualProvider adapters
//   3. If transition in progress:
//      a. Advance TransitionFade by deltaTime / TransitionDuration (1.5s)
//      b. Interpolate theme color for smooth visual blend
//      c. When fade complete: stop old ambient, finalize new visuals
//   4. Update StrifeVisualState singleton each frame
```

### StrifeAudioSystem (Managed SystemBase)

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeAudioSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: StrifeVisualBridge
//
// Manages the per-card ambient audio layer.
//
// State: two AudioSource references (current + previous, for crossfade)
//
// OnUpdate:
//   1. Read StrifeVisualState → ActiveCardId, TransitionFade
//   2. If card changed (new card != current AudioSource clip):
//      a. Swap current → previous AudioSource
//      b. Load new card's AmbientLayer clip from StrifeCardDefinitionSO
//      c. Start new AudioSource at volume 0
//   3. During transition:
//      a. Previous source volume = 1.0 - TransitionFade
//      b. Current source volume = TransitionFade
//   4. When transition complete:
//      a. Stop previous source, release clip
//   5. RevealSting: played once on card activation/rotation via one-shot AudioSource
//
// Audio ducking: Strife ambient layer runs at -6dB below master ambient.
// Spatial: non-positional (2D), consistent presence regardless of player position.
```

### StrifeMarkerSpawnSystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeMarkerSpawnSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: StrifeVisualBridge
//
// Spawns and manages world-space district interaction markers.
//
// OnUpdate:
//   1. On district entry (StrifeDistrictModifier appears):
//      a. Read StrifeDistrictModifier → InteractionType
//      b. Determine marker placement positions:
//         - Amplify: red markers at district entry points, near hazard zones
//         - Mitigate: blue markers at safe zones, near relief points
//      c. Spawn StrifeDistrictMarker entities at positions
//      d. Instantiate marker visual prefab (managed, pooled via VFXManager)
//   2. Each frame while markers exist:
//      a. Update PulsePhase = frac(time * PulseSpeed)
//      b. Apply pulse animation to marker visuals (scale + emissive intensity)
//   3. On district exit (StrifeDistrictModifier removed):
//      a. Destroy all StrifeDistrictMarker entities
//      b. Return marker visuals to pool
//
// Marker visual: floating holographic diamond (Amplify=red, Mitigate=blue)
// with card-specific icon projected on its face.
```

### StrifeMarkerAnimationSystem

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeMarkerAnimationSystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: StrifeMarkerSpawnSystem
//
// Burst-compatible system that updates marker pulse animations.
//
// For each StrifeDistrictMarker:
//   1. PulsePhase += deltaTime * PulseFrequency (default 0.8 Hz)
//   2. PulsePhase = frac(PulsePhase) — wrap to 0–1
//   3. Compute scale factor: 1.0 + sin(PulsePhase * 2π) * PulseAmplitude (0.15)
//   4. Write scale to LocalTransform
//   5. Compute emissive intensity: Lerp(0.5, 1.0, (sin(PulsePhase * 2π) + 1) * 0.5)
//   6. Write intensity to MaterialPropertyBlock (via managed bridge)
```

### StrifeBossPreviewBridge (Managed SystemBase)

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeBossPreviewBridge.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
//
// Displays the Strife boss clause preview in the arena pre-fight staging area.
//
// OnUpdate:
//   1. Query StrifeBossPreview (enableable) — skip if disabled
//   2. If enabled and not yet showing:
//      a. Read StrifeBossPreview → Clause, CardId
//      b. Look up StrifeCardDefinitionSO for display data
//      c. Show boss clause preview panel:
//         - Card icon + name + theme color border
//         - Boss clause description text
//         - Animated preview of the clause mechanic (e.g., regen nodes visual)
//      d. Play reveal sting audio
//   3. If disabled and currently showing:
//      a. Fade out and hide preview panel
//
// Preview layout:
// ┌──────────────────────────────────────┐
// │  ⚡ STRIFE CLAUSE: GRAVITY STORM     │
// │  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ │
// │  Arena gravity rotates 90° per phase │
// │  Reorientation window: 3 seconds     │
// │  [Animated gravity direction arrows] │
// └──────────────────────────────────────┘
```

### StrifeUIOverlaySystem (Managed SystemBase)

```csharp
// File: Assets/Scripts/Strife/Systems/StrifeUIOverlaySystem.cs
// WorldSystemFilter: ClientSimulation | LocalSimulation
// UpdateInGroup: PresentationSystemGroup
// UpdateAfter: StrifeVisualBridge
//
// Manages persistent HUD elements for the active Strife card.
//
// OnUpdate:
//   1. Read StrifeVisualState singleton
//   2. Update HUD Strife indicator (top-right corner):
//      a. Card icon with theme color border
//      b. Card name text
//      c. If in Strife-interacting district: show interaction badge (Amplify/Mitigate)
//   3. Update screen-edge vignette:
//      a. Subtle color vignette matching ThemeColor at 10% opacity
//      b. Pulse intensity: Lerp(0.08, 0.12, sin(time * 0.5))
//   4. On card rotation:
//      a. Full-screen card reveal animation (1.5s):
//         - Old card slides out left
//         - New card slides in from right
//         - Card art displayed at 60% screen for 0.8s
//         - Fade to HUD indicator
//      b. Play rotation reveal sting
//   5. On expedition start:
//      a. Full card reveal animation (2.5s):
//         - Card art fills 80% screen
//         - Name + flavor text fade in below
//         - Map rule / enemy mutation / boss clause text cascade
//         - Fade to gameplay with persistent HUD indicator
```

---

## Adapter Pattern (Managed Bridge)

```csharp
// File: Assets/Scripts/Strife/Bridges/IStrifeVisualProvider.cs
namespace Hollowcore.Strife.Bridges
{
    /// <summary>
    /// Interface for MonoBehaviour adapters that display Strife visual state.
    /// Registered with StrifeVisualRegistry (follows CombatUIRegistry pattern).
    /// </summary>
    public interface IStrifeVisualProvider
    {
        void OnStrifeCardChanged(StrifeCardId newCard, StrifeCardId oldCard);
        void OnStrifeDistrictEntered(StrifeCardId card, int districtId, StrifeInteractionType type);
        void OnStrifeDistrictExited();
        void UpdateThemeColor(UnityEngine.Color color, float transitionProgress);
    }
}
```

```csharp
// File: Assets/Scripts/Strife/Bridges/StrifeVisualRegistry.cs
using System.Collections.Generic;

namespace Hollowcore.Strife.Bridges
{
    /// <summary>
    /// Static registry for Strife visual providers. Follows CombatUIRegistry pattern.
    /// MonoBehaviour adapters register on Enable, unregister on Disable.
    /// </summary>
    public static class StrifeVisualRegistry
    {
        private static readonly List<IStrifeVisualProvider> _providers = new();

        public static void Register(IStrifeVisualProvider provider) => _providers.Add(provider);
        public static void Unregister(IStrifeVisualProvider provider) => _providers.Remove(provider);

        public static void NotifyCardChanged(StrifeCardId newCard, StrifeCardId oldCard)
        {
            for (int i = 0; i < _providers.Count; i++)
                _providers[i].OnStrifeCardChanged(newCard, oldCard);
        }

        public static void NotifyDistrictEntered(StrifeCardId card, int districtId, StrifeInteractionType type)
        {
            for (int i = 0; i < _providers.Count; i++)
                _providers[i].OnStrifeDistrictEntered(card, districtId, type);
        }

        public static void NotifyDistrictExited()
        {
            for (int i = 0; i < _providers.Count; i++)
                _providers[i].OnStrifeDistrictExited();
        }

        public static void UpdateTheme(UnityEngine.Color color, float progress)
        {
            for (int i = 0; i < _providers.Count; i++)
                _providers[i].UpdateThemeColor(color, progress);
        }
    }
}
```

---

## System Execution Order

```
PresentationSystemGroup (Client|Local only):
  ┌─────────────────────────────┐
  │ StrifeVisualBridge          │ ← reads ActiveStrifeState, updates StrifeVisualState
  │   ↓                        │
  │ StrifeAudioSystem           │ ← crossfade ambient layers
  │   ↓                        │
  │ StrifeUIOverlaySystem       │ ← HUD indicator, vignette, card reveal
  │   ↓                        │
  │ StrifeMarkerSpawnSystem     │ ← spawn/destroy district markers
  │   ↓                        │
  │ StrifeMarkerAnimationSystem │ ← pulse animation on markers
  │   ↓                        │
  │ StrifeBossPreviewBridge     │ ← boss clause preview panel
  └─────────────────────────────┘
```

---

## Setup Guide

1. **Requires** EPIC 7.1 card definitions with visual fields populated (ThemeColor, Icon, CardArt, ParticleEffectPrefab, UIBorderSprite, AmbientLayer, RevealSting)
2. **Requires** EPIC 7.2 `ActiveStrifeState` singleton operational
3. Create `StrifeVisualState.cs`, `StrifeDistrictMarker.cs`, `StrifeBossPreview.cs` in `Assets/Scripts/Strife/Components/`
4. Create all 6 systems in `Assets/Scripts/Strife/Systems/`
5. Create `IStrifeVisualProvider.cs` and `StrifeVisualRegistry.cs` in `Assets/Scripts/Strife/Bridges/`
6. **Create visual assets**:
   - 12 card art sprites in `Assets/Art/UI/Strife/CardArt/`
   - 12 card icon sprites in `Assets/Art/UI/Strife/Icons/`
   - 12 UI border sprites in `Assets/Art/UI/Strife/Borders/`
   - 12 particle effect prefabs in `Assets/Prefabs/VFX/Strife/`
   - 12 ambient audio clips in `Assets/Audio/Strife/Ambient/`
   - 12 reveal sting clips in `Assets/Audio/Strife/Stings/`
7. **Create marker prefab**: `Assets/Prefabs/VFX/Strife/StrifeDistrictMarker.prefab` — holographic diamond with dynamic material
8. Populate each `StrifeCardDefinitionSO` with references to its visual/audio assets
9. Add `StrifeBossPreview` (baked disabled) to the boss arena staging authoring
10. Create HUD adapter MonoBehaviour implementing `IStrifeVisualProvider`, add to HUD canvas
11. Create vignette post-process adapter implementing `IStrifeVisualProvider`

---

## Verification

- [ ] `StrifeVisualState` singleton updated on card activation
- [ ] Theme color applied to HUD indicator border
- [ ] Screen-edge vignette pulses with correct theme color at low opacity
- [ ] Card reveal animation plays on expedition start (2.5s, full art)
- [ ] Card rotation animation plays on card change (1.5s, slide transition)
- [ ] Ambient audio layer starts on card activation
- [ ] Ambient audio crossfades smoothly on card rotation (no pop/click)
- [ ] Ambient layer runs at -6dB below master ambient
- [ ] Reveal sting plays on card activation and rotation
- [ ] District interaction markers spawn on entry to Strife-interacting district
- [ ] Markers pulse with correct animation (0.8 Hz, 15% amplitude)
- [ ] Amplify markers are red, Mitigate markers are blue
- [ ] Markers destroyed on district exit (no orphans)
- [ ] Boss clause preview panel appears in pre-fight staging area
- [ ] Preview shows correct clause text and animated mechanic preview
- [ ] All 12 cards have distinct visual identity (color, particle, border, audio)
- [ ] All visual/audio systems run client-side only (`ClientSimulation | LocalSimulation`)
- [ ] No new ghost components (all visual state is client-derived)
- [ ] `StrifeVisualRegistry` adapter pattern works (register/unregister lifecycle correct)

---

## Debug Visualization

```csharp
// File: Assets/Scripts/Strife/Debug/StrifeVisualDebugOverlay.cs
// Managed SystemBase, ClientSimulation | LocalSimulation, PresentationSystemGroup
//
// Strife Visual Debug HUD — toggled via debug console: `strife.visual.debug`
//
// Displays:
//   1. StrifeVisualState singleton:
//      - ActiveCardId, ThemeColor (color swatch), ParticleEffectHash, UIBorderHash
//      - AmbientPlaying flag, TransitionFade progress bar
//   2. Audio state:
//      - Current ambient clip name + volume level
//      - Previous ambient clip (during crossfade) + volume level
//      - Crossfade progress bar
//   3. District markers:
//      - Count of active StrifeDistrictMarker entities
//      - Per-marker: WorldPosition, PulsePhase, InteractionType
//   4. Boss preview:
//      - StrifeBossPreview enabled/disabled status
//      - If enabled: Clause enum name, CardId
//   5. Registered IStrifeVisualProvider count + adapter names
//
// Active Strife effects HUD (gameplay-facing debug, separate toggle: `strife.hud`):
//   - Compact panel showing all active modifiers from current card:
//     * Map Rule: icon + one-line description
//     * Enemy Mutation: icon + one-line description
//     * Boss Clause: icon + one-line description (greyed out until boss fight)
//     * District Interaction: if active, show Amplify/Mitigate + effect
//   - Useful for playtesting: always visible reminder of what the Strife is doing
```
