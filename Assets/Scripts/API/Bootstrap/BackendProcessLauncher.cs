using UnityEngine;

#if !UNITY_EDITOR
using System.Diagnostics;
using System.IO;
#endif

namespace Game.API
{
    public class BackendProcessLauncher : MonoBehaviour
    {
#if !UNITY_EDITOR
        private Process backendProcess;
#endif

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        public void Launch()
        {
#if !UNITY_EDITOR
            var serverExe = Path.Combine(Application.streamingAssetsPath, "Server", "Trading-Game-DOTNET.exe");

            if (!File.Exists(serverExe))
            {
                UnityEngine.Debug.LogError($"[BackendLauncher] Server executable not found at: {serverExe}");
                return;
            }

            backendProcess = Process.Start(new ProcessStartInfo
            {
                FileName = serverExe,
                WorkingDirectory = Path.GetDirectoryName(serverExe),
                CreateNoWindow = true,
                UseShellExecute = false
            });

            UnityEngine.Debug.Log("[BackendLauncher] Backend server started.");
#else
            UnityEngine.Debug.Log("[BackendLauncher] Skipped in Editor — run backend manually.");
#endif
        }

        private void OnApplicationQuit()
        {
#if !UNITY_EDITOR
            if (backendProcess != null && !backendProcess.HasExited)
            {
                backendProcess.Kill();
                backendProcess.Dispose();
                UnityEngine.Debug.Log("[BackendLauncher] Backend server stopped.");
            }
#endif
        }
    }
}
