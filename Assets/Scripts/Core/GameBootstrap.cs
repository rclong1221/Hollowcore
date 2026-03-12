using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Unity.Networking.Transport;
using Unity.Scenes;
using System.Collections;

// Create a custom bootstrap that waits for user input (Host or Join button)
[UnityEngine.Scripting.Preserve]
public class GameBootstrap : ClientServerBootstrap
{
    public static bool HasInitialized { get; private set; }
    // Toggle to enable/disable debug logs in this file
    public static bool EnableDebugLogs = false;

    /// <summary>
    /// EPIC 18.10: When true, the client sends SpectatorJoinRequest instead of GoInGameRequest.
    /// Set before calling CreateClient().
    /// </summary>
    public static bool IsSpectatorMode { get; set; }

    /// <summary>
    /// EPIC 18.6: Resets the initialization flag so new worlds can be created
    /// after returning to lobby or transitioning to a non-network state.
    /// </summary>
    public static void ResetInitialized() { HasInitialized = false; }

    // Reset static state on domain reload (needed for Editor play mode)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        HasInitialized = false;
        IsSpectatorMode = false;
    }
    
    public override bool Initialize(string defaultWorldName)
    {
        // CRITICAL: Set AutoConnectPort to 0 to DISABLE automatic connection
        // We handle connections manually in SubsceneLoadHelper after subscenes load
        AutoConnectPort = 0;
        DefaultConnectAddress = NetworkEndpoint.LoopbackIpv4;
        DefaultListenAddress = NetworkEndpoint.AnyIpv4;
        
        // Enable running in background to prevent connection stalls when tabbing out
        Application.runInBackground = true;
        
        // We need to return true and let the base handle some initialization,
        // but we'll control when the actual server/client worlds are created
        HasInitialized = false;
        if (EnableDebugLogs) Debug.Log("Waiting for Host/Join button...");
        
        // Create a minimal local world to satisfy the DefaultGameObjectInjectionWorld requirement
        // This prevents the assertion error
        var localWorld = new World("LocalWorld", WorldFlags.Game);
        World.DefaultGameObjectInjectionWorld = localWorld;
        
        // Return true - we'll manually create NetCode worlds when buttons are pressed
        return true;
    }
    
    private static void CleanupExistingWorlds()
    {
        // Dispose any existing server/client worlds to prevent duplicate connections
        foreach (var world in World.All)
        {
            if (world.Name == "ServerWorld" || world.Name == "ClientWorld")
            {
                if (EnableDebugLogs) Debug.Log($"Disposing existing {world.Name}");
                world.Dispose();
            }
        }
    }
    
    public static void CreateHost()
    {
        if (HasInitialized)
        {
            if (EnableDebugLogs) Debug.LogWarning("Already initialized!");
            return;
        }
        
        // Set flag IMMEDIATELY to prevent duplicate calls from OnGUI
        HasInitialized = true;
        
        // Clean up any existing worlds to prevent duplicate connections
        CleanupExistingWorlds();
        
        if (EnableDebugLogs) Debug.Log("Creating HOST (Server + Client)...");
        var serverWorld = CreateServerWorld("ServerWorld");
        	var clientWorld = CreateClientWorld("ClientWorld");

        	// Defensive: remove any pre-existing listen/connect requests that may have
        	// been created by underlying bootstrap logic to avoid double-connect errors
        	var serverListenQuery = serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamRequestListen>());
        	if (!serverListenQuery.IsEmpty)
        	{
        		serverWorld.EntityManager.DestroyEntity(serverListenQuery);
          		if (EnableDebugLogs) Debug.Log("Removed pre-existing NetworkStreamRequestListen from ServerWorld");
        	}

        	var clientConnectQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamRequestConnect>());
        	if (!clientConnectQuery.IsEmpty)
        	{
        		clientWorld.EntityManager.DestroyEntity(clientConnectQuery);
          		if (EnableDebugLogs) Debug.Log("Removed pre-existing NetworkStreamRequestConnect from ClientWorld");
        	}
        
        // Load subscenes into the created worlds and wait
        var helper = new GameObject("SubsceneLoadHelper").AddComponent<SubsceneLoadHelper>();
        helper.StartCoroutine(helper.LoadAndWait(serverWorld, clientWorld));
    }
    
    public static void CreateClient()
    {
        if (HasInitialized)
        {
            if (EnableDebugLogs) Debug.LogWarning("Already initialized!");
            return;
        }
        
        // Set flag IMMEDIATELY to prevent duplicate calls from OnGUI
        HasInitialized = true;
        
        // Clean up any existing worlds to prevent duplicate connections
        CleanupExistingWorlds();
        
        if (EnableDebugLogs) Debug.Log("Creating CLIENT...");
        	var clientWorld = CreateClientWorld("ClientWorld");

        	// Defensive: remove any pre-existing connect requests to avoid double-connect
        	var clientConnectQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamRequestConnect>());
        	if (!clientConnectQuery.IsEmpty)
        	{
        		clientWorld.EntityManager.DestroyEntity(clientConnectQuery);
          		if (EnableDebugLogs) Debug.Log("Removed pre-existing NetworkStreamRequestConnect from ClientWorld");
        	}
        
        // Load subscenes into the created world and wait
        var helper = new GameObject("SubsceneLoadHelper").AddComponent<SubsceneLoadHelper>();
        helper.StartCoroutine(helper.LoadAndWait(null, clientWorld));
    }
}

