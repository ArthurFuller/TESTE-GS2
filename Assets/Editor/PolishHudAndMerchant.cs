using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PolishHudAndMerchant
{
    [MenuItem("Tools/Polish HUD and Merchant %&h")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Pare o Play antes de ajustar a cena.");
            return;
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PrototypeLayout.unity", OpenSceneMode.Single);
        var panel = GameObject.Find("Painel de recursos").GetComponent<RectTransform>();
        panel.anchoredPosition = new Vector2(588, -10);
        panel.sizeDelta = new Vector2(300, 132);
        panel.GetComponent<Image>().color = new Color(0.94f, 0.91f, 0.80f, 0.97f);

        Place("Dia: 1 / 12", new Vector2(600, -19), new Vector2(120, 20), 12);
        Place("Reputação", new Vector2(744, -19), new Vector2(136, 20), 11);
        Place("Madeira: —", new Vector2(600, -45), new Vector2(130, 20), 12);
        Place("Comida: 2", new Vector2(744, -45), new Vector2(136, 20), 12);
        Place("Sementes: —", new Vector2(600, -70), new Vector2(130, 39), 12);
        Place("Moedas", new Vector2(744, -70), new Vector2(136, 20), 12);
        Place("Pedido ativo", new Vector2(600, -112), new Vector2(280, 22), 10);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("HUD organizado em linhas sem sobreposição.");
    }

    private static void Place(string name, Vector2 position, Vector2 size, int fontSize)
    {
        var go = GameObject.Find(name);
        if (go == null) throw new System.InvalidOperationException("Elemento do HUD ausente: " + name);
        var rect = go.GetComponent<RectTransform>();
        var label = go.GetComponent<Text>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        label.fontSize = fontSize;
        label.alignment = name == "Sementes: —" ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
    }
}
