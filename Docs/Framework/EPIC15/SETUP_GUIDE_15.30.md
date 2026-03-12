# SETUP GUIDE 15.30: Damage Pipeline Visual Unification

**Status**: Implemented
**Last Updated**: February 11, 2026
**Requires**: EPIC 15.29 setup complete (see `SETUP_GUIDE_15.29.md`)

This guide covers what devs and designers need to configure for unified damage visuals. After EPIC 15.30, all damage sources automatically produce correctly colored damage numbers, DOT tick visuals, and status effect floating text with no per-weapon hookup. This guide covers optional visual tuning only.

---

## What Changed

Before EPIC 15.30, damage visuals were broken for weapon modifiers:
- BonusDamage, DOT ticks, and explosion hits produced no visible damage numbers
- Ice, Lightning, Holy, Shadow, and Arcane all showed as white (color lost in conversion)
- No "BLEEDING!", "BURNING!" floating text when status effects were applied

Now:
- **All damage sources** automatically produce correctly colored damage numbers
- **DOT ticks** use a dedicated smaller prefab (configurable)
- **Status effect application** shows floating text ("BLEEDING!", "BURNING!", etc.)
- **Elemental colors** are preserved end-to-end (Ice = blue, Lightning = yellow, etc.)

---

## What's Automatic (No Setup Required)

These features work out of the box once code is compiled:

| Feature | How It Works |
|---------|-------------|
| Elemental damage number colors | All 8 elements have hardcoded fallback colors (see table below) |
| DOT tick numbers | Automatically routed to DOTPrefab (or normal prefab if not assigned) |
| Status effect text | "BLEEDING!", "BURNING!", etc. appear when combat effects are applied |
| Correct element on Frostbite/Shock DOTs | Frostbite ticks show as Ice (blue), Shock ticks show as Lightning (yellow) |
| BonusDamage visibility | Bonus elemental damage from modifiers produces separate colored damage numbers |
| Explosion AOE numbers | Each entity hit by an explosion gets its own damage number |
| Health bar updates | Health is reduced correctly for all damage paths — no visual hookup needed |
| Existing weapons unchanged | All existing Physical weapons continue to work identically |

---

## 1. Configuring Element Colors (Optional)

Element colors have hardcoded fallbacks. To override them with custom colors, edit the **DamageFeedbackProfile** ScriptableObject.

### 1.1 Open the Profile

1. Navigate to `Assets/Data/Config/` in the Project window
2. Select **DefaultDamageFeedbackProfile** (or your active profile)
3. In the Inspector, find the **Damage Types (Color/Font)** section

### 1.2 Add Element Entries

The `DamageTypes` list maps each element to a color. Click **+** to add entries for each element you want to customize.

| Field | Description |
|-------|-------------|
| **Type** | The element (dropdown: Physical, Fire, Ice, Lightning, Poison, Holy, Shadow, Arcane) |
| **Display Name** | Text label (e.g., "Fire", "Ice") — for UI/logging |
| **Color** | The damage number color for this element |
| **Size Multiplier** | Scale multiplier (1.0 = normal) |

### 1.3 Hardcoded Fallback Colors

If a DamageType has no entry in the profile (or the profile is not assigned), these fallback colors are used:

| Element | Fallback Color | Hex | Use Case |
|---------|---------------|-----|----------|
| Physical | White | #FFFFFF | Melee, bleed ticks, physical projectiles |
| Fire | Orange-Red | #FF6619 | Fire weapons, burn ticks |
| Ice | Light Blue | #4DB3FF | Ice weapons, frostbite ticks |
| Lightning | Yellow | #FFF24D | Lightning weapons, shock ticks |
| Poison | Green | #66E633 | Poison weapons, poison DOT ticks |
| Holy | Warm White | #FFFFCC | Holy weapons |
| Shadow | Purple | #9933CC | Shadow weapons |
| Arcane | Magenta | #CC4DFF | Arcane weapons |

### 1.4 Recommended Profile Setup

For best visual quality, add all 8 entries to the DamageTypes list. Example:

| Type | Display Name | Color | Size Multiplier |
|------|-------------|-------|-----------------|
| Physical | Physical | White (255, 255, 255) | 1.0 |
| Fire | Fire | Orange (255, 100, 25) | 1.0 |
| Ice | Ice | Cyan (77, 179, 255) | 1.0 |
| Lightning | Lightning | Yellow (255, 242, 77) | 1.0 |
| Poison | Poison | Green (102, 230, 51) | 1.0 |
| Holy | Holy | Gold (255, 255, 204) | 1.0 |
| Shadow | Shadow | Purple (153, 51, 204) | 1.0 |
| Arcane | Arcane | Magenta (204, 77, 255) | 1.0 |

