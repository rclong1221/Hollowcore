## EPIC 1: Core Player Framework & Refactoring

### Epic 1.1: Project Restructuring
**Priority**: CRITICAL  
**Goal**: Organize existing code into modular framework structure

**Tasks**:
- [X] Create directory structure: `Core/`, `Systems/Camera/`, `Systems/Network/`, `Player/Authoring/`, `Player/Components/`, `Player/Systems/`, `Player/Camera/`
- [X] Move `Game.cs` → `Core/GameBootstrap.cs`
- [X] Move `GoInGame.cs` → `Systems/Network/GoInGameSystem.cs`
- [X] Move `NetworkUI.cs` → `Systems/Network/NetworkUI.cs`
- Move `PlayerAuthoring.cs` → `Player/Authoring/PlayerAuthoring.cs`
- [X] Move `PlayerInputAuthoring.cs` → `Player/Authoring/PlayerInputAuthoring.cs`
- [X] Move `PlayerSpawnerAuthoring.cs` → `Player/Authoring/PlayerSpawnerAuthoring.cs`
- [X] Move `PlayerMovementSystem.cs` → `Player/Systems/PlayerMovementSystem.cs`
- [X] Update all namespace references and imports
- [X] Verify project compiles after restructuring