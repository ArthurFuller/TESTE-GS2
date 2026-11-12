using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mantém os marcadores de percurso ajustados no Play Mode ao voltar ao editor.
[InitializeOnLoad]
public static class HouseRoutePlaySave
{
    [Serializable]
    private sealed class MarkerPose
    {
        public string name;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
    }

    [Serializable]
    private sealed class ScaleValue
    {
        public string objectName;
        public string fieldName;
        public float value;
    }

    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public MarkerPose[] markers;
        public ScaleValue[] scales;
        public bool changed;
    }

    private const string Key = "Sertao.HouseRoute.PlaySettings";

    static HouseRoutePlaySave()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            Settings initial = Capture(SceneManager.GetActiveScene().path);
            if (initial != null) SessionState.SetString(Key, JsonUtility.ToJson(initial));
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Settings initial = Read();
            if (initial == null) return;
            Settings current = Capture(initial.scenePath);
            if (current == null) return;
            current.changed = HasChanges(initial, current);
            SessionState.SetString(Key, JsonUtility.ToJson(current));
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += Apply;
    }

    private static Settings Capture(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath)) return null;
        var poses = new Dictionary<string, MarkerPose>();
        var scales = new List<ScaleValue>();
        foreach (WorkerAssignment worker in UnityEngine.Object.FindObjectsByType<WorkerAssignment>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (worker.gameObject.scene.path != scenePath) continue;
            Add(poses, worker, "homeMarker", "outsideMarker", "doorwayMarker");
            AddScale(scales, worker, "indoorScale");
        }
        foreach (HouseSite site in UnityEngine.Object.FindObjectsByType<HouseSite>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (site.gameObject.scene.path == scenePath)
                Add(poses, site, "homeMarker", "outsideMarker", "doorwayMarker");
        foreach (HouseInteriorTest test in UnityEngine.Object.FindObjectsByType<HouseInteriorTest>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (test.gameObject.scene.path != scenePath) continue;
            Add(poses, test, "outside", "doorway", "returnSpot", "activitySpot");
            AddScale(scales, test, "indoorSpriteScale");
            AddScale(scales, test, "walkingSpeed");
        }
        return new Settings { scenePath = scenePath,
            markers = new List<MarkerPose>(poses.Values).ToArray(), scales = scales.ToArray() };
    }

    private static void Add(Dictionary<string, MarkerPose> poses,
        MonoBehaviour owner, params string[] fieldNames)
    {
        var source = new SerializedObject(owner);
        foreach (string fieldName in fieldNames)
        {
            SerializedProperty property = source.FindProperty(fieldName);
            Transform marker = property != null ? property.objectReferenceValue as Transform : null;
            if (marker == null || poses.ContainsKey(marker.name)) continue;
            poses.Add(marker.name, new MarkerPose { name = marker.name,
                localPosition = marker.localPosition, localEulerAngles = marker.localEulerAngles });
        }
    }

    private static void AddScale(List<ScaleValue> values, MonoBehaviour owner, string fieldName)
    {
        SerializedProperty property = new SerializedObject(owner).FindProperty(fieldName);
        if (property != null) values.Add(new ScaleValue { objectName = owner.gameObject.name,
            fieldName = fieldName, value = property.floatValue });
    }

    private static bool HasChanges(Settings old, Settings current)
    {
        var previous = new Dictionary<string, MarkerPose>();
        foreach (MarkerPose marker in old.markers) previous[marker.name] = marker;
        foreach (MarkerPose marker in current.markers)
            if (!previous.TryGetValue(marker.name, out MarkerPose first) ||
                first.localPosition != marker.localPosition ||
                first.localEulerAngles != marker.localEulerAngles) return true;
        var oldScales = new Dictionary<string, float>();
        foreach (ScaleValue scale in old.scales) oldScales[scale.objectName + "/" + scale.fieldName] = scale.value;
        foreach (ScaleValue scale in current.scales)
            if (!oldScales.TryGetValue(scale.objectName + "/" + scale.fieldName, out float value) ||
                !Mathf.Approximately(value, scale.value)) return true;
        return false;
    }

    private static Settings Read()
    {
        string json = SessionState.GetString(Key, "");
        return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Settings>(json);
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
        if (scene.path != settings.scenePath) return;
        var poses = new Dictionary<string, MarkerPose>();
        foreach (MarkerPose marker in settings.markers) poses[marker.name] = marker;
        foreach (Transform transform in UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (transform.gameObject.scene.path != scene.path ||
                !poses.TryGetValue(transform.name, out MarkerPose pose)) continue;
            transform.localPosition = pose.localPosition;
            transform.localEulerAngles = pose.localEulerAngles;
        }
        // Os trabalhadores já presentes na cena também devem aparecer sobre a nova origem.
        foreach (WorkerAssignment worker in UnityEngine.Object.FindObjectsByType<WorkerAssignment>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (worker.gameObject.scene.path != scene.path) continue;
            SerializedProperty home = new SerializedObject(worker).FindProperty("homeMarker");
            if (home != null && home.objectReferenceValue is Transform marker)
                worker.transform.position = marker.position;
        }
        foreach (ScaleValue scale in settings.scales)
        {
            foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component == null || component.gameObject.scene.path != scene.path ||
                    component.gameObject.name != scale.objectName) continue;
                var destination = new SerializedObject(component);
                SerializedProperty property = destination.FindProperty(scale.fieldName);
                if (property == null || property.propertyType != SerializedPropertyType.Float) continue;
                property.floatValue = scale.value;
                destination.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
