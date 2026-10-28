using UnityEngine;
using UnityEngine.UI;

public sealed class FoodPreparationPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Text selectionText;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private InventoryTrading trading;
    private int fish;
    private int corn;
    private int pumpkin;

    private void Awake() { Hide(); }
    private void OnEnable() { if (resources != null) resources.Changed += Refresh; }
    private void OnDisable() { if (resources != null) resources.Changed -= Refresh; }

    public void Show()
    {
        fish = corn = pumpkin = 0;
        if (group != null)
        {
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        Refresh();
    }

    public void AddFish() { if (resources != null && fish < resources.Fish) fish++; Refresh(); }
    public void RemoveFish() { fish = Mathf.Max(0, fish - 1); Refresh(); }
    public void AddCorn() { if (resources != null && corn < resources.Corn) corn++; Refresh(); }
    public void RemoveCorn() { corn = Mathf.Max(0, corn - 1); Refresh(); }
    public void AddPumpkin() { if (resources != null && pumpkin < resources.Pumpkin) pumpkin++; Refresh(); }
    public void RemovePumpkin() { pumpkin = Mathf.Max(0, pumpkin - 1); Refresh(); }

    public void Confirm()
    {
        if (actions != null && actions.ConfirmFoodPreparation(fish, corn, pumpkin)) Hide();
    }

    public void Cancel()
    {
        if (actions != null) actions.CancelFoodPreparation();
        Hide();
    }

    private void Hide()
    {
        if (group == null) return;
        group.alpha = 0;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void Refresh()
    {
        if (selectionText == null || resources == null) return;
        selectionText.text = "ESCOLHA OS INGREDIENTES\nPeixe: " + fish + " / " + resources.Fish +
            "\nMilho: " + corn + " / " + resources.Corn +
            "\nAbóbora: " + pumpkin + " / " + resources.Pumpkin +
            "\nComida gerada: " + (trading != null ? trading.EstimateFood(fish, corn, pumpkin) : 0);
    }
}
