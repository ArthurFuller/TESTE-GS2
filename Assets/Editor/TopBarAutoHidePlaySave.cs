using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TopBarAutoHidePlaySave
{
    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public Vector2 openPosition;
        public Vector2 closedPosition;
        public Vector2 timelineDayPosition;
        public float[] values;
        public bool changed;
    }

    private const string Key = "Sertao.TopBar.PlaySettings";
    private static readonly string[] FloatFields =
        { "idleSeconds", "slideDuration", "waveDelay", "widgetOffset" };
    private static double nextCaptureTime;

    static TopBarAutoHidePlaySave()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += EditorUpdate;
    }

    public static void RecordCurrent(TopBarAutoHide bar)
    {
        if (!EditorApplication.isPlaying || bar == null) return;
        Settings current = Capture(bar);
        Settings previous = Read();
        if (previous != null && !string.IsNullOrEmpty(previous.scenePath))
            current.scenePath = previous.scenePath;
        current.changed = true;
        SessionState.SetString(Key, JsonUtility.ToJson(current));
    }

    private static void EditorUpdate()
    {
        if (!EditorApplication.isPlaying)
        {
            ApplyPending();
            return;
        }
        if (EditorApplication.timeSinceStartup < nextCaptureTime) return;
        nextCaptureTime = EditorApplication.timeSinceStartup + 0.1d;
        Settings previous = Read();
        TopBarAutoHide bar = previous != null ? FindBar(previous.scenePath) : null;
        if (bar == null) return;
        if (Different(previous, Capture(bar)))
        {
            bar.PreviewCurrentPosition();
            RecordCurrent(bar);
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            TopBarAutoHide bar = FindBar(SceneManager.GetActiveScene().path);
            if (bar != null) SessionState.SetString(Key, JsonUtility.ToJson(Capture(bar)));
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Settings previous = Read();
            if (previous == null) return;
            TopBarAutoHide bar = FindBar(previous.scenePath);
            if (previous.changed) return;
            if (bar == null) return;
            Settings current = Capture(bar);
            current.changed = Different(previous, current);
            SessionState.SetString(Key, JsonUtility.ToJson(current));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplyPending;
    }

    private static TopBarAutoHide FindBar(string scenePath)
    {
        foreach (TopBarAutoHide bar in UnityEngine.Object.FindObjectsByType<TopBarAutoHide>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (bar.gameObject.scene.path == scenePath) return bar;
        return null;
    }

    private static Settings Capture(TopBarAutoHide bar)
    {
        var source = new SerializedObject(bar);
        var settings = new Settings
        {
            scenePath = bar.gameObject.scene.path,
            openPosition = source.FindProperty("openPosition").vector2Value,
            closedPosition = source.FindProperty("closedPosition").vector2Value,
            timelineDayPosition = source.FindProperty("timelineDayPosition").vector2Value,
            values = new float[FloatFields.Length]
        };
        for (int i = 0; i < FloatFields.Length; i++)
            settings.values[i] = source.FindProperty(FloatFields[i]).floatValue;
        return settings;
    }

    private static bool Different(Settings before, Settings after)
    {
        if ((before.openPosition - after.openPosition).sqrMagnitude > 0.0001f ||
            (before.closedPosition - after.closedPosition).sqrMagnitude > 0.0001f ||
            (before.timelineDayPosition - after.timelineDayPosition).sqrMagnitude > 0.0001f ||
            before.values == null || before.values.Length != FloatFields.Length)
            return true;
        for (int i = 0; i < FloatFields.Length; i++)
            if (!Mathf.Approximately(before.values[i], after.values[i])) return true;
        return false;
    }

    private static Settings Read()
    {
        string data = SessionState.GetString(Key, "");
        return string.IsNullOrEmpty(data) ? null : JsonUtility.FromJson<Settings>(data);
    }

    private static void ApplyPending()
    {
        Settings settings = Read();
        if (settings == null || !settings.changed ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        TopBarAutoHide bar = scene.path == settings.scenePath ? FindBar(scene.path) : null;
        if (bar == null) return;
        var destination = new SerializedObject(bar);
        destination.FindProperty("openPosition").vector2Value = settings.openPosition;
        destination.FindProperty("closedPosition").vector2Value = settings.closedPosition;
        destination.FindProperty("timelineDayPosition").vector2Value =
            settings.timelineDayPosition;
        for (int i = 0; settings.values != null && i < FloatFields.Length &&
            i < settings.values.Length; i++)
            destination.FindProperty(FloatFields[i]).floatValue = settings.values[i];
        destination.ApplyModifiedPropertiesWithoutUndo();
        var visual = destination.FindProperty("bar").objectReferenceValue as RectTransform;
        if (visual != null) visual.anchoredPosition = settings.openPosition;
        var art = destination.FindProperty("background").objectReferenceValue as RectTransform;
        if (visual != null && art != null && art.parent != visual && visual.rect.width > 0f)
            art.anchoredPosition = settings.openPosition * (art.rect.width / visual.rect.width);
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
