using System;
using DG.Tweening;
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
        public Vector2 panelOffset;
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
        RectTransform clip = timeline.PanelClip;
        if (previous.values == null || previous.values.Length != Fields.Length ||
            previous.totalDays != current.totalDays ||
            (previous.panelOffset - current.panelOffset).sqrMagnitude > 0.0001f)
        {
            if (clip != null) clip.anchoredPosition = timeline.PanelClipPosition;
            RecordCurrent(timeline);
            return;
        }
        for (int i = 0; i < Fields.Length; i++)
            if (!Mathf.Approximately(previous.values[i], current.values[i]))
            {
                if (clip != null) clip.anchoredPosition = timeline.PanelClipPosition;
                RecordCurrent(timeline);
                return;
            }
        CaptureMovedClip(timeline);
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
            DayTimeline timeline = original != null ? FindTimeline(original.scenePath) : null;
            if (CaptureMovedClip(timeline)) return;
            // Inspector edits were captured when they happened. Do not replace them
            // with values restored by Unity while Play Mode is closing.
            if (original != null && original.changed) return;
            if (timeline == null) return;
            Settings current = Capture(timeline);
            current.changed = original.values == null ||
                original.values.Length != Fields.Length ||
                original.totalDays != current.totalDays ||
                (original.panelOffset - current.panelOffset).sqrMagnitude > 0.0001f;
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

    private static bool CaptureMovedClip(DayTimeline timeline)
    {
        RectTransform clip = timeline != null ? timeline.PanelClip : null;
        if (clip == null) return false;
        Vector2 delta = clip.anchoredPosition - timeline.PanelClipPosition;
        RectTransform panel = timeline.TimelinePanel;
        bool movedPanel = delta.sqrMagnitude <= 0.0001f && timeline.IsOpen &&
            panel != null && !DOTween.IsTweening(panel);
        if (movedPanel) delta = panel.anchoredPosition - timeline.PanelHomePosition;
        if (delta.sqrMagnitude <= 0.0001f) return false;
        var moved = new SerializedObject(timeline);
        SerializedProperty offset = moved.FindProperty("panelOffset");
        offset.vector2Value += delta;
        moved.ApplyModifiedPropertiesWithoutUndo();
        clip.anchoredPosition = timeline.PanelClipPosition;
        if (movedPanel) panel.anchoredPosition = timeline.PanelHomePosition;
        RecordCurrent(timeline);
        return true;
    }

    private static Settings Capture(DayTimeline timeline)
    {
        var source = new SerializedObject(timeline);
        var settings = new Settings
        {
            scenePath = timeline.gameObject.scene.path,
            totalDays = source.FindProperty("totalDays").intValue,
            panelOffset = source.FindProperty("panelOffset").vector2Value,
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
        destination.FindProperty("panelOffset").vector2Value = settings.panelOffset;
        for (int i = 0; settings.values != null &&
            i < Fields.Length && i < settings.values.Length; i++)
            destination.FindProperty(Fields[i]).floatValue = settings.values[i];
        destination.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
