using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceHud : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text woodText;
    [SerializeField] private Text foodText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text coinText;
    [SerializeField] private Text seedText;

    private void OnEnable()
    {
        if (resources != null)
            resources.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (resources != null)
            resources.Changed -= Refresh;
    }

    private void Start()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (resources == null)
            return;
        if (woodText != null) woodText.text = "Madeira: " + resources.Wood;
        if (foodText != null) foodText.text = "Comida: " + resources.Food;
        if (scoreText != null) scoreText.text = "Reputação: " + resources.Reputation;
        if (coinText != null) coinText.text = "Moedas: " + resources.Coins;
        if (seedText != null) seedText.text = "Milho: " + resources.CornSeeds + "\nAbóbora: " + resources.PumpkinSeeds;
    }
}
