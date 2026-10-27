using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ConfigureAgriculture
{
    private const string ScenePath = "Assets/Scenes/PrototypeLayout.unity";

    [MenuItem("Tools/Configure Agriculture")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Pare o Play antes de configurar a cena.");
            return;
        }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (GameObject.Find("Barraca de sementes") != null)
        {
            Debug.Log("Agricultura já configurada.");
            return;
        }

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        ActionResolver actions = UnityEngine.Object.FindFirstObjectByType<ActionResolver>();
        DayCycle day = UnityEngine.Object.FindFirstObjectByType<DayCycle>();
        ResourceBank resources = UnityEngine.Object.FindFirstObjectByType<ResourceBank>();
        ResourceHud hud = UnityEngine.Object.FindFirstObjectByType<ResourceHud>();
        GameFeedback feedback = UnityEngine.Object.FindFirstObjectByType<GameFeedback>();
        Camera camera = Camera.main;
        Plot[] plots = UnityEngine.Object.FindObjectsByType<Plot>(FindObjectsSortMode.None);
        if (canvas == null || actions == null || day == null || resources == null || plots.Length == 0)
            throw new Exception("Cena base incompleta.");

        SeedCatalog catalog = new GameObject("Catálogo de culturas").AddComponent<SeedCatalog>();
        catalog.transform.SetParent(actions.transform.parent, false);
        Set(actions, "seedCatalog", catalog);
        Set(day, "seedCatalog", catalog);
        SetArray(day, "plots", plots);

        var stall = new GameObject("Barraca de sementes");
        stall.transform.SetParent(GameObject.Find("Mapa 3D - blocagem").transform, false);
        Transform forest = GameObject.Find("Região - floresta").transform;
        stall.transform.position = forest.position + new Vector3(2.4f, 0, 0.5f);
        var stallVisual = new GameObject("Visual da barraca");
        stallVisual.transform.SetParent(stall.transform, false);
        Cube(stallVisual.transform, "Balcão", new Vector3(0, 0.35f, 0), new Vector3(1.15f, 0.7f, 0.7f), "Color-76512FFF");
        Cube(stallVisual.transform, "Toldo", new Vector3(0, 1.45f, 0), new Vector3(1.4f, 0.2f, 1.0f), "Color-EACA34FF");
        Cube(stallVisual.transform, "Poste esquerdo", new Vector3(-0.58f, 0.9f, -0.3f), new Vector3(0.1f, 1.1f, 0.1f), "Color-69442CFF");
        Cube(stallVisual.transform, "Poste direito", new Vector3(0.58f, 0.9f, -0.3f), new Vector3(0.1f, 1.1f, 0.1f), "Color-69442CFF");
        var destination = new GameObject("Destino da visita").transform;
        destination.SetParent(stall.transform, false);
        destination.localPosition = new Vector3(0, 0, -1.1f);
        var merchant = stall.AddComponent<SeedMerchant>();
        Set(merchant, "resources", resources);
        Set(merchant, "catalog", catalog);
        Set(merchant, "actions", actions);
        Set(merchant, "dayCycle", day);
        Set(merchant, "feedback", feedback);
        Set(merchant, "stallVisual", stallVisual);
        Set(merchant, "stallDestination", destination);
        Set(actions, "merchant", merchant);
        Set(day, "merchant", merchant);

        var shop = Panel(canvas.transform, "Painel da barraca", new Vector2(310, -100), new Vector2(350, 250), new Color(0.91f, 0.84f, 0.64f));
        var shopGroup = shop.gameObject.AddComponent<CanvasGroup>();
        shopGroup.alpha = 0;
        shopGroup.interactable = false;
        shopGroup.blocksRaycasts = false;
        Label(shop, "Título da barraca", "BARRACA DE SEMENTES", new Vector2(12, -8), new Vector2(326, 34), 18);
        Button cornButton = MakeButton(shop, "Comprar milho", "MILHO: 1 madeira → 2 sementes", new Vector2(20, -52), new Vector2(310, 48), new Color(0.78f, 0.87f, 0.36f));
        Button pumpkinButton = MakeButton(shop, "Comprar abóbora", "ABÓBORA: 2 madeiras → 1 semente", new Vector2(20, -110), new Vector2(310, 48), new Color(0.97f, 0.68f, 0.35f));
        Text shopStatus = Label(shop, "Mensagem da barraca", "Escolha as sementes.", new Vector2(20, -168), new Vector2(310, 27), 13);
        Button closeShop = MakeButton(shop, "Fechar barraca", "FECHAR", new Vector2(120, -204), new Vector2(110, 34), new Color(0.76f, 0.76f, 0.72f));
        Set(merchant, "shopGroup", shopGroup);
        Set(merchant, "shopStatusText", shopStatus);
        UnityEventTools.AddPersistentListener(cornButton.onClick, merchant.BuyCorn);
        UnityEventTools.AddPersistentListener(pumpkinButton.onClick, merchant.BuyPumpkin);
        UnityEventTools.AddPersistentListener(closeShop.onClick, merchant.HideShop);

        var harvest = Panel(canvas.transform, "Escolha da colheita", new Vector2(330, -135), new Vector2(310, 190), new Color(0.90f, 0.86f, 0.65f));
        var harvestGroup = harvest.gameObject.AddComponent<CanvasGroup>();
        harvestGroup.alpha = 0;
        harvestGroup.interactable = false;
        harvestGroup.blocksRaycasts = false;
        Text harvestTitle = Label(harvest, "Título da colheita", "COLHEITA", new Vector2(10, -12), new Vector2(290, 38), 18);
        Button foodButton = MakeButton(harvest, "Colher como comida", "+4 COMIDAS", new Vector2(20, -60), new Vector2(270, 45), new Color(0.76f, 0.87f, 0.37f));
        Button scoreButton = MakeButton(harvest, "Colher como pontos", "+100 PONTOS", new Vector2(20, -119), new Vector2(270, 45), new Color(0.98f, 0.74f, 0.35f));
        var harvestChoice = harvest.gameObject.AddComponent<HarvestChoiceView>();
        Set(harvestChoice, "actions", actions);
        Set(harvestChoice, "choiceGroup", harvestGroup);
        Set(harvestChoice, "titleText", harvestTitle);
        Set(harvestChoice, "foodButtonText", foodButton.GetComponentInChildren<Text>());
        Set(harvestChoice, "scoreButtonText", scoreButton.GetComponentInChildren<Text>());
        Set(actions, "harvestChoice", harvestChoice);
        UnityEventTools.AddPersistentListener(foodButton.onClick, harvestChoice.ChooseFood);
        UnityEventTools.AddPersistentListener(scoreButton.onClick, harvestChoice.ChooseScore);

        Text seedText = GameObject.Find("Sementes: —").GetComponent<Text>();
        Set(hud, "seedText", seedText);
        seedText.fontSize = 12;

        foreach (Plot plot in plots)
        {
            var corn = new GameObject("Milho 3D");
            corn.transform.SetParent(plot.transform, false);
            corn.transform.localPosition = new Vector3(0, 0.12f, 0);
            Cube(corn.transform, "Caule", new Vector3(0, 0.31f, 0), new Vector3(0.11f, 0.62f, 0.11f), "Color-356F39FF").GetComponent<Collider>().enabled = false;
            Cube(corn.transform, "Espiga", new Vector3(0.12f, 0.48f, 0), new Vector3(0.19f, 0.34f, 0.18f), "Color-EACA34FF").GetComponent<Collider>().enabled = false;
            corn.SetActive(false);
            Set(plot, "cornVisual", corn);

            var pumpkin = new GameObject("Abóbora 3D");
            pumpkin.transform.SetParent(plot.transform, false);
            pumpkin.transform.localPosition = new Vector3(0, 0.12f, 0);
            var fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fruit.name = "Fruto";
            fruit.transform.SetParent(pumpkin.transform, false);
            fruit.transform.localPosition = new Vector3(0, 0.18f, 0);
            fruit.transform.localScale = new Vector3(0.52f, 0.36f, 0.5f);
            fruit.GetComponent<Renderer>().sharedMaterial = Mat("Color-97563CFF");
            fruit.GetComponent<Collider>().enabled = false;
            Cube(pumpkin.transform, "Ramo", new Vector3(0, 0.42f, 0), new Vector3(0.09f, 0.18f, 0.09f), "Color-356F39FF").GetComponent<Collider>().enabled = false;
            pumpkin.SetActive(false);
            Set(plot, "pumpkinVisual", pumpkin);
        }

        var panel = GameObject.Find("Painel de atividades").GetComponent<RectTransform>();
        panel.sizeDelta = new Vector2(540, 226);
        string[] oldNames = { "Card - Coleta de Madeira", "Card - Pescaria", "Card - Arar Terreno", "Card - Comprar Sementes" };
        GameObject[] cards = new GameObject[7];
        for (int i = 0; i < 4; i++) cards[i] = GameObject.Find(oldNames[i]);
        GameObject template = cards[2];
        cards[4] = UnityEngine.Object.Instantiate(template, panel);
        cards[5] = UnityEngine.Object.Instantiate(template, panel);
        cards[6] = UnityEngine.Object.Instantiate(template, panel);
        ConfigureCard(cards[3], "Card - Visitar barraca", ActivityKind.BuySeeds, "Visitar barraca", "Compra sem gastar ação", camera);
        ConfigureCard(cards[4], "Card - Plantar Milho", ActivityKind.PlantCorn, "Plantar Milho", "1 semente · 1 ação", camera);
        ConfigureCard(cards[5], "Card - Plantar Abóbora", ActivityKind.PlantPumpkin, "Plantar Abóbora", "1 semente · 1 ação", camera);
        ConfigureCard(cards[6], "Card - Colher", ActivityKind.Harvest, "Colher", "Comida ou pontos", camera);
        for (int i = 0; i < cards.Length; i++)
        {
            var rect = cards[i].GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(8 + (i % 4) * 132, -8 - (i / 4) * 110);
            rect.sizeDelta = new Vector2(120, 100);
            rect.localScale = Vector3.one;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Agricultura configurada: 7 cards, 18 canteiros, barraca, escolhas e HUD.");
    }

    private static void ConfigureCard(GameObject card, string name, ActivityKind kind, string title, string subtitle, Camera camera)
    {
        card.name = name;
        var drag = card.GetComponent<ActivityCardDrag>();
        SetEnum(drag, "activity", (int)kind);
        SetString(drag, "activityName", title);
        Set(drag, "worldCamera", camera);
        Text[] texts = card.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            if (text.name == "Arar Terreno" || text.name == "Comprar Sementes") text.text = title;
            else text.text = subtitle;
            if (text.name != "Arar Terreno" && text.name != "Comprar Sementes") text.fontSize = 10;
        }
    }

    private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, string material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = Mat(material);
        return cube;
    }

    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prototype/" + name + ".mat");

    private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rect;
    }

    private static Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var rect = Panel(parent, name, position, size, Color.clear);
        UnityEngine.Object.DestroyImmediate(rect.GetComponent<Image>());
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = new Color(0.18f, 0.15f, 0.12f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static Button MakeButton(Transform parent, string name, string value, Vector2 position, Vector2 size, Color color)
    {
        var rect = Panel(parent, name, position, size, color);
        var button = rect.gameObject.AddComponent<Button>();
        Label(rect, "Texto", value, Vector2.zero, size, 14);
        return button;
    }

    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(name).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetString(UnityEngine.Object target, string name, string value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(name).stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetEnum(UnityEngine.Object target, string name, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(name).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray(UnityEngine.Object target, string name, Plot[] values)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
