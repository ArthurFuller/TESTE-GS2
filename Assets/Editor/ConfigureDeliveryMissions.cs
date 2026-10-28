using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ConfigureDeliveryMissions
{
    [MenuItem("Tools/Configure Delivery Missions %&m")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Pare o Play antes de configurar as missões.");
            return;
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PrototypeLayout.unity", OpenSceneMode.Single);
        if (Object.FindFirstObjectByType<DeliveryMissionBoard>() != null)
        {
            Debug.Log("As missões já estão configuradas nesta cena.");
            return;
        }

        var canvas = Object.FindFirstObjectByType<Canvas>();
        var panel = GameObject.Find("Painel da barraca").GetComponent<RectTransform>();
        panel.anchoredPosition = new Vector2(145, -82);
        panel.sizeDelta = new Vector2(620, 320);

        var title = GameObject.Find("Título da barraca").GetComponent<Text>();
        title.text = "BARRACA E PEDIDOS";
        title.rectTransform.sizeDelta = new Vector2(600, 34);

        var corn = GameObject.Find("Comprar milho").GetComponent<Button>();
        var pumpkin = GameObject.Find("Comprar abóbora").GetComponent<Button>();
        corn.GetComponentInChildren<Text>().text = "2 sementes de milho  •  2 moedas";
        pumpkin.GetComponentInChildren<Text>().text = "1 semente de abóbora  •  3 moedas";
        SetNamedText("2x Comidas", "4x Comidas");
        SetNamedText("2x Sementes de Milho", "2 moedas");
        SetNamedText("Colher como pontos", "Colher como reputação");
        foreach (var label in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
        {
            if (label.text == "Comida ou pontos") label.text = "Comida ou reputação";
            if (label.text == "MILHO: 1 madeira → 2 sementes") label.text = "MILHO: 2 moedas → 2 sementes";
        }

        var merchant = Object.FindFirstObjectByType<SeedMerchant>();
        var food = MakeButton(panel, "Comprar comida", "2 comidas  •  1 moeda",
            new Vector2(20, -168), new Vector2(310, 42), new Color(0.86f, 0.76f, 0.51f));
        UnityEventTools.AddPersistentListener(food.onClick, merchant.BuyFood);
        Set(merchant, "cornButton", corn);
        Set(merchant, "pumpkinButton", pumpkin);
        Set(merchant, "foodButton", food);

        var status = GameObject.Find("Mensagem da barraca").GetComponent<RectTransform>();
        status.anchoredPosition = new Vector2(20, -218);
        status.sizeDelta = new Vector2(310, 30);
        var close = GameObject.Find("Fechar barraca").GetComponent<RectTransform>();
        close.anchoredPosition = new Vector2(250, -267);

        var orderBox = MakePanel(panel, "Área de pedidos", new Vector2(348, -45),
            new Vector2(255, 257), new Color(0.97f, 0.92f, 0.79f));
        MakeText(orderBox, "Título dos pedidos", "ENCOMENDAS", new Vector2(10, -8),
            new Vector2(235, 28), 15, TextAnchor.MiddleCenter);
        var orderText = MakeText(orderBox, "Detalhes do pedido", "Novo pedido", new Vector2(12, -43),
            new Vector2(232, 145), 12, TextAnchor.UpperLeft);
        var accept = MakeButton(orderBox, "Aceitar pedido", "ACEITAR", new Vector2(10, -204),
            new Vector2(112, 38), new Color(0.68f, 0.81f, 0.55f));
        var deliver = MakeButton(orderBox, "Entregar pedido", "ENTREGAR", new Vector2(132, -204),
            new Vector2(112, 38), new Color(0.88f, 0.72f, 0.43f));

        var missionBoard = GameObject.Find("Barraca de sementes").AddComponent<DeliveryMissionBoard>();
        Set(missionBoard, "resources", Object.FindFirstObjectByType<ResourceBank>());
        Set(missionBoard, "feedback", Object.FindFirstObjectByType<GameFeedback>());
        Set(missionBoard, "orderText", orderText);
        Set(missionBoard, "acceptButton", accept);
        Set(missionBoard, "deliverButton", deliver);
        UnityEventTools.AddPersistentListener(accept.onClick, missionBoard.Accept);
        UnityEventTools.AddPersistentListener(deliver.onClick, missionBoard.Deliver);

        var score = GameObject.Find("Score: —").GetComponent<Text>();
        score.gameObject.name = "Reputação";
        score.text = "Reputação: 0";
        score.rectTransform.anchoredPosition = new Vector2(752, -21);
        score.rectTransform.sizeDelta = new Vector2(138, 18);
        var coins = MakeText(canvas.transform, "Moedas", "Moedas: 2", new Vector2(752, -42),
            new Vector2(138, 18), 11, TextAnchor.MiddleLeft);
        var orderHud = MakeText(canvas.transform, "Pedido ativo", "Novo pedido na barraca",
            new Vector2(606, -63), new Vector2(284, 18), 11, TextAnchor.MiddleRight);
        Set(missionBoard, "hudText", orderHud);
        Set(Object.FindFirstObjectByType<ResourceHud>(), "coinText", coins);
        Set(Object.FindFirstObjectByType<DayCycle>(), "missionBoard", missionBoard);

        var resources = Object.FindFirstObjectByType<ResourceBank>();
        SetInt(resources, "startingCoins", 2);
        var resolver = Object.FindFirstObjectByType<ActionResolver>();
        SetInt(resolver, "fishingReward", 4);
        var catalog = Object.FindFirstObjectByType<SeedCatalog>();
        var catalogData = new SerializedObject(catalog);
        catalogData.FindProperty("corn").FindPropertyRelative("coinCost").intValue = 2;
        catalogData.FindProperty("pumpkin").FindPropertyRelative("coinCost").intValue = 3;
        catalogData.ApplyModifiedPropertiesWithoutUndo();

        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.name = "Quadro permanente de pedidos";
        sign.transform.SetParent(merchant.transform, false);
        sign.transform.localPosition = new Vector3(-1.55f, 0.68f, 0);
        sign.transform.localScale = new Vector3(0.82f, 1.12f, 0.14f);
        sign.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Prototype/Color-69442CFF.mat");
        var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "Papel dos pedidos";
        paper.transform.SetParent(sign.transform, false);
        paper.transform.localPosition = new Vector3(0, 0, -0.55f);
        paper.transform.localScale = new Vector3(0.82f, 0.79f, 0.1f);
        paper.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Prototype/Color-EACA34FF.mat");
        paper.GetComponent<Collider>().enabled = false;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Missões configuradas na Hierarquia, loja expandida e alimentação ajustada.");
    }

    private static RectTransform MakePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, position, size);
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static Text MakeText(Transform parent, string name, string value, Vector2 position,
        Vector2 size, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Place(rect, position, size);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.18f, 0.16f, 0.12f);
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static Button MakeButton(Transform parent, string name, string value, Vector2 position,
        Vector2 size, Color color)
    {
        var rect = MakePanel(parent, name, position, size, color);
        var button = rect.gameObject.AddComponent<Button>();
        MakeText(rect, "Texto " + name, value, Vector2.zero, size, 12, TextAnchor.MiddleCenter);
        return button;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Set(Object component, string field, Object value)
    {
        var data = new SerializedObject(component);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object component, string field, int value)
    {
        var data = new SerializedObject(component);
        data.FindProperty(field).intValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetNamedText(string name, string value)
    {
        var go = GameObject.Find(name);
        if (go != null && go.TryGetComponent<Text>(out var label)) label.text = value;
    }
}
