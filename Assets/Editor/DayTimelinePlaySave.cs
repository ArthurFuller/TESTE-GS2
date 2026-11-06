using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class DayTimelinePlaySave
{
    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public int totalDays;
        public float[] values;
        public bool changed;
    }

    private const string Key = "Sertao.DayTimeline.PlaySettings";
    private static double nextCaptureTime;
    private static readonly string[] Fields =
    {
        "panelWidth", "daysHeight", "cardsHeight", "topBarHeight",
        "dayButtonWidth", "dayButtonHeight", "dayLineHeight",
        "cardWidth", "cardHeight", "cardGap", "cardLineHeight",
        "cardOverlap", "pegSize", "hangingAngle", "cardHoverScale",
        "cardStartX", "cardTopInset", "cardLineOffsetY",
        "slideDuration", "waveDelay",
        "inspectionTilt", "inspectionShift", "inspectionFollowSpeed"
    };

    static DayTimelinePlaySave()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += EditorUpdate;
    }

    public static void RecordCurrent(DayTimeline timeline)
    {
        if (!EditorApplication.isPlaying || timeline == null) return;
        Settings settings = Capture(timeline);
        Settings previous = Read();
        if (previous != null && !string.IsNullOrEmpty(previous.scenePath))
            settings.scenePath = previous.scenePath;
        settings.changed = true;
        SessionState.SetString(Key, JsonUtility.ToJson(settings));
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
        DayTimeline timeline = previous != null ? FindTimeline(previous.scenePath) : null;
        if (timeline == null) return;
        Settings current = Capture(timeline);
        if (previous.values == null || previous.values.Length != Fields.Length ||
            previous.totalDays != current.totalDays)
        {
            RecordCurrent(timeline);
            return;
        }
        for (int i = 0; i < Fields.Length; i++)
            if (!Mathf.Approximately(previous.values[i], current.values[i]))
            {
                RecordCurrent(timeline);
                return;
            }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            DayTimeline timeline = FindTimeline(SceneManager.GetActiveScene().path);
            if (timeline != null)
                SessionState.SetString(Key, JsonUtility.ToJson(Capture(timeline)));
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Settings original = Read();
            // Inspector edits were captured when they happened. Do not replace them
            // with values restored by Unity while Play Mode is closing.
            if (original != null && original.changed) return;
            DayTimeline timeline = original != null ? FindTimeline(original.scenePath) : null;
            if (timeline == null) return;
            Settings current = Capture(timeline);
            current.changed = original.values == null ||
                original.values.Length != Fields.Length ||
                original.totalDays != current.totalDays;
            for (int i = 0; i < Fields.Length; i++)
                if (original.values != null && i < original.values.Length &&
                    !Mathf.Approximately(original.values[i], current.values[i]))
                    current.changed = true;
            SessionState.SetString(Key, JsonUtility.ToJson(current));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplyPending;
    }

    private static DayTimeline FindTimeline(string scenePath)
    {
        foreach (DayTimeline timeline in UnityEngine.Object.FindObjectsByType<DayTimeline>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (timeline.gameObject.scene.path == scenePath) return timeline;
        return null;
    }

    private static Settings Capture(DayTimeline timeline)
    {
        var source = new SerializedObject(timeline);
        var settings = new Settings
        {
            scenePath = timeline.gameObject.scene.path,
            totalDays = source.FindProperty("totalDays").intValue,
            values = new float[Fields.Length]
        };
        for (int i = 0; i < Fields.Length; i++)
            settings.values[i] = source.FindProperty(Fields[i]).floatValue;
        return settings;
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
        DayTimeline timeline = scene.path == settings.scenePath ? FindTimeline(scene.path) : null;
        if (timeline == null) return;

        var destination = new SerializedObject(timeline);
        destination.FindProperty("totalDays").intValue = settings.totalDays;
        for (int i = 0; settings.values != null &&
            i < Fields.Length && i < settings.values.Length; i++)
            destination.FindProperty(Fields[i]).floatValue = settings.values[i];
        destination.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
