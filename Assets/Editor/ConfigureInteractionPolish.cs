using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ConfigureInteractionPolish
{
    [MenuItem("Tools/Configure Interaction Polish")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PrototypeLayout.unity", OpenSceneMode.Single);
        var canvas = Object.FindFirstObjectByType<Canvas>();
        var cards = new[]
        {
            GameObject.Find("Card - Coleta de Madeira"),
            GameObject.Find("Card - Pescaria"),
            GameObject.Find("Card - Arar Terreno"),
            GameObject.Find("Card - Comprar Sementes")
        };

        var panel = new GameObject("Painel de atividades", typeof(RectTransform), typeof(CanvasGroup));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(canvas.transform, false);
        SetTopLeft(panelRect, Vector2.zero, new Vector2(610, 124));
        foreach (var card in cards)
            card.transform.SetParent(panel.transform, true);

        var buttonRect = Panel(canvas.transform, "Abrir atividades", new Vector2(548, -10), new Vector2(58, 64),
            new Color(0.87f, 0.86f, 0.72f));
        var button = buttonRect.gameObject.AddComponent<Button>();
        var buttonText = Label(buttonRect, "Texto abrir atividades", "AÇÕES", new Vector2(2, -19),
            new Vector2(54, 27), 11);
        var toggle = buttonRect.gameObject.AddComponent<ActivityPanelToggle>();
        Set(toggle, "cardsPanel", panelRect);
        Set(toggle, "cardsGroup", panel.GetComponent<CanvasGroup>());
        Set(toggle, "buttonText", buttonText);
        UnityEventTools.AddPersistentListener(button.onClick, toggle.Toggle);

        var previewRect = Panel(canvas.transform, "Indicador pequeno de arraste", Vector2.zero,
            new Vector2(92, 42), new Color(0.96f, 0.84f, 0.84f));
        previewRect.anchorMin = previewRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewRect.GetComponent<Image>().raycastTarget = false;
        var group = previewRect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        group.blocksRaycasts = false;
        group.interactable = false;
        var activityText = Label(previewRect, "Nome da atividade", "Coleta de madeira", new Vector2(3, -3),
            new Vector2(86, 36), 10);
        var preview = previewRect.gameObject.AddComponent<DragCursorPreview>();
        Set(preview, "canvas", canvas);
        Set(preview, "previewRect", previewRect);
        Set(preview, "previewGroup", group);
        Set(preview, "activityText", activityText);
        foreach (var card in cards)
            Set(card.GetComponent<ActivityCardDrag>(), "preview", preview);

        AddProgress("Trabalhador ilustrativo A");
        AddProgress("Trabalhador ilustrativo B");

        var actions = Object.FindFirstObjectByType<ActionResolver>();
        SetFloat(actions, "movementDuration", 5f);
        SetFloat(actions, "woodWorkDuration", 8f);
        SetFloat(actions, "fishingWorkDuration", 8f);
        SetFloat(actions, "plowWorkDuration", 8f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Interação configurada: cursor pequeno, painel animado e progresso de trabalho.");
    }

    private static void AddProgress(string workerName)
    {
        var worker = GameObject.Find(workerName);
        var bar = new GameObject("Progresso de trabalho");
        bar.transform.SetParent(worker.transform, false);
        bar.transform.localPosition = new Vector3(0, 1.47f, 0);
        var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
        background.name = "Fundo da barra";
        background.transform.SetParent(bar.transform, false);
        background.transform.localScale = new Vector3(0.92f, 0.12f, 0.045f);
        background.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Prototype/Color-69442CFF.mat");
        background.GetComponent<Collider>().enabled = false;

        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fill.name = "Preenchimento da barra";
        fill.transform.SetParent(bar.transform, false);
        fill.transform.localPosition = new Vector3(0, 0, -0.04f);
        fill.transform.localScale = new Vector3(0.84f, 0.08f, 0.05f);
        fill.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Prototype/Color-EACA34FF.mat");
        fill.GetComponent<Collider>().enabled = false;
        bar.SetActive(false);

        var progress = worker.AddComponent<WorkProgress>();
        Set(progress, "barRoot", bar);
        Set(progress, "fill", fill.transform);
        Set(worker.GetComponent<WorkerAssignment>(), "workProgress", progress);
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetTopLeft(rect, position, size);
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int sizeFont)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        SetTopLeft(rect, position, size);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = sizeFont;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.16f, 0.15f, 0.12f);
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Set(Object component, string field, Object value)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Object component, string field, float value)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
