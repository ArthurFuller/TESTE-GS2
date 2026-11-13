using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ClimateParticleController))]
public sealed class ClimateParticleControllerEditor : Editor
{
    private const string PreviewName = "Prévia - Bola do deserto";

    public override void OnInspectorGUI()
    {
        if (DrawDefaultInspector()) SyncPreview();
        if (EditorApplication.isPlaying) return;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Arraste os pontos dos caminhos na Scene View. A bola escolhe um dos caminhos e percorre do início ao fim.", MessageType.Info);
        Transform preview = ((ClimateParticleController)target).transform.Find(PreviewName);
        if (GUILayout.Button(preview == null ? "Mostrar bola na cena" : "Atualizar prévia da bola"))
        {
            if (preview == null) CreatePreview();
            else SyncPreview();
        }
        if (preview != null && GUILayout.Button("Remover prévia da cena"))
            Undo.DestroyObjectImmediate(preview.gameObject);
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();
        SerializedProperty routes = serializedObject.FindProperty("tumbleweedRoutes");
        float radius = serializedObject.FindProperty("tumbleweedDiameter").floatValue * 0.5f;
        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            SerializedProperty startProperty = route.FindPropertyRelative("start");
            SerializedProperty endProperty = route.FindPropertyRelative("end");
            Vector3 start = startProperty.vector3Value;
            Vector3 end = endProperty.vector3Value;
            Handles.color = new Color(0.95f, 0.65f, 0.19f, 1f);
            Handles.DrawAAPolyLine(3f, start, end);
            Handles.DrawWireDisc(start, Vector3.up, radius);
            Handles.Label(start + Vector3.up * (radius + 0.15f), "Bola " + (i + 1) + " · início");
            Handles.Label(end + Vector3.up * 0.25f, "Bola " + (i + 1) + " · fim");
            EditorGUI.BeginChangeCheck();
            Vector3 nextStart = Handles.PositionHandle(start, Quaternion.identity);
            Vector3 nextEnd = Handles.PositionHandle(end, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) continue;
            Undo.RecordObject(target, "Mover caminho da bola do deserto");
            startProperty.vector3Value = nextStart;
            endProperty.vector3Value = nextEnd;
            serializedObject.ApplyModifiedProperties();
            SyncPreview();
            break;
        }
    }

    private void CreatePreview()
    {
        var controller = (ClimateParticleController)target;
        GameObject prefab = serializedObject.FindProperty("tumbleweedPrefab").objectReferenceValue as GameObject;
        if (prefab == null) return;
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, controller.gameObject.scene) as GameObject;
        if (instance == null) return;
        Undo.RegisterCreatedObjectUndo(instance, "Mostrar bola do deserto");
        Undo.SetTransformParent(instance.transform, controller.transform, "Mostrar bola do deserto");
        instance.name = PreviewName;
        instance.tag = "EditorOnly";
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>()) collider.enabled = false;
        SyncPreview();
        Selection.activeGameObject = controller.gameObject;
    }

    private void SyncPreview()
    {
        if (EditorApplication.isPlaying) return;
        var controller = (ClimateParticleController)target;
        Transform preview = controller.transform.Find(PreviewName);
        if (preview == null) return;
        serializedObject.Update();
        var routes = serializedObject.FindProperty("tumbleweedRoutes");
        if (routes.arraySize == 0) return;
        Vector3 start = routes.GetArrayElementAtIndex(0).FindPropertyRelative("start").vector3Value;
        GameObject prefab = serializedObject.FindProperty("tumbleweedPrefab").objectReferenceValue as GameObject;
        float diameter = serializedObject.FindProperty("tumbleweedDiameter").floatValue;
        Undo.RecordObject(preview, "Ajustar prévia da bola do deserto");
        preview.position = start;
        preview.rotation = Quaternion.identity;
        preview.localScale = prefab != null ? prefab.transform.localScale : Vector3.one;
        Renderer[] renderers = preview.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        preview.localScale *= diameter / Mathf.Max(0.01f, bounds.size.x, bounds.size.y, bounds.size.z);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        preview.position += Vector3.up * (start.y - bounds.min.y);
        SceneView.RepaintAll();
    }
}
