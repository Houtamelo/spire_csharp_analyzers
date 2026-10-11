using System;
using UnityEditor;
using UnityEngine;

public sealed class ProbeAsset : ScriptableObject
{
    public Snapshot<string> snapshot;

    [MenuItem("Repro/Create asset")]
    public static void Reproduce()
    {
        Debug.Log("MRP_BEGIN=" + Application.unityVersion);
        AssetDatabase.CreateAsset(CreateInstance<ProbeAsset>(), "Assets/Probe.asset");
        Debug.Log("MRP_PASS=" + Application.unityVersion);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}

[Serializable]
public struct Snapshot<T> : ISerializationCallbackReceiver
{
    [SerializeReference] public object payload;

    public void OnBeforeSerialize() => Debug.Log("callback");
    public void OnAfterDeserialize() { }
}
