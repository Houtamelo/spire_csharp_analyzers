#nullable enable

using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    [UnityEngine.Scripting.Preserve]
    internal sealed class CorePlayerExitScheduler : MonoBehaviour
    {
        private int _exitCode;
        private bool _scheduled;

        // Unity 2023.2 can crash during startup native cleanup when quit is requested from AfterSceneLoad.
        internal static void Schedule(int exitCode)
        {
            var host = new GameObject("SpireCorePlayerExitScheduler");
            Object.DontDestroyOnLoad(host);
            var scheduler = host.AddComponent<CorePlayerExitScheduler>();
            scheduler._exitCode = exitCode;
            scheduler._scheduled = true;
        }

        private void Update()
        {
            if (!_scheduled)
                return;

            _scheduled = false;
            Application.Quit(_exitCode);
        }
    }

}