---

## 2. Configuring DOT Tick Prefab (Optional)

DOT ticks (bleed, burn, poison, frostbite, shock) use a dedicated prefab that should be smaller and less visually prominent than regular damage numbers.

### 2.1 Assign the DOT Prefab

1. Open the **DefaultDamageFeedbackProfile** in the Inspector
2. Find the **Utility Prefabs** section
3. Assign a DamageNumbersPro prefab to the **DOT Prefab** slot

### 2.2 Creating a DOT Prefab

If no DOT prefab exists yet:

1. Duplicate an existing DamageNumber prefab (e.g., the normal hit prefab)
2. Rename it to `DamageNumber_DOT` or similar
3. Adjust the prefab settings for DOT-style appearance:
   - **Smaller font size** (60-70% of normal)
   - **Shorter lifetime** (0.6-0.8 seconds)
   - **Less vertical travel** (reduced rise speed)
   - **Random horizontal offset** (spreads out rapid ticks)
   - **Lower opacity** (70-80% alpha)
4. Assign it to the `DOTPrefab` slot on the profile

> **Note:** If no DOTPrefab is assigned, DOT ticks use the normal damage number prefab. This works but makes DOT ticks visually indistinguishable from direct hits.

---

## 3. Status Effect Floating Text (Optional Tuning)

When a combat status effect is applied (Bleed, Burn, Frostbite, Shock, Poison, Stun, Slow, Weaken), floating text appears above the target (e.g., "BLEEDING!", "BURNING!").

### 3.1 Customize Colors and Names

1. Navigate to `Assets/Data/Config/`
2. Select the **FloatingTextStyleConfig** asset
3. Configure the status effect color fields:

| Field | Default Color | Controls |
|-------|-------------|----------|
| **Burn Color** | Orange (1, 0.4, 0) | "BURNING!" text color |
| **Bleed Color** | Red (0.8, 0, 0) | "BLEEDING!" text color |
| **Poison Color** | Green (0.5, 0.8, 0.2) | "POISONED!" text color |
| **Freeze Color** | Cyan (0.5, 0.8, 1) | "FROZEN!" text color |
| **Stun Color** | Yellow (1, 1, 0.3) | "STUNNED!" text color |
| **Buff Color** | Light Green (0.3, 1, 0.5) | Buff application text color |
| **Debuff Color** | Magenta (0.8, 0.3, 0.8) | Debuff application text color |

### 3.2 Status Effect Display Names

The `FloatingTextStyleConfig.GetStatusEffectName()` method returns the display text for each status type. These are hardcoded in the config class. The mapping is:

| Status Effect | Display Text |
|--------------|-------------|
| Burn | "BURNING!" |
| Bleed | "BLEEDING!" |
| Poison | "POISONED!" |
| Frostbite | "FROZEN!" |
| Stun | "STUNNED!" |
| Slow | "SLOWED!" |
| Weakness | "WEAKENED!" |

### 3.3 Which Effects Show Text

Only combat-relevant effects produce floating text:

| Shows Text | No Text (Environmental) |
|-----------|------------------------|
| Burn, Bleed, Poison, Frostbite, Shock, Stun, Slow, Weaken | Hypoxia, Radiation Poisoning, Concussion |

---

## 4. Element Reference for Weapon Setup

When configuring weapons (see `SETUP_GUIDE_15.29.md`), the full element chain is now:

| Weapon Element | DamageEvent Type | Visual Color | DOT Tick Color |
|---------------|-----------------|-------------|----------------|
| Physical | Physical | White | White |
| Fire | Heat | Orange-Red | Orange-Red |
| Ice | Ice | Light Blue | Light Blue |
| Lightning | Lightning | Yellow | Yellow |
| Poison | Toxic | Green | Green |
| Holy | Holy | Warm White | N/A |
| Shadow | Shadow | Purple | N/A |
| Arcane | Arcane | Magenta | N/A |

> **Key change from 15.29:** Ice, Lightning, Holy, Shadow, and Arcane are now **preserved through the entire pipeline** instead of degrading to Physical/white. No setup change needed — existing weapons with these elements now display correctly.

