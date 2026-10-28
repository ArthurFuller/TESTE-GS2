using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ConfigureRandomMissions
{
    [MenuItem("Tools/Configure Random Missions %&n")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Pare o Play antes de ajustar as missões.");
            return;
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PrototypeLayout.unity", OpenSceneMode.Single);
        var board = Object.FindFirstObjectByType<DeliveryMissionBoard>();
        if (board == null) throw new System.InvalidOperationException("Quadro de pedidos ausente.");
        if (GameObject.Find("Próximo pedido") != null)
        {
            Debug.Log("Navegação dos pedidos já configurada.");
            return;
        }
        var pane = GameObject.Find("Área de pedidos").GetComponent<RectTransform>();
        var details = GameObject.Find("Detalhes do pedido").GetComponent<Text>();
        details.fontSize = 11;
        details.rectTransform.sizeDelta = new Vector2(232, 143);

        var go = new GameObject("Próximo pedido", typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(pane, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(70, -166);
        rect.sizeDelta = new Vector2(115, 29);
        go.GetComponent<Image>().color = new Color(0.79f, 0.82f, 0.68f);
        var labelGo = new GameObject("Texto próximo pedido", typeof(RectTransform), typeof(Text));
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        var label = labelGo.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 11;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.18f, 0.16f, 0.12f);
        label.raycastTarget = false;
        label.text = "PRÓXIMO PEDIDO";
        var button = go.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, board.Next);
        var data = new SerializedObject(board);
        data.FindProperty("nextButton").objectReferenceValue = button;
        data.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Pedidos aleatórios configurados na Hierarquia.");
    }
}
