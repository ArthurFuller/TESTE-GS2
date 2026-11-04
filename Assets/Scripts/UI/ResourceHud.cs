using UnityEngine;
using UnityEngine.UI;

public sealed class ResourceHud : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text woodText;
    [SerializeField] private Text fishText;
    [SerializeField] private Text cassavaText;
    [SerializeField] private Text pumpkinText;
    [SerializeField] private Text cornText;
    [SerializeField] private Text foodText;
    [SerializeField] private Text coinText;
    [SerializeField] private Text animalText;

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
        if (woodText != null) woodText.text = resources.Wood.ToString();
        if (fishText != null) fishText.text = resources.Fish.ToString();
        if (cassavaText != null) cassavaText.text = resources.Jerimum.ToString();
        if (pumpkinText != null) pumpkinText.text = resources.Pumpkin.ToString();
        if (cornText != null) cornText.text = resources.Corn.ToString();
        if (foodText != null) foodText.text = "Comida: " + resources.Food;
        if (coinText != null) coinText.text = "Dinheiro: " + resources.Coins;
        if (animalText != null) animalText.text = "Leite: " + resources.Milk +
            "     Ovos: " + resources.Eggs;
    }
}
