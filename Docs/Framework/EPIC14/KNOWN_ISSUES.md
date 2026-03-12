# EPIC 14 - Known Issues

This document tracks unresolved issues encountered during EPIC 14 (Equipment System) development.

---

## 14.4 Off-Hand System

### Shield Off→Main Swap Animation Stuck
**Severity:** Low (Edge Case)  
**Status:** Unresolved

**Description:**  
When moving a Shield from Off-Hand (Slot 1) to Main-Hand (Slot 0), the left arm animation remains stuck in the "shield holding" pose. This blocks subsequent off-hand item animations until manually cleared.

**Steps to Reproduce:**
1. Equip Sword in Main Hand.
2. Equip Shield in Off Hand (Option+2).
3. Move Shield to Main Hand (press 2 without modifier).
4. Left arm stays in shield pose despite Off-Hand being empty.

**Workaround:**  
Use Equipment System Debugger → **Clear** button on Off-Hand row before switching Shield to Main-Hand.

**Root Cause Analysis:**
- The Animator Controller (`ClimbingDemo.controller`) expects proper "Unequip" animation state transitions.
- When items swap slots directly, the off-hand never receives `Slot1ItemStateIndex = 5` (Unequip) signal in a way the Animator processes correctly.
- Multiple fixes attempted:
  - Layer weight suppression (partial success)
  - Explicit weight reset every frame (unsuccessful)
  - Timed unequip transition (unsuccessful)
- The Animator Controller's internal state machine logic may require direct modification.

**Future Fix:**  
Investigate `ClimbingDemo.controller` Animator transitions for Slot1 → Empty flow. May need Editor script to add/modify transitions.

---

## 14.1 - 14.3 Equipment Systems

No unresolved issues at this time. Core equipping, visual bridge, and off-hand visuals work as expected.
