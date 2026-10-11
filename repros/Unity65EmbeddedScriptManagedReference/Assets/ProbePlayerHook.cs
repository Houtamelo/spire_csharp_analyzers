using UnityEngine;

public sealed class ProbePlayerHook : MonoBehaviour
{
    private void Start()
    {
#if ENABLE_IL2CPP
        const string backend = "IL2CPP";
#else
        const string backend = "Mono";
#endif
        Debug.Log("MRP_SCENE_LOADED=" + Application.unityVersion + ";backend=" + backend);
        Application.Quit(0);
    }
}

public sealed class ProbeHost : MonoBehaviour
{
    [SerializeReference] public object value;
}
