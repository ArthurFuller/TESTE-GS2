using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CardHandVisualMigration
{
    private const string DoneKey = "Sertao.CardHandVisual.20261102";

    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (!EditorPrefs.GetBool(DoneKey)) EditorApplication.update += WaitForScene;
    }

    private static void WaitForScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/PrototypeLayout.unity")
            return;
        EditorApplication.update -= WaitForScene;
        Apply();
    }

    private static void Apply()
    {
        GameObject handObject = GameObject.Find("Mão de cartas - configurar aqui");
        if (handObject == null) return;
        var hand = handObject.GetComponent<ActivityCardAvailability>();
        var data = new SerializedObject(hand);
        var array = data.FindProperty("cards");
        var original = new List<ActivityCardDrag>();
        for (int i = 0; i < array.arraySize; i++)
        {
            var card = array.GetArrayElementAtIndex(i).objectReferenceValue as ActivityCardDrag;
            if (card != null) original.Add(card);
        }

        ActivityCardDrag corn = original.Find(card => card.Kind == ActivityKind.PlantCorn);
        ActivityCardDrag pumpkin = original.Find(card => card.Kind == ActivityKind.PlantPumpkin);
        if (corn == null || pumpkin == null) return;
        foreach (ActivityCardDrag card in original)
            if (card != corn && card != pumpkin &&
                (card.Kind == ActivityKind.PlantCorn || card.Kind == ActivityKind.PlantPumpkin))
                Object.DestroyImmediate(card.gameObject);

        Sprite paper = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arte/Cartas/9-SlicedBox.png");
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/xilosa_.ttf");
        var ordered = new List<ActivityCardDrag>();
        foreach (ActivityCardDrag card in original)
        {
            if (card == null || card.Kind == ActivityKind.PlantCorn ||
                card.Kind == ActivityKind.PlantPumpkin || card.Kind == ActivityKind.BuySeeds) continue;
            if (card.Kind == ActivityKind.Harvest || card.Kind == ActivityKind.PrepareFood)
                SetTextCard(card, paper, font);
            card.gameObject.SetActive(true);
            card.transform.SetAsLastSibling();
            ordered.Add(card);
        }

        corn.gameObject.name = "Modelo - Plantar milho";
        pumpkin.gameObject.name = "Modelo - Plantar jerimum";
        corn.gameObject.SetActive(false);
        pumpkin.gameObject.SetActive(false);
        ordered.Add(corn);
        ordered.Add(pumpkin);
        foreach (ActivityCardDrag card in original)
            if (card != null && card.Kind == ActivityKind.BuySeeds) ordered.Add(card);
        array.arraySize = ordered.Count;
        for (int i = 0; i < ordered.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = ordered[i];
        data.FindProperty("handWidth").floatValue = 480f;
        data.FindProperty("maxHandCards").intValue = 15;
        data.ApplyModifiedPropertiesWithoutUndo();

        RectTransform tooltip = data.FindProperty("descriptionPanel").objectReferenceValue as RectTransform;
        if (tooltip != null)
        {
            CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
            if (group == null) group = tooltip.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorPrefs.SetBool(DoneKey, true);
        Debug.Log("CARD_HAND_VISUAL_READY: 8 modelos na lista, 28 cópias removidas, largura 480.");
    }

    private static void SetTextCard(ActivityCardDrag card, Sprite paper, Font font)
    {
        var image = card.GetComponent<Image>();
        image.sprite = paper;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        var rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(140, 140);
        Text label = card.GetComponentInChildren<Text>(true);
        if (label == null) return;
        label.gameObject.SetActive(true);
        label.text = card.Kind == ActivityKind.Harvest ? "Colher" : "Preparar\ncomida";
        label.font = font;
        label.fontSize = card.Kind == ActivityKind.Harvest ? 24 : 19;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.72f, 0.08f, 0.06f, 1f);
        label.raycastTarget = false;
        var textRect = label.GetComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0, -8);
        textRect.sizeDelta = new Vector2(100, 64);
    }
}
