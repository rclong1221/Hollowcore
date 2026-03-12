using System;
using System.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: Static utility for async scene loading.
    /// Handles three modes: Single (SceneManager), Additive (SceneManager),
    /// SubScene (SceneSystem for ECS worlds).
    /// Progress is aggregated into a single 0-1 float across all operations.
    /// Mirrors the SubsceneLoadHelper pattern from GameBootstrap but with
    /// progress reporting and parametric GUID support.
    /// </summary>
    public static class SceneLoader
    {
        /// <summary>
        /// Load a scene definition asynchronously.
        /// <paramref name="progressCallback"/> receives 0-1 float each frame.
        /// For SubScene mode, <paramref name="serverWorld"/> and <paramref name="clientWorld"/>
        /// specify ECS worlds (null = skip).
        /// </summary>
        public static IEnumerator LoadAsync(
            SceneDefinitionSO sceneDef,
            Action<float> progressCallback,
            World serverWorld = null,
            World clientWorld = null)
        {
            if (sceneDef == null)
            {
                progressCallback?.Invoke(1f);
                yield break;
            }

            switch (sceneDef.LoadMode)
            {
                case SceneLoadMode.Single:
                    yield return LoadSceneManagerAsync(sceneDef.SceneName,
                        LoadSceneMode.Single, progressCallback);
                    break;

                case SceneLoadMode.Additive:
                    yield return LoadSceneManagerAsync(sceneDef.SceneName,
                        LoadSceneMode.Additive, progressCallback);
                    break;

                case SceneLoadMode.SubScene:
                    yield return LoadSubScenesAsync(sceneDef, progressCallback,
                        serverWorld, clientWorld);
                    break;
            }
        }

        /// <summary>
        /// Unload an additively loaded scene by name.
        /// </summary>
        public static IEnumerator UnloadAsync(string sceneName, Action onComplete = null)
        {
            var op = SceneManager.UnloadSceneAsync(sceneName);
            if (op != null)
            {
                while (!op.isDone)
                    yield return null;
            }
            onComplete?.Invoke();
        }

        private static IEnumerator LoadSceneManagerAsync(
            string sceneName, LoadSceneMode mode, Action<float> progressCallback)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[SceneLoader] SceneName is null or empty.");
#endif
                progressCallback?.Invoke(1f);
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, mode);
            if (op == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SceneLoader] Failed to start load for scene '{sceneName}'. Is it in Build Settings?");
#endif
                progressCallback?.Invoke(1f);
                yield break;
            }

            op.allowSceneActivation = false;

            // Unity caps progress at 0.9 until allowSceneActivation = true
            while (op.progress < 0.9f)
            {
                progressCallback?.Invoke(op.progress / 0.9f);
                yield return null;
            }

            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                progressCallback?.Invoke(1f);
                yield return null;
            }

            progressCallback?.Invoke(1f);
        }

        private static IEnumerator LoadSubScenesAsync(
            SceneDefinitionSO sceneDef,
            Action<float> progressCallback,
            World serverWorld,
            World clientWorld)
        {
            var guids = sceneDef.SubSceneGuids;
            if (guids == null || guids.Length == 0)
            {
                progressCallback?.Invoke(1f);
                yield break;
            }

            int worldCount = (serverWorld != null ? 1 : 0) + (clientWorld != null ? 1 : 0);
            int totalOps = guids.Length * worldCount;
            if (totalOps == 0)
            {
                progressCallback?.Invoke(1f);
                yield break;
            }

            int completedOps = 0;

            for (int g = 0; g < guids.Length; g++)
            {
                var sceneGuid = new Unity.Entities.Hash128(guids[g]);

                // Server world
                if (serverWorld != null)
                {
                    yield return LoadSingleSubScene(serverWorld, sceneGuid, () =>
                    {
                        float p = (completedOps + 0.5f) / totalOps;
                        progressCallback?.Invoke(p);
                    });
                    completedOps++;
                }

                // Client world
                if (clientWorld != null)
                {
                    yield return LoadSingleSubScene(clientWorld, sceneGuid, () =>
                    {
                        float p = (completedOps + 0.5f) / totalOps;
                        progressCallback?.Invoke(p);
                    });
                    completedOps++;
                }
            }

            progressCallback?.Invoke(1f);
        }

        private static IEnumerator LoadSingleSubScene(
            World world, Unity.Entities.Hash128 sceneGuid, Action onProgress)
        {
            var sceneEntity = SceneSystem.LoadSceneAsync(world.Unmanaged, sceneGuid,
                new SceneSystem.LoadParameters
                {
                    Flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn
                });

            // Match SubsceneLoadHelper timeout pattern: ~5 seconds at 60 fps
            int timeout = 300;
            while (!SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity) && timeout > 0)
            {
                world.Update();
                onProgress?.Invoke();
                yield return null;
                timeout--;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (timeout <= 0)
                Debug.LogError($"[SceneLoader] Timeout loading SubScene {sceneGuid} into {world.Name}");
#endif
        }
    }
}