---

## 5. Verification Checklist

| # | Test | Steps | Expected Result |
|---|------|-------|-----------------|
| 1 | Compile | Build project | No errors |
| 2 | Fire weapon hit | Equip fire weapon, shoot BoxingJoe | Orange-red damage number |
| 3 | Ice weapon hit | Equip ice weapon, shoot BoxingJoe | Blue damage number |
| 4 | Lightning BonusDamage | Add BonusDamage (Lightning) modifier, shoot | Yellow bonus number alongside primary |
| 5 | Bleed modifier | Add Bleed modifier (Chance=1.0), attack | "BLEEDING!" floating text + periodic white DOT ticks |
| 6 | Burn modifier | Add Burn modifier (Chance=1.0), attack | "BURNING!" floating text + periodic orange DOT ticks |
| 7 | Explosion modifier | Add Explosion modifier, attack | Damage numbers on all entities in blast radius |
| 8 | Bow/projectile hit | Shoot with bow (DamagePreApplied=false) | Damage number still appears |
| 9 | Blocked hit | Trigger a block | "BLOCKED" defensive text (not from damage queue) |
| 10 | Miss | Trigger a miss | "MISS" text (not from damage queue) |
| 11 | No duplicate numbers | Any weapon, single hit | Exactly one damage number per damage source (no doubles) |
| 12 | DOT prefab | Assign DOTPrefab, trigger bleed | Bleed ticks use smaller DOT prefab |
| 13 | Environmental radiation | Enter radiation zone | Small white DOT-style tick numbers |

---

## 6. Troubleshooting

| Issue | Likely Cause | Solution |
|-------|-------------|----------|
| All damage numbers are white | DamageFeedbackProfile has no DamageType entries | Add entries for each element (Section 1.4), or rely on hardcoded fallbacks |
| No damage numbers at all | DamageNumbersProAdapter not in scene | Ensure a GameObject with `DamageNumbersProAdapter` exists in the scene |
| DOT ticks look same as normal hits | No DOTPrefab assigned | Assign a smaller DamageNumber prefab to DOTPrefab on the profile (Section 2) |
| No "BLEEDING!" text | FloatingTextManager not in scene | Ensure a GameObject with `FloatingTextManager` exists and is registered |
| Status text wrong color | FloatingTextStyleConfig not configured | Check status effect color fields on the config asset (Section 3.1) |
| BonusDamage not showing | Target has no `DamageEvent` buffer | Ensure target has `DamageableAuthoring` component (bakes required buffers) |
| Ice/Lightning still white | Old code cached | Clean build: delete `Library/` folder and reimport |
| Changes not taking effect | Stale subscene bake | Reimport the subscene (right-click > Reimport) |
| Duplicate damage numbers | Custom system also enqueuing | Check for any other systems calling `DamageVisualQueue.Enqueue` |

---

## 7. Designer Reference: What Controls What

| What You Want | What to Configure | Where |
|---------------|-------------------|-------|
| Element colors (override) | DamageTypeProfile entries | DamageFeedbackProfile Inspector |
| DOT tick visual style | DOTPrefab | DamageFeedbackProfile > Utility Prefabs |
| Status effect text colors | Burn/Bleed/Poison/Freeze/Stun Color | FloatingTextStyleConfig Inspector |
| Hit severity (crit/execute scale) | DamageNumberProfile settings | DamageFeedbackProfile > Hit Severity |
| Cull distance / max numbers | CullDistance, MaxActiveNumbers | DamageFeedbackProfile > Settings |
| Weapon element | `WeaponAuthoring.DamageElement` | Weapon prefab Inspector (see 15.29) |
| On-hit modifiers | `WeaponModifier` entries | Weapon prefab Inspector (see 15.29) |
| DOT tick interval | `StatusEffectConfig.TickInterval` | StatusEffectConfig singleton |

---

## 8. Relationship to Other EPICs

| Concern | Guide |
|---------|-------|
| Damage number prefabs, hit severity profiles | SETUP_GUIDE_15.22 |
| Weapon elements and on-hit modifiers | SETUP_GUIDE_15.29 |
| Pipeline routing, resolver types, hitboxes | SETUP_GUIDE_15.28 |
| **Element colors and DOT visuals** | **This guide (15.30)** |
| **Status effect floating text** | **This guide (15.30)** |
