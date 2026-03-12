# EPIC 15.11: Artificial Intelligence Foundation

## Goal
To establish the "Brain" of the game. Currently, we have basic mobs, but no standardized AI framework for complex behaviors (Patrol, Investigate, Combat Tactics).

---

## 1. Behavior Architecture
*   **Status:** **MISSING**. No unified AI framework found (likely ad-hoc scripts).
*   **Requirement:** A modular decision-making engine.
*   **Architecture:**
    *   **Type:** **Utility AI** or **Behavior Tree**.
    *   **Library:** Evaluate **NodeCanvas** or **Behavior Designer** (if integration allowed) OR implement a customized **DOTS-based Utility AI** (Score-based decisions).
    *   **Note:** Since we are ECS, a pure DOTS Utility AI is preferred for performance (thousands of agents).

## 2. Navigation
*   **Status:** **UNKNOWN**. Unity NavMesh is likely used, but needs ECS integration.
*   **Requirement:** 3D Navigation (Flying drones, Climbing spiders).
*   **System:** `AStarPathfindingSystem` (Custom or A* Pathfinding Project integration).
    *   Must support **Voxel changes** (Real-time NavMesh rebaking).

## 3. NPC Sensory System
*   **Status:** `NoiseEmitter` exists (EPIC 15.3), but needs a *Receiver*.
*   **System:** `AISensorySystem`
    *   **Vision:** Cone check (Dot Product + Raycast).
    *   **Hearing:** Distance check vs Noise Level.
    *   **Memory:** `MemoryState` component (Last Known Position).

---

## Implementation Tasks
- [ ] Design ECS Utility AI framework (`AiBrain`, `AiAction`, `AiScorer`).
- [ ] Implement `AISensorySystem` (Vision/Hearing).
- [ ] Research Voxel NavMesh solutions (Runtime baking).
