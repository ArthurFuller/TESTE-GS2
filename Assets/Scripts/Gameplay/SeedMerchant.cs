using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SeedMerchant : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedCatalog catalog;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private GameObject stallVisual;
    [SerializeField] private GameObject closedStallLock;
    [SerializeField] private Transform stallDestination;
    [SerializeField] private CanvasGroup shopGroup;
    [SerializeField] private Text shopStatusText;
    [SerializeField] private Button cornButton;
    [SerializeField] private Button pumpkinButton;
    [SerializeField] private Button foodButton;
    [SerializeField] private DragCursorPreview dragPreview;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
    [SerializeField] private ShopTabs shopTabs;
    [SerializeField] private int[] marketDays = { 1, 4, 7, 10 };
    [SerializeField, Min(1)] private int foodCoinCost = 1;
    [SerializeField, Min(1)] private int foodPerPurchase = 2;
    [SerializeField, Min(0)] private float panelFadeDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float arrivalRadius = 0.55f;
    [SerializeField, Range(0f, 1f)] private float lowReputationVisitChance = 0.6f;
    [SerializeField] private int lowReputationThreshold = 0;
    [SerializeField] private int highReputationThreshold = 300;
    [SerializeField, Min(1)] private int lowReputationStock = 2;
    [SerializeField, Min(1)] private int regularStock = 3;
    [SerializeField, Min(1)] private int highReputationStock = 5;

    private int stockRemaining;

    public bool IsAvailable { get; private set; }
    public bool IsOpen => shopGroup != null && shopGroup.interactable;

    private void Awake()
    {
        HideShop();
    }

    private void OnEnable()
    {
        if (resources != null) resources.Changed += RefreshStockDisplay;
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= RefreshStockDisplay;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        RequestVisit(WorkerAssignment.Selected);
    }

    public void SetDay(int day)
    {
        bool scheduled = System.Array.IndexOf(marketDays, day) >= 0;
        IsAvailable = scheduled && resources != null && (resources.Reputation >= lowReputationThreshold ||
            day == 1 || day == 10 || UnityEngine.Random.value < lowReputationVisitChance);
        stockRemaining = IsAvailable ? resources.Reputation < lowReputationThreshold ? lowReputationStock :
            resources.Reputation >= highReputationThreshold ? highReputationStock : regularStock : 0;
        if (stallVisual != null) stallVisual.SetActive(IsAvailable);
        if (closedStallLock != null) closedStallLock.SetActive(!IsAvailable);
        RefreshStockDisplay();
        HideShop();
    }

    public void RequestVisit(WorkerAssignment worker)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (worker == null)
        {
            feedback.ShowMessage("Selecione um trabalhador para visitar os pedidos.");
            return;
        }
        if (worker.IsUnavailableToday)
        {
            feedback.ShowMessage("Esse trabalhador está ausente hoje.");
            return;
        }
        if (actions == null || stallDestination == null)
        {
            feedback.ShowMessage("Barraca não configurada.");
            return;
        }
        if (actions.IsWorkerStoppedNear(worker, stallDestination.position, arrivalRadius))
        {
            ShowShop();
            return;
        }
        if (actions.IsBusy)
        {
            feedback.ShowMessage("Aguarde a ação em andamento.");
            return;
        }
        actions.TravelToMerchant(worker, stallDestination.position, ShowShop);
    }

    public void BuyCorn() => Buy(CropType.Corn);
    public void BuyPumpkin() => Buy(CropType.Pumpkin);

    public void BuyFood()
    {
        if (!CanBuy()) return;
        if (!resources.TrySpendCoins(Price(foodCoinCost)))
        {
            if (shopStatusText != null) shopStatusText.text = "Moedas insuficientes.";
            return;
        }
        resources.AddFood(foodPerPurchase);
        stockRemaining--;
        RefreshStockDisplay();
        if (shopStatusText != null) shopStatusText.text = "+" + foodPerPurchase + " comida";
        feedback.ShowMessage("Comida comprada.");
    }

    private void Buy(CropType crop)
    {
        if (!CanBuy() || catalog == null) return;
        CropSettings settings = catalog.Get(crop);
        if (settings == null || !resources.TrySpendCoins(Price(settings.coinCost)))
        {
            if (shopStatusText != null) shopStatusText.text = "Moedas insuficientes.";
            feedback.ShowMessage("Moedas insuficientes para comprar sementes.");
            return;
        }
        resources.AddSeeds(crop, settings.seedsPerPurchase);
        stockRemaining--;
        RefreshStockDisplay();
        if (shopStatusText != null)
            shopStatusText.text = "+" + settings.seedsPerPurchase + " sementes de " + settings.displayName;
        feedback.ShowMessage("Sementes de " + settings.displayName + " compradas.");
    }

    private int Price(int basePrice) => Mathf.Max(1, basePrice +
        (resources.Reputation < lowReputationThreshold ? 1 :
            resources.Reputation >= highReputationThreshold ? -1 : 0));

    private void RefreshStockDisplay()
    {
        bool canBuy = IsAvailable && stockRemaining > 0;
        if (cornButton != null) cornButton.interactable = canBuy;
        if (pumpkinButton != null) pumpkinButton.interactable = canBuy;
        if (foodButton != null) foodButton.interactable = canBuy;
        if (catalog == null || resources == null) return;
        if (cornButton != null) cornButton.GetComponentInChildren<Text>().text =
            catalog.Get(CropType.Corn).seedsPerPurchase + " sementes de milho • " +
            Price(catalog.Get(CropType.Corn).coinCost) + " moedas";
        if (pumpkinButton != null) pumpkinButton.GetComponentInChildren<Text>().text =
            catalog.Get(CropType.Pumpkin).seedsPerPurchase + " semente(s) de abóbora • " +
            Price(catalog.Get(CropType.Pumpkin).coinCost) + " moedas";
        if (foodButton != null) foodButton.GetComponentInChildren<Text>().text =
            foodPerPurchase + " comidas • " + Price(foodCoinCost) + " moeda(s)";
    }

    private bool CanBuy() => IsAvailable && stockRemaining > 0 && resources != null && shopGroup != null &&
        shopGroup.interactable && dayCycle != null && dayCycle.CanAcceptActions;

    public void ShowShop()
    {
        if (dragPreview != null) dragPreview.CancelCurrentDrag();
        if (shopGroup == null)
            return;
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (shopTabs != null) shopTabs.ShowDefault(IsAvailable);
        shopGroup.transform.SetAsLastSibling();
        shopGroup.DOKill();
        shopGroup.blocksRaycasts = true;
        shopGroup.interactable = true;
        shopGroup.DOFade(1, panelFadeDuration);
        if (shopStatusText != null)
            shopStatusText.text = IsAvailable ? "Estoque da feira: " + stockRemaining + " compra(s)." :
                "Mercadorias fechadas; entregas disponíveis.";
    }

    public void HideShop()
    {
        if (shopGroup == null)
            return;
        shopGroup.DOKill();
        shopGroup.blocksRaycasts = false;
        shopGroup.interactable = false;
        shopGroup.DOFade(0, panelFadeDuration);
    }
}
