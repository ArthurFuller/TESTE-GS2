using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CommunityCameraPlaySave
{
    [Serializable]
    private sealed class Settings
    {
        public string scenePath;
        public bool changed;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 minimum;
        public Vector3 maximum;
        public bool useAdditionalPositionLimits;
        public bool useCustomViewBounds;
        public Vector2 minimumView;
        public Vector2 maximumView;
        public float fieldOfView;
        public float panSpeed;
        public float zoomStep;
        public float keyboardZoomSpeed;
        public float closestDistance;
        public float farthestDistance;
        public float followDistance;
        public float smoothTime;
        public float farZoomPanRadius;
    }

    private const string Key = "Sertao.CommunityCamera.PlaySettings";

    static CommunityCameraPlaySave()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RecordPose(CommunityCamera camera)
    {
        Record(settings =>
        {
            settings.position = camera.ViewTransform.localPosition;
            settings.rotation = camera.ViewTransform.localEulerAngles;
        });
    }

    public static void RecordFieldOfView(float value)
    {
        Record(settings => settings.fieldOfView = value);
    }

    public static void RecordAdditionalLimits(bool value)
    {
        Record(settings => settings.useAdditionalPositionLimits = value);
    }

    public static void RecordCustomBounds(bool enabled, Vector2 minimum, Vector2 maximum)
    {
        Record(settings =>
        {
            settings.useCustomViewBounds = enabled;
            settings.minimumView = minimum;
            settings.maximumView = maximum;
        });
    }

    public static void RecordCurrentView(CommunityCamera camera)
    {
        Record(settings =>
        {
            settings.position = camera.ViewTransform.localPosition;
            settings.rotation = camera.ViewTransform.localEulerAngles;
            settings.fieldOfView = camera.FieldOfView;
        });
    }

    public static void RecordField(string name, float value)
    {
        Record(settings =>
        {
            switch (name)
            {
                case "panSpeed": settings.panSpeed = value; break;
                case "zoomStep": settings.zoomStep = value; break;
                case "keyboardZoomSpeed": settings.keyboardZoomSpeed = value; break;
                case "closestDistance": settings.closestDistance = value; break;
                case "farthestDistance": settings.farthestDistance = value; break;
                case "followDistance": settings.followDistance = value; break;
                case "smoothTime": settings.smoothTime = value; break;
                case "farZoomPanRadius": settings.farZoomPanRadius = value; break;
            }
        });
    }

    public static void RecordBoundary(bool minimum, Vector3 position)
    {
        Record(settings =>
        {
            if (minimum) settings.minimum = position;
            else settings.maximum = position;
        });
    }

    private static void Record(Action<Settings> change)
    {
        if (!EditorApplication.isPlaying) return;
        Settings settings = Read();
        if (settings == null) return;
        change(settings);
        settings.changed = true;
        SessionState.SetString(Key, JsonUtility.ToJson(settings));
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            CommunityCamera camera = FindCamera(SceneManager.GetActiveScene().path);
            if (camera != null)
                SessionState.SetString(Key, JsonUtility.ToJson(Capture(camera)));
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Settings settings = Read();
            CommunityCamera camera = settings != null ? FindCamera(settings.scenePath) : null;
            if (camera != null)
            {
                Settings current = Capture(camera);
                settings.changed |= settings.fieldOfView != current.fieldOfView ||
                    settings.minimum != current.minimum || settings.maximum != current.maximum ||
                    settings.useAdditionalPositionLimits != current.useAdditionalPositionLimits ||
                    settings.useCustomViewBounds != current.useCustomViewBounds ||
                    settings.minimumView != current.minimumView || settings.maximumView != current.maximumView ||
                    settings.panSpeed != current.panSpeed || settings.zoomStep != current.zoomStep ||
                    settings.keyboardZoomSpeed != current.keyboardZoomSpeed ||
                    settings.closestDistance != current.closestDistance ||
                    settings.farthestDistance != current.farthestDistance ||
                    settings.followDistance != current.followDistance || settings.smoothTime != current.smoothTime ||
                    settings.farZoomPanRadius != current.farZoomPanRadius;
                settings.fieldOfView = current.fieldOfView;
                settings.minimum = current.minimum;
                settings.maximum = current.maximum;
                settings.useAdditionalPositionLimits = current.useAdditionalPositionLimits;
                settings.useCustomViewBounds = current.useCustomViewBounds;
                settings.minimumView = current.minimumView;
                settings.maximumView = current.maximumView;
                settings.panSpeed = current.panSpeed;
                settings.zoomStep = current.zoomStep;
                settings.keyboardZoomSpeed = current.keyboardZoomSpeed;
                settings.closestDistance = current.closestDistance;
                settings.farthestDistance = current.farthestDistance;
                settings.followDistance = current.followDistance;
                settings.smoothTime = current.smoothTime;
                settings.farZoomPanRadius = current.farZoomPanRadius;
                SessionState.SetString(Key, JsonUtility.ToJson(settings));
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ApplySavedSettings;
    }

    private static Settings Capture(CommunityCamera camera)
    {
        SerializedObject source = new SerializedObject(camera);
        Transform minimum = source.FindProperty("minimumCorner").objectReferenceValue as Transform;
        Transform maximum = source.FindProperty("maximumCorner").objectReferenceValue as Transform;
        return new Settings
        {
            scenePath = camera.gameObject.scene.path,
            position = camera.ViewTransform.localPosition,
            rotation = camera.ViewTransform.localEulerAngles,
            fieldOfView = camera.FieldOfView,
            minimum = minimum != null ? minimum.position : Vector3.zero,
            maximum = maximum != null ? maximum.position : Vector3.zero,
            useAdditionalPositionLimits = source.FindProperty("useAdditionalPositionLimits").boolValue,
            useCustomViewBounds = source.FindProperty("useCustomViewBounds").boolValue,
            minimumView = source.FindProperty("minimumView").vector2Value,
            maximumView = source.FindProperty("maximumView").vector2Value,
            panSpeed = source.FindProperty("panSpeed").floatValue,
            zoomStep = source.FindProperty("zoomStep").floatValue,
            keyboardZoomSpeed = source.FindProperty("keyboardZoomSpeed").floatValue,
            closestDistance = source.FindProperty("closestDistance").floatValue,
            farthestDistance = source.FindProperty("farthestDistance").floatValue,
            followDistance = source.FindProperty("followDistance").floatValue,
            smoothTime = source.FindProperty("smoothTime").floatValue,
            farZoomPanRadius = source.FindProperty("farZoomPanRadius").floatValue
        };
    }

    private static Settings Read()
    {
        string data = SessionState.GetString(Key, "");
        return string.IsNullOrEmpty(data) ? null : JsonUtility.FromJson<Settings>(data);
    }

    private static CommunityCamera FindCamera(string scenePath)
    {
        foreach (CommunityCamera camera in UnityEngine.Object.FindObjectsByType<CommunityCamera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (camera.gameObject.scene.path == scenePath) return camera;
        return null;
    }

    private static void ApplySavedSettings()
    {
        Settings settings = Read();
        if (settings == null || !settings.changed)
        {
            SessionState.EraseString(Key);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        CommunityCamera camera = scene.path == settings.scenePath ? FindCamera(scene.path) : null;
        if (camera == null) return;

        camera.ViewTransform.localPosition = settings.position;
        camera.ViewTransform.localEulerAngles = settings.rotation;
        camera.FieldOfView = settings.fieldOfView;

        SerializedObject destination = new SerializedObject(camera);
        Transform minimum = destination.FindProperty("minimumCorner").objectReferenceValue as Transform;
        Transform maximum = destination.FindProperty("maximumCorner").objectReferenceValue as Transform;
        if (minimum != null) minimum.position = settings.minimum;
        if (maximum != null) maximum.position = settings.maximum;
        destination.FindProperty("useAdditionalPositionLimits").boolValue = settings.useAdditionalPositionLimits;
        destination.FindProperty("useCustomViewBounds").boolValue = settings.useCustomViewBounds;
        destination.FindProperty("minimumView").vector2Value = settings.minimumView;
        destination.FindProperty("maximumView").vector2Value = settings.maximumView;
        destination.FindProperty("panSpeed").floatValue = settings.panSpeed;
        destination.FindProperty("zoomStep").floatValue = settings.zoomStep;
        destination.FindProperty("keyboardZoomSpeed").floatValue = settings.keyboardZoomSpeed;
        destination.FindProperty("closestDistance").floatValue = settings.closestDistance;
        destination.FindProperty("farthestDistance").floatValue = settings.farthestDistance;
        destination.FindProperty("followDistance").floatValue = settings.followDistance;
        destination.FindProperty("smoothTime").floatValue = settings.smoothTime;
        destination.FindProperty("farZoomPanRadius").floatValue = settings.farZoomPanRadius;
        destination.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (EditorSceneManager.SaveScene(scene)) SessionState.EraseString(Key);
    }
}
