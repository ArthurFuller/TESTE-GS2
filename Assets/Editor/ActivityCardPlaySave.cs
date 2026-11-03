using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ActivityCardPlaySave
{
    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public int maxHandCards;
        public float[] values;
        public Vector2 containerSize;
        public Vector2 containerPosition;
        public bool changed;
    }

    private const string Key = "Sertao.ActivityCards.PlaySettings";
    private static readonly string[] Fields =
    {
        "handWidth", "cardSpacing", "cardBaseY", "arcHeight", "arcAngle",
        "hoverLift", "hoverScale", "hoverDuration", "hoverSpread", "descriptionGap",
        "containerPadding"
    };

    static ActivityCardPlaySave()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            ActivityCardAvailability hand = FindHand(SceneManager.GetActiveScene().path);
            if (hand != null) SessionState.SetString(Key, JsonUtility.ToJson(Capture(hand)));
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Settings original = Read();
            ActivityCardAvailability hand = original != null ? FindHand(original.scenePath) : null;
            if (hand == null) return;
            Settings current = Capture(hand);
            current.changed = original.maxHandCards != current.maxHandCards;
            for (int i = 0; i < Fields.Length; i++)
                if (!Mathf.Approximately(original.values[i], current.values[i])) current.changed = true;
            if (original.containerSize != current.containerSize ||
                original.containerPosition != current.containerPosition) current.changed = true;
            SessionState.SetString(Key, JsonUtility.ToJson(current));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += Apply;
    }

    private static ActivityCardAvailability FindHand(string scenePath)
    {
        foreach (ActivityCardAvailability hand in UnityEngine.Object.FindObjectsByType<ActivityCardAvailability>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (hand.gameObject.scene.path == scenePath) return hand;
        return null;
    }

    private static Settings Capture(ActivityCardAvailability hand)
    {
        var source = new SerializedObject(hand);
        var settings = new Settings
        {
            scenePath = hand.gameObject.scene.path,
            maxHandCards = source.FindProperty("maxHandCards").intValue,
            values = new float[Fields.Length],
            containerSize = ((RectTransform)hand.transform.parent).sizeDelta,
            containerPosition = ((RectTransform)hand.transform.parent).anchoredPosition
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

    private static void Apply()
    {
        Settings settings = Read();
        if (settings == null || !settings.changed)
        {
            SessionState.EraseString(Key);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        ActivityCardAvailability hand = scene.path == settings.scenePath ? FindHand(scene.path) : null;
        if (hand == null) return;

        var destination = new SerializedObject(hand);
        destination.FindProperty("maxHandCards").intValue = settings.maxHandCards;
        for (int i = 0; i < Fields.Length; i++)
            destination.FindProperty(Fields[i]).floatValue = settings.values[i];
        destination.ApplyModifiedPropertiesWithoutUndo();
        RectTransform container = (RectTransform)hand.transform.parent;
        container.sizeDelta = settings.containerSize;
        container.anchoredPosition = settings.containerPosition;
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
