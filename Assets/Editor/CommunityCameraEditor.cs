using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(CommunityCamera))]
public sealed class CommunityCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        CommunityCamera controller = (CommunityCamera)target;
        Transform cameraTransform = controller.ViewTransform;

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Os ajustes deste painel serão salvos na cena ao parar o Play.", MessageType.Info);
            if (GUILayout.Button("Usar a vista atual como início"))
            {
                controller.UseCurrentViewAsHome();
                CommunityCameraPlaySave.RecordCurrentView(controller);
            }
        }

        EditorGUILayout.LabelField("Enquadramento inicial", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        Vector3 position = EditorGUILayout.Vector3Field("Posição X / Y / Z", cameraTransform.localPosition);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cameraTransform, "Alterar posição inicial da câmera");
            cameraTransform.localPosition = position;
            if (EditorApplication.isPlaying) controller.UseCurrentViewAsHome();
            CommunityCameraPlaySave.RecordPose(controller);
            Refresh(cameraTransform);
        }

        EditorGUI.BeginChangeCheck();
        Vector3 rotation = EditorGUILayout.Vector3Field("Rotação X / Y / Z", cameraTransform.localEulerAngles);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cameraTransform, "Alterar rotação inicial da câmera");
            cameraTransform.localEulerAngles = rotation;
            if (EditorApplication.isPlaying) controller.UseCurrentViewAsHome();
            CommunityCameraPlaySave.RecordPose(controller);
            Refresh(cameraTransform);
        }

        float groundAngle = -cameraTransform.forward.y;
        float distance = groundAngle > 0.01f ? cameraTransform.position.y / groundAngle : 0f;
        if (groundAngle > 0.01f)
        {
            EditorGUI.BeginChangeCheck();
            float nextDistance = EditorGUILayout.FloatField("Distância inicial", distance);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cameraTransform, "Alterar distância inicial da câmera");
                Vector3 focus = cameraTransform.position + cameraTransform.forward * distance;
                cameraTransform.position = focus - cameraTransform.forward * Mathf.Max(0.1f, nextDistance);
                if (EditorApplication.isPlaying) controller.UseCurrentViewAsHome();
                CommunityCameraPlaySave.RecordPose(controller);
                Refresh(cameraTransform);
                distance = Mathf.Max(0.1f, nextDistance);
            }
        }
        else EditorGUILayout.HelpBox("A câmera precisa apontar para o chão para ajustar a distância.", MessageType.Warning);

        EditorGUI.BeginChangeCheck();
        float fieldOfView = EditorGUILayout.Slider("Campo de visão (FOV)", controller.FieldOfView, 1f, 179f);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(controller.CommunityShot != null ?
                (Object)controller.CommunityShot : controller.GetComponent<Camera>(),
                "Alterar campo de visão da câmera");
            controller.FieldOfView = fieldOfView;
            CommunityCameraPlaySave.RecordFieldOfView(fieldOfView);
            Refresh(cameraTransform);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Limites da visão", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Azul: retângulo máximo visível no chão. Amarelo: visão atual. X e Z param separadamente nas bordas.", MessageType.Info);
        DrawProperty("useCustomViewBounds", "Personalizar retângulo azul");
        SerializedProperty customBounds = serializedObject.FindProperty("useCustomViewBounds");
        if (GUILayout.Button("Copiar enquadramento do zoom out") &&
            controller.GetAutomaticViewBounds(out Vector2 automaticMin, out Vector2 automaticMax))
        {
            customBounds.boolValue = true;
            serializedObject.FindProperty("minimumView").vector2Value = automaticMin;
            serializedObject.FindProperty("maximumView").vector2Value = automaticMax;
            CommunityCameraPlaySave.RecordCustomBounds(true, automaticMin, automaticMax);
            Refresh(cameraTransform);
        }
        if (customBounds.boolValue)
        {
            DrawViewCorner("minimumView", "Borda mínima X / Z");
            DrawViewCorner("maximumView", "Borda máxima X / Z");
            Vector2 minView = serializedObject.FindProperty("minimumView").vector2Value;
            Vector2 maxView = serializedObject.FindProperty("maximumView").vector2Value;
            if (minView.x >= maxView.x || minView.y >= maxView.y)
                EditorGUILayout.HelpBox("A borda mínima precisa ficar antes da máxima em X e Z.", MessageType.Warning);
            else if (controller.GetAutomaticViewBounds(out Vector2 zoomOutMin,
                out Vector2 zoomOutMax) &&
                (minView.x > zoomOutMin.x || minView.y > zoomOutMin.y ||
                 maxView.x < zoomOutMax.x || maxView.y < zoomOutMax.y))
                EditorGUILayout.HelpBox("O enquadramento no zoom out ultrapassa o retângulo personalizado.", MessageType.Warning);
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Limites adicionais da posição", EditorStyles.boldLabel);
        DrawProperty("useAdditionalPositionLimits", "Usar limites extras X / Z");
        bool useAdditionalLimits = serializedObject.FindProperty("useAdditionalPositionLimits").boolValue;
        Transform minimum = useAdditionalLimits ? DrawBoundary("Limite extra mínimo X / Z", "minimumCorner") : null;
        Transform maximum = useAdditionalLimits ? DrawBoundary("Limite extra máximo X / Z", "maximumCorner") : null;
        DrawProperty("closestDistance", "Distância mínima (zoom máximo)");
        DrawProperty("farthestDistance", "Distância máxima (zoom mínimo)");

        float closest = serializedObject.FindProperty("closestDistance").floatValue;
        float farthest = serializedObject.FindProperty("farthestDistance").floatValue;
        if (closest > farthest)
            EditorGUILayout.HelpBox("A distância mínima precisa ser menor que a máxima.", MessageType.Warning);
        else if (groundAngle > 0.01f && (distance < closest || distance > farthest))
            EditorGUILayout.HelpBox("A distância inicial precisa ficar entre os dois limites de zoom.", MessageType.Warning);
        if (minimum != null && maximum != null &&
            (minimum.position.x >= maximum.position.x || minimum.position.z >= maximum.position.z))
            EditorGUILayout.HelpBox("O limite extra mínimo precisa ficar antes do máximo em X e Z.", MessageType.Warning);
        else if (minimum != null && maximum != null &&
            (cameraTransform.position.x < minimum.position.x || cameraTransform.position.x > maximum.position.x ||
             cameraTransform.position.z < minimum.position.z || cameraTransform.position.z > maximum.position.z))
            EditorGUILayout.HelpBox("A posição inicial da câmera está fora dos limites extras X/Z.", MessageType.Warning);
        else if (minimum != null && maximum != null && groundAngle > 0.01f)
        {
            Vector3 farPosition = cameraTransform.position + cameraTransform.forward * (distance - farthest);
            if (farPosition.x < minimum.position.x || farPosition.x > maximum.position.x ||
                farPosition.z < minimum.position.z || farPosition.z > maximum.position.z)
                EditorGUILayout.HelpBox("Os limites extras não incluem a câmera no zoom out. O eixo incompatível será limitado apenas pelo retângulo azul.", MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Velocidades e acompanhamento", EditorStyles.boldLabel);
        DrawProperty("panSpeed", "Movimento WASD");
        DrawProperty("zoomStep", "Zoom por passo do scroll");
        DrawProperty("keyboardZoomSpeed", "Zoom por teclado");
        DrawProperty("farZoomPanRadius", "Expansão além do zoom out");
        DrawProperty("followDistance", "Distância ao trabalhador");
        DrawProperty("smoothTime", "Suavidade do movimento");
        DrawProperty("communityButton", "Botão Comunidade");

        serializedObject.ApplyModifiedProperties();
    }

    private Transform DrawBoundary(string label, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Transform marker = property.objectReferenceValue as Transform;
        if (marker == null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            return null;
        }

        EditorGUI.BeginChangeCheck();
        Vector2 value = EditorGUILayout.Vector2Field(label,
            new Vector2(marker.position.x, marker.position.z));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(marker, "Alterar limite da câmera");
            marker.position = new Vector3(value.x, marker.position.y, value.y);
            CommunityCameraPlaySave.RecordBoundary(propertyName == "minimumCorner", marker.position);
            Refresh(marker);
        }
        return marker;
    }

    private void DrawProperty(string name, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, new GUIContent(label));
        if (EditorGUI.EndChangeCheck())
        {
            if (property.propertyType == SerializedPropertyType.Float)
                CommunityCameraPlaySave.RecordField(name, property.floatValue);
            else if (name == "useAdditionalPositionLimits")
                CommunityCameraPlaySave.RecordAdditionalLimits(property.boolValue);
            else if (name == "useCustomViewBounds")
                CommunityCameraPlaySave.RecordCustomBounds(property.boolValue,
                    serializedObject.FindProperty("minimumView").vector2Value,
                    serializedObject.FindProperty("maximumView").vector2Value);
            Refresh(((CommunityCamera)target).transform);
        }
    }

    private void DrawViewCorner(string name, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(property, new GUIContent(label));
        if (EditorGUI.EndChangeCheck())
        {
            CommunityCameraPlaySave.RecordCustomBounds(true,
                serializedObject.FindProperty("minimumView").vector2Value,
                serializedObject.FindProperty("maximumView").vector2Value);
            Refresh(((CommunityCamera)target).transform);
        }
    }

    private void Refresh(Transform changed)
    {
        if (!EditorApplication.isPlaying)
            EditorSceneManager.MarkSceneDirty(changed.gameObject.scene);
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}
