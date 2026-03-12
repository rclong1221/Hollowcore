# Audio QA: Animator-driven footsteps (Hybrid migration)

This document describes how to test the Animator→ECS hybrid audio workflow (local immediate playback + compact network events for remotes).

Quick checklist
- Ensure `AudioManager` is present in the scene (create one from `GameObject` -> `Create Empty` and add `AudioManager` component, or use the Editor helper window `Audio/Create Audio QA Scene`).
- On your player prefab: add `AnimatorEventBridge` to the GameObject with the `Animator`.
- Add animation events to footstep frames that call `OnFootstep()` (or `OnFootstep_Int(int)` to pass explicit material id) and `OnLanding()` as needed.
- If you want the bridge to publish compact network events for remote playback, enable `PublishNetworkEvent` on the `AnimatorEventBridge` instance.
- Confirm `AudioSettings.UseAnimatorForFootsteps` is `true` (default) to disable DOTS footstep emitters on local clients.

How it works
- Local player: `AnimatorEventBridge` receives animation events and plays audio immediately via the `AudioManager`. This gives precise timing and designer control.
- Network: when `PublishNetworkEvent` is enabled, the bridge also writes a compact buffer element (`NetworkedAudioBufferElement`) into a singleton transmitter entity. A netcode adapter (project-specific) should read that buffer and send the events to remote clients.
- Remote clients: received buffer elements (or the transmitter when running in a loopback test) are consumed by `NetworkedAudioPlaybackSystem`. It dedupes near-duplicate events and plays them using the local `AudioManager`.

Testing steps
1. Open a scene with the player prefab and an `AudioManager`.
2. Put the player above a surface that has a `SurfaceMaterialAuthoring` component assigned.
3. Enter Play mode and trigger the animation(s) with footstep events.
4. Verify local sounds play at the correct frames and rigs/IK triggers fire.
5. Toggle `PublishNetworkEvent` and run a two-client test (or use the Editor helper) to verify remote playback.

Notes and next steps
- The `NetworkedAudioSystem` already collects DOTS `FootstepEvent` instances into the same buffer for network transmission if DOTS emitters are enabled server-side.
- The provided editor helper `Assets/Editor/Audio/CreateAudioQAScene.cs` can scaffold a quick test scene.
- For production networking, implement a NetCode adapter that serializes `NetworkedAudioBufferElement` and transmits to remote clients. Ensure rate limiting on the server.
