#if DIG_DEV_CONSOLE
using DIG.DebugConsole.UI;
using UnityEngine;
using UnityEngine.Profiling;

namespace DIG.DebugConsole.Commands
{
    /// <summary>
    /// EPIC 18.9: System/diagnostic console commands.
    /// </summary>
    public static class SystemCommands
    {
        [ConCommand("fps", "Toggle FPS/stat overlay", "fps [on|off]", ConCommandFlags.ReadOnly)]
        public static void CmdFps(ConCommandArgs args)
        {
            bool target = args.Count > 0 ? args.GetBool(0) : !StatOverlayView.IsVisible;
            StatOverlayView.IsVisible = target;
            DevConsoleService.Instance.Log($"Stat overlay: {(target ? "ON" : "OFF")}");
        }

        [ConCommand("memory", "Show memory usage statistics", "", ConCommandFlags.ReadOnly)]
        public static void CmdMemory(ConCommandArgs args)
        {
            var svc = DevConsoleService.Instance;
            long totalMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            long reservedMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
            long unusedMB = Profiler.GetTotalUnusedReservedMemoryLong() / (1024 * 1024);
            long gcMB = System.GC.GetTotalMemory(false) / (1024 * 1024);
            long monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024 * 1024);
            long monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024 * 1024);

            svc.Log("--- Memory ---");
            svc.Log($"  Total Allocated: {totalMB} MB");
            svc.Log($"  Reserved:        {reservedMB} MB");
            svc.Log($"  Unused Reserved: {unusedMB} MB");
            svc.Log($"  GC Heap:         {gcMB} MB");
            svc.Log($"  Mono Heap:       {monoHeapMB} MB (used: {monoUsedMB} MB)");
        }

        [ConCommand("gc", "Force garbage collection", "", ConCommandFlags.None)]
        public static void CmdGC(ConCommandArgs args)
        {
            long before = System.GC.GetTotalMemory(false) / (1024 * 1024);
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            long after = System.GC.GetTotalMemory(true) / (1024 * 1024);
            DevConsoleService.Instance.Log($"GC collected. Heap: {before} MB -> {after} MB (freed {before - after} MB)");
        }

        [ConCommand("screenshot", "Take a screenshot", "screenshot [filename]", ConCommandFlags.None)]
        public static void CmdScreenshot(ConCommandArgs args)
        {
            string filename = args.Count > 0
                ? args.GetString(0)
                : $"screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";

            if (!filename.EndsWith(".png")) filename += ".png";

            string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
            ScreenCapture.CaptureScreenshot(path);
            DevConsoleService.Instance.Log($"Screenshot saved: {path}");
        }

        [ConCommand("quit", "Quit the application", "", ConCommandFlags.None)]
        public static void CmdQuit(ConCommandArgs args)
        {
            DevConsoleService.Instance.Log("Quitting...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        [ConCommand("timescale", "Set Time.timeScale", "timescale <value>", ConCommandFlags.None)]
        public static void CmdTimeScale(ConCommandArgs args)
        {
            if (args.Count == 0)
            {
                DevConsoleService.Instance.Log($"Time.timeScale = {Time.timeScale:F2}");
                return;
            }
            float scale = Mathf.Clamp(args.GetFloat(0, 1f), 0f, 10f);
            Time.timeScale = scale;
            DevConsoleService.Instance.Log($"Time.timeScale set to {scale:F2}");
        }
    }
}
#endif
