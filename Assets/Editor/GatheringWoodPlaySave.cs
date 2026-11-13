using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GatheringWoodPlaySave
{
    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public Vector2 targetSize;
        public float targetY;
        public Vector2 trackSize;
        public Vector2 woodSize;
        public float sidePadding;
        public float edgePadding;
    }

    private const string BaselineKey = "GS2.WoodMinigame.Baseline";
    private const string PendingKey = "GS2.WoodMinigame.Pending";

    static GatheringWoodPlaySave() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

    private static GatheringMinigame FindMinigame() =>
        UnityEngine.Object.FindFirstObjectByType<GatheringMinigame>(FindObjectsInactive.Include);

    private static Settings Capture()
    {
        GatheringMinigame minigame = FindMinigame();
        if (minigame == null) return null;
        var data = new SerializedObject(minigame);
        var target = data.FindProperty("greenTarget").objectReferenceValue as RectTransform;
        var track = data.FindProperty("woodTrack").objectReferenceValue as RectTransform;
        var wood = data.FindProperty("woodArtRoot").objectReferenceValue as RectTransform;
        if (target == null || track == null || wood == null) return null;
        return new Settings
        {
            scenePath = minigame.gameObject.scene.path,
            targetSize = target.sizeDelta,
            targetY = target.anchoredPosition.y,
            trackSize = track.sizeDelta,
            woodSize = wood.sizeDelta,
            sidePadding = data.FindProperty("targetSidePadding").floatValue,
            edgePadding = data.FindProperty("edgePadding").floatValue
        };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            SessionState.EraseString(PendingKey);
            Settings baseline = Capture();
            SessionState.SetString(BaselineKey, baseline == null ? "" : JsonUtility.ToJson(baseline));
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Settings current = Capture();
            if (current != null && JsonUtility.ToJson(current) != SessionState.GetString(BaselineKey, ""))
                SessionState.SetString(PendingKey, JsonUtility.ToJson(current));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplyPending;
    }

    private static void ApplyPending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string json = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return;
        Settings settings = JsonUtility.FromJson<Settings>(json);
        GatheringMinigame minigame = FindMinigame();
        if (minigame == null || minigame.gameObject.scene.path != settings.scenePath) return;
        var data = new SerializedObject(minigame);
        var target = data.FindProperty("greenTarget").objectReferenceValue as RectTransform;
        var track = data.FindProperty("woodTrack").objectReferenceValue as RectTransform;
        var wood = data.FindProperty("woodArtRoot").objectReferenceValue as RectTransform;
        if (target == null || track == null || wood == null) return;
        target.sizeDelta = settings.targetSize;
        target.anchoredPosition = new Vector2(target.anchoredPosition.x, settings.targetY);
        track.sizeDelta = settings.trackSize;
        wood.sizeDelta = settings.woodSize;
        data.FindProperty("targetSidePadding").floatValue = settings.sidePadding;
        data.FindProperty("edgePadding").floatValue = settings.edgePadding;
        data.ApplyModifiedPropertiesWithoutUndo();
        Scene scene = minigame.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(PendingKey);
    }
}