// Helper MonoBehaviour to handle async subscene loading
public class SubsceneLoadHelper : MonoBehaviour
{
    public IEnumerator LoadAndWait(World serverWorld, World clientWorld)
    {
        // Find all SubScene objects in the current scene
        var subScenes = UnityEngine.Object.FindObjectsByType<Unity.Scenes.SubScene>(UnityEngine.FindObjectsSortMode.None);
        
        if (subScenes.Length == 0)
        {
            if (GameBootstrap.EnableDebugLogs) Debug.LogWarning("No SubScenes found in scene! Player spawning may not work.");
            Destroy(gameObject);
            yield break;
        }
        
        if (GameBootstrap.EnableDebugLogs) Debug.Log($"Loading {subScenes.Length} subscenes...");
        
        foreach (var subScene in subScenes)
        {
                if (serverWorld != null)
                {
                    if (GameBootstrap.EnableDebugLogs) Debug.Log($"Loading SubScene {subScene.SceneGUID} into ServerWorld");
                var sceneEntity = SceneSystem.LoadSceneAsync(serverWorld.Unmanaged, subScene.SceneGUID, 
                    new SceneSystem.LoadParameters { Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn });
                
                // Wait for server subscene to load - tick world to allow systems to run
                int timeout = 100;
                while (!SceneSystem.IsSceneLoaded(serverWorld.Unmanaged, sceneEntity) && timeout > 0)
                {
                    serverWorld.Update();
                    yield return null;
                    timeout--;
                }
                
                if (timeout == 0)
                    if (GameBootstrap.EnableDebugLogs) Debug.LogError("Timeout waiting for ServerWorld subscene to load!");
                else
                    if (GameBootstrap.EnableDebugLogs) Debug.Log("ServerWorld subscene loaded successfully");
            }
            
                if (clientWorld != null)
                {
                    if (GameBootstrap.EnableDebugLogs) Debug.Log($"Loading SubScene {subScene.SceneGUID} into ClientWorld");
                var sceneEntity = SceneSystem.LoadSceneAsync(clientWorld.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters { Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn });
                
                // Wait for client subscene to load - tick world to allow systems to run
                int timeout = 100;
                while (!SceneSystem.IsSceneLoaded(clientWorld.Unmanaged, sceneEntity) && timeout > 0)
                {
                    clientWorld.Update();
                    yield return null;
                    timeout--;
                }
                
                if (timeout == 0)
                    if (GameBootstrap.EnableDebugLogs) Debug.LogError("Timeout waiting for ClientWorld subscene to load!");
                else
                    if (GameBootstrap.EnableDebugLogs) Debug.Log("ClientWorld subscene loaded successfully");
            }
        }
        
        if (GameBootstrap.EnableDebugLogs) Debug.Log("All subscenes loaded. Now establishing network connections...");
        
        // Manually initiate network connections after subscenes are loaded
        // Only create connection entities if they don't already exist
        if (serverWorld != null)
        {
            var listenQuery = serverWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamRequestListen>());
            if (listenQuery.IsEmpty)
            {
                var listenEntity = serverWorld.EntityManager.CreateEntity();
                serverWorld.EntityManager.AddComponentData(listenEntity, new NetworkStreamRequestListen
                {
                    Endpoint = NetworkEndpoint.AnyIpv4.WithPort(7979)
                });
                if (GameBootstrap.EnableDebugLogs) Debug.Log("Server listening on port 7979");
            }
            else
            {
                if (GameBootstrap.EnableDebugLogs) Debug.Log("Server already has a listen request, skipping");
            }
        }
        
        if (clientWorld != null)
        {
            var connectQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamRequestConnect>());
            if (connectQuery.IsEmpty)
            {
                var connectEntity = clientWorld.EntityManager.CreateEntity();
                clientWorld.EntityManager.AddComponentData(connectEntity, new NetworkStreamRequestConnect
                {
                    Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(7979)
                });
                if (GameBootstrap.EnableDebugLogs) Debug.Log("Client connecting to localhost:7979");
            }
            else
            {
                if (GameBootstrap.EnableDebugLogs) Debug.Log("Client already has a connect request, skipping");
            }
        }
        
        if (GameBootstrap.EnableDebugLogs) Debug.Log("Network setup complete!");
        
        // EPIC 15.18: Switch from UI context to Gameplay context now that we're in-game
        if (DIG.Core.Input.InputContextManager.Instance != null)
        {
            DIG.Core.Input.InputContextManager.Instance.SetContext(DIG.Core.Input.InputContext.Gameplay);
            if (GameBootstrap.EnableDebugLogs) Debug.Log("Switched to Gameplay input context");
        }
        
        Destroy(gameObject);
    }
}
