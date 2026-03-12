using Unity.Entities;
using Unity.Burst;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// EPIC 15.16 Task 11: Lock Behavior Dispatcher
    /// 
    /// Routes targeting updates to the appropriate lock behavior system based on current mode.
    /// Manages the ActiveLockBehavior singleton and enables/disables per-mode systems.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct LockBehaviorDispatcherSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Create the ActiveLockBehavior singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<ActiveLockBehavior>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, ActiveLockBehavior.HardLock());
                state.EntityManager.SetName(entity, "ActiveLockBehavior");
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            // The dispatcher doesn't need to do much per-frame
            // Individual behavior systems check ActiveLockBehavior.BehaviorType
            // This system mainly ensures the singleton exists and can handle mode switches
        }
    }
    
    /// <summary>
    /// Helper for managed code to change lock behavior mode.
    /// </summary>
    public static class LockBehaviorHelper
    {
        public static void SetMode(LockBehaviorType mode)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            var em = world.EntityManager;
            
            // Find or create the singleton
            var query = em.CreateEntityQuery(typeof(ActiveLockBehavior));
            if (query.IsEmpty)
            {
                var entity = em.CreateEntity();
                em.AddComponentData(entity, ActiveLockBehavior.HardLock());
            }
            
            // Update the singleton
            foreach (var entity in query.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                ActiveLockBehavior newBehavior;
                switch (mode)
                {
                    case LockBehaviorType.HardLock:
                        newBehavior = ActiveLockBehavior.HardLock();
                        break;
                    case LockBehaviorType.SoftLock:
                        newBehavior = ActiveLockBehavior.SoftLock();
                        break;
                    case LockBehaviorType.IsometricLock:
                        newBehavior = ActiveLockBehavior.IsometricLock();
                        break;
                    case LockBehaviorType.OverTheShoulder:
                        newBehavior = ActiveLockBehavior.OverTheShoulder();
                        break;
                    case LockBehaviorType.TwinStick:
                        newBehavior = ActiveLockBehavior.TwinStick();
                        break;
                    case LockBehaviorType.FirstPerson:
                        newBehavior = ActiveLockBehavior.FirstPerson();
                        break;
                    default:
                        newBehavior = ActiveLockBehavior.HardLock();
                        break;
                }
                em.SetComponentData(entity, newBehavior);
            }
        }
        
        public static LockBehaviorType GetCurrentMode()
        {
            // Check ClientWorld first (where CameraLockOnSystem runs)
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;
                if (world.Name != "ClientWorld") continue;
                
                var em = world.EntityManager;
                var query = em.CreateEntityQuery(typeof(ActiveLockBehavior));
                
                if (!query.IsEmpty)
                {
                    var entity = query.GetSingletonEntity();
                    var behavior = em.GetComponentData<ActiveLockBehavior>(entity);
                    return behavior.BehaviorType;
                }
            }
            
            // Fallback to default world
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null) return LockBehaviorType.None;
            
            var defaultEm = defaultWorld.EntityManager;
            var defaultQuery = defaultEm.CreateEntityQuery(typeof(ActiveLockBehavior));
            
            if (defaultQuery.IsEmpty) return LockBehaviorType.None;
            
            var defaultEntity = defaultQuery.GetSingletonEntity();
            var defaultBehavior = defaultEm.GetComponentData<ActiveLockBehavior>(defaultEntity);
            return defaultBehavior.BehaviorType;
        }
    }
}
