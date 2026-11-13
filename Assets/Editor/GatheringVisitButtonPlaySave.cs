using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(GatheringVisitButtonVisual))]
public sealed class GatheringVisitButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector() && EditorApplication.isPlaying)
            GatheringVisitButtonPlaySave.Record((GatheringVisitButtonVisual)target);
    }
}

[InitializeOnLoad]
public static class GatheringVisitButtonPlaySave
{
    [Serializable]
    private sealed class ButtonSettings
    {
        public string name;
        public Vector2 buttonPosition;
        public Vector2 buttonSize;
        public float entranceDuration;
        public float offscreenPadding;
        public Vector2 characterPosition;
        public Vector2 characterSize;
        public float sideHopDistance;
        public float sideHopDuration;
        public float exitDuration;
        public Quaternion characterRotation;
    }

    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public ButtonSettings[] buttons;
        public bool changed;
    }

    private const string Key = "Sertao.GatheringVisitButtons.PlaySettings";
    private static double nextCaptureTime;

    static GatheringVisitButtonPlaySave()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += WatchPlayMode;
    }

    public static void Record(GatheringVisitButtonVisual source)
    {
        if (source == null) return;
        Settings settings = CaptureScene(source.gameObject.scene);
        settings.changed = true;
        SessionState.SetString(Key, JsonUtility.ToJson(settings));
    }

    private static Settings CaptureScene(Scene scene)
    {
        var buttons = new List<ButtonSettings>();
        foreach (GatheringVisitButtonVisual visual in UnityEngine.Object.FindObjectsByType<
            GatheringVisitButtonVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (visual.gameObject.scene == scene) buttons.Add(Capture(visual));
        buttons.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return new Settings
        {
            scenePath = scene.path,
            buttons = buttons.ToArray()
        };
    }

    private static ButtonSettings Capture(GatheringVisitButtonVisual visual)
    {
        var data = new SerializedObject(visual);
        return new ButtonSettings
        {
            name = visual.gameObject.name,
            buttonPosition = data.FindProperty("buttonPosition").vector2Value,
            buttonSize = data.FindProperty("buttonSize").vector2Value,
            entranceDuration = data.FindProperty("entranceDuration").floatValue,
            offscreenPadding = data.FindProperty("offscreenPadding").floatValue,
            characterPosition = data.FindProperty("characterPosition").vector2Value,
            characterSize = data.FindProperty("characterSize").vector2Value,
            sideHopDistance = data.FindProperty("sideHopDistance").floatValue,
            sideHopDuration = data.FindProperty("sideHopDuration").floatValue,
            exitDuration = data.FindProperty("exitDuration").floatValue,
            characterRotation = visual.Character != null ?
                visual.Character.localRotation : Quaternion.identity
        };
    }

    private static Settings Read()
    {
        string json = SessionState.GetString(Key, "");
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Settings>(json);
    }

    private static bool Different(Settings before, Settings after)
    {
        if (before?.buttons == null || after?.buttons == null ||
            before.buttons.Length != after.buttons.Length) return true;
        for (int i = 0; i < before.buttons.Length; i++)
        {
            ButtonSettings a = before.buttons[i], b = after.buttons[i];
            if (a.name != b.name ||
                (a.buttonPosition - b.buttonPosition).sqrMagnitude > 0.0001f ||
                (a.buttonSize - b.buttonSize).sqrMagnitude > 0.0001f ||
                (a.characterPosition - b.characterPosition).sqrMagnitude > 0.0001f ||
                (a.characterSize - b.characterSize).sqrMagnitude > 0.0001f ||
                !Mathf.Approximately(a.entranceDuration, b.entranceDuration) ||
                !Mathf.Approximately(a.offscreenPadding, b.offscreenPadding) ||
                !Mathf.Approximately(a.sideHopDistance, b.sideHopDistance) ||
                !Mathf.Approximately(a.sideHopDuration, b.sideHopDuration) ||
                !Mathf.Approximately(a.exitDuration, b.exitDuration) ||
                Quaternion.Angle(a.characterRotation, b.characterRotation) > 0.05f)
                return true;
        }
        return false;
    }

    private static void WatchPlayMode()
    {
        if (!EditorApplication.isPlaying ||
            EditorApplication.timeSinceStartup < nextCaptureTime) return;
        nextCaptureTime = EditorApplication.timeSinceStartup + 0.1d;
        CaptureChanges();
    }

    private static void CaptureChanges()
    {
        Settings previous = Read();
        if (previous == null) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != previous.scenePath) return;
        Settings current = CaptureScene(scene);
        if (!Different(previous, current)) return;
        current.changed = true;
        SessionState.SetString(Key, JsonUtility.ToJson(current));
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            SessionState.SetString(Key, JsonUtility.ToJson(
                CaptureScene(SceneManager.GetActiveScene())));
        else if (state == PlayModeStateChange.ExitingPlayMode)
            CaptureChanges();
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplyPending;
    }

    private static void ApplyPending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Settings settings = Read();
        if (settings == null || !settings.changed)
        {
            SessionState.EraseString(Key);
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (settings.scenePath != scene.path || settings.buttons == null)
            return;
        foreach (GatheringVisitButtonVisual visual in UnityEngine.Object.FindObjectsByType<
            GatheringVisitButtonVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (visual.gameObject.scene != scene) continue;
            foreach (ButtonSettings saved in settings.buttons)
            {
                if (saved.name != visual.gameObject.name) continue;
                var data = new SerializedObject(visual);
                data.FindProperty("buttonPosition").vector2Value = saved.buttonPosition;
                data.FindProperty("buttonSize").vector2Value = saved.buttonSize;
                data.FindProperty("entranceDuration").floatValue = saved.entranceDuration;
                data.FindProperty("offscreenPadding").floatValue = saved.offscreenPadding;
                data.FindProperty("characterPosition").vector2Value = saved.characterPosition;
                data.FindProperty("characterSize").vector2Value = saved.characterSize;
                data.FindProperty("sideHopDistance").floatValue = saved.sideHopDistance;
                data.FindProperty("sideHopDuration").floatValue = saved.sideHopDuration;
                data.FindProperty("exitDuration").floatValue = saved.exitDuration;
                data.ApplyModifiedPropertiesWithoutUndo();
                visual.RefreshLayout();
                if (visual.Character != null)
                    visual.Character.localRotation = saved.characterRotation;
                break;
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
