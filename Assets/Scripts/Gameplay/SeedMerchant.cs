using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class SeedMerchant : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private SeedCatalog catalog;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private GameObject stallVisual;
    [SerializeField] private GameObject closedStallLock;
    [SerializeField] private CanvasGroup shopGroup;
    [SerializeField] private Text shopStatusText;
    [SerializeField] private DragCursorPreview dragPreview;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
    [SerializeField] private ShopTabs shopTabs;
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField] private HouseSite houseSite;
    [SerializeField] private HouseSite secondHouseSite;
    [SerializeField, Min(0)] private int fenceWoodCost = 8;
    [SerializeField, Min(0)] private int fenceCoinCost = 8;
    [SerializeField, Min(0)] private int houseWoodCost = 16;
    [SerializeField, Min(0)] private int houseCoinCost = 16;
    [SerializeField] private int[] marketDays = { 1, 4, 7, 10 };
    [SerializeField, Min(1)] private int foodCoinCost = 1;
    [SerializeField, Min(0)] private float panelFadeDuration = 0.25f;

    public event Action AvailabilityChanged;
    public bool IsAvailable { get; private set; }
    public bool IsOpen => shopGroup != null && shopGroup.interactable;
    public int FenceWoodCost => fenceWoodCost;
    public int FenceCoinCost => fenceCoinCost;
    public int FenceSlotsAvailable => hand != null ? hand.FreeSlots : 0;
    public int HouseWoodCost => houseWoodCost;
    public int HouseCoinCost => houseCoinCost;
    public int HouseSlotsAvailable => hand == null ? 0 : Mathf.Min(hand.FreeSlots,
        Mathf.Max(0, (houseSite != null ? 1 : 0) + (secondHouseSite != null ? 1 : 0) - housesPurchased),
        (houseSite != null && !houseSite.IsBuilt ? 1 : 0) +
        (secondHouseSite != null && !secondHouseSite.IsBuilt ? 1 : 0));
    private int housesPurchased;

    private void Awake() => HideShop();
    public void ToggleShop()
    {
        if (!IsAvailable || dayCycle == null || !dayCycle.CanAcceptActions)
        {
            if (feedback != null) feedback.ShowMessage("A barraca está fechada hoje.");
            return;
        }
        if (IsOpen) HideShop();
        else ShowShop();
    }

    public void SetDay(int day, bool hasDeliveryDue = false)
    {
        bool scheduled = Array.IndexOf(marketDays, day) >= 0;
        IsAvailable = hasDeliveryDue || scheduled;
        if (stallVisual != null) stallVisual.SetActive(IsAvailable);
        if (closedStallLock != null) closedStallLock.SetActive(!IsAvailable);
        HideShop();
        AvailabilityChanged?.Invoke();
    }

    public int GetUnitPrice(SaleItem item)
    {
        if (catalog == null) return 0;
        return item switch
        {
            SaleItem.CornSeeds => catalog.Get(CropType.Corn).coinCost,
            SaleItem.PumpkinSeeds => catalog.Get(CropType.Pumpkin).coinCost,
            SaleItem.JerimumSeeds => catalog.Get(CropType.Jerimum).coinCost,
            SaleItem.Food => foodCoinCost,
            _ => 0
        };
    }

    public bool TryBuyCart(int cornSeeds, int pumpkinSeeds, int jerimumSeeds, int food, int fences,
        int houses = 0)
    {
        if (!IsAvailable || !IsOpen || dayCycle == null || !dayCycle.CanAcceptActions || resources == null ||
            catalog == null ||
            cornSeeds != 0 || pumpkinSeeds != 0 || jerimumSeeds != 0 || food < 0 || fences < 0 ||
            houses < 0 || houses > HouseSlotsAvailable ||
            fences + houses > FenceSlotsAvailable ||
            resources.Wood < fences * fenceWoodCost + houses * houseWoodCost) return false;
        long total = (long)food * GetUnitPrice(SaleItem.Food) + (long)fences * fenceCoinCost +
            (long)houses * houseCoinCost;
        if (total <= 0 || total > int.MaxValue || !resources.TrySpendCoins((int)total))
        {
            if (feedback != null) feedback.ShowMessage("Moedas insuficientes para a compra.");
            return false;
        }
        if (food > 0)
        {
            resources.AddFood(food);
            if (resourceHud != null && shopGroup != null)
                resourceHud.FlyFromRect(ResourceKind.Food, food,
                    shopGroup.transform as RectTransform);
        }
        if (fences > 0)
        {
            resources.TrySpendWood(fences * fenceWoodCost);
            for (int i = 0; i < fences; i++) hand.AddCard(ActivityKind.BuildFence);
        }
        if (houses > 0)
        {
            resources.TrySpendWood(houses * houseWoodCost);
            for (int i = 0; i < houses; i++) hand.AddCard(ActivityKind.BuildHouse);
            housesPurchased += houses;
        }
        if (feedback != null) feedback.ShowMessage("Compra concluída: -" + total + " moedas.");
        return true;
    }

    public void ShowShop()
    {
        if (!IsAvailable || shopGroup == null) return;
        if (dragPreview != null) dragPreview.CancelCurrentDrag();
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (shopTabs != null) shopTabs.ShowDefault(true);
        shopGroup.transform.SetAsLastSibling();
        shopGroup.DOKill();
        shopGroup.blocksRaycasts = true;
        shopGroup.interactable = true;
        shopGroup.DOFade(1, panelFadeDuration);
        if (shopStatusText != null) shopStatusText.text = "Role a lista para ver todos os itens.";
    }

    public void HideShop()
    {
        if (shopGroup == null) return;
        shopGroup.DOKill();
        shopGroup.blocksRaycasts = false;
        shopGroup.interactable = false;
        shopGroup.DOFade(0, panelFadeDuration);
    }
}
