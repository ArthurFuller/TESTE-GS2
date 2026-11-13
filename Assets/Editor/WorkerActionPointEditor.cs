using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(WorkerAssignment))]
public sealed class WorkerActionPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector())
            WorkerActionPointSync.CopyFrom((WorkerAssignment)target);
    }
}

[InitializeOnLoad]
public static class WorkerActionPointSync
{
    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public string spriteId;
        public float size;
        public Vector3 offset;
        public float spacing;
        public int previewCount;
    }

    private const string Key = "Sertao.WorkerActionPoints.PlaySettings";

    static WorkerActionPointSync()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void CopyFrom(WorkerAssignment source)
    {
        if (source == null || !source.gameObject.scene.IsValid()) return;
        Settings settings = Capture(source);
        Apply(settings, source.gameObject.scene);
        if (EditorApplication.isPlaying)
            SessionState.SetString(Key, JsonUtility.ToJson(settings));
    }

    private static Settings Capture(WorkerAssignment source)
    {
        var data = new SerializedObject(source);
        var sprite = data.FindProperty("actionPointSprite").objectReferenceValue;
        return new Settings
        {
            scenePath = source.gameObject.scene.path,
            spriteId = sprite != null ? GlobalObjectId.GetGlobalObjectIdSlow(sprite).ToString() : "",
            size = data.FindProperty("actionPointSize").floatValue,
            offset = data.FindProperty("actionPointOffset").vector3Value,
            spacing = data.FindProperty("actionPointSpacing").floatValue,
            previewCount = data.FindProperty("previewActionPoints").intValue
        };
    }

    private static void Apply(Settings settings, Scene scene)
    {
        Sprite sprite = null;
        if (GlobalObjectId.TryParse(settings.spriteId, out GlobalObjectId id))
            sprite = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Sprite;
        foreach (WorkerAssignment worker in UnityEngine.Object.FindObjectsByType<WorkerAssignment>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (worker.gameObject.scene != scene) continue;
            var data = new SerializedObject(worker);
            data.FindProperty("actionPointSprite").objectReferenceValue = sprite;
            data.FindProperty("actionPointSize").floatValue = settings.size;
            data.FindProperty("actionPointOffset").vector3Value = settings.offset;
            data.FindProperty("actionPointSpacing").floatValue = settings.spacing;
            data.FindProperty("previewActionPoints").intValue = settings.previewCount;
            data.ApplyModifiedProperties();
            worker.RefreshActionPointAppearance();
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            SessionState.EraseString(Key);
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplyPending;
    }

    private static void ApplyPending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string json = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) return;
        Settings settings = JsonUtility.FromJson<Settings>(json);
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != settings.scenePath) return;
        Apply(settings, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
