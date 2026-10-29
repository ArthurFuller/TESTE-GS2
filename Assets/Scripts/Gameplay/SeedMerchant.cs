using System;
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
    [SerializeField] private DragCursorPreview dragPreview;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
    [SerializeField] private ShopTabs shopTabs;
    [SerializeField] private int[] marketDays = { 1, 4, 7, 10 };
    [SerializeField, Min(1)] private int foodCoinCost = 1;
    [SerializeField, Min(0)] private float panelFadeDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float arrivalRadius = 0.55f;
    [SerializeField, Range(0f, 1f)] private float lowReputationVisitChance = 0.6f;
    [SerializeField] private int lowReputationThreshold = 0;
    [SerializeField] private int highReputationThreshold = 300;

    public event Action AvailabilityChanged;
    public bool IsAvailable { get; private set; }
    public bool IsOpen => shopGroup != null && shopGroup.interactable;

    private void Awake() => HideShop();
    public void OnPointerClick(PointerEventData eventData) => RequestVisit(WorkerAssignment.Selected);

    public void SetDay(int day, bool hasDeliveryDue = false)
    {
        bool scheduled = Array.IndexOf(marketDays, day) >= 0;
        IsAvailable = hasDeliveryDue || (scheduled && resources != null &&
            (resources.Reputation >= lowReputationThreshold || day == 1 || day == 10 ||
             UnityEngine.Random.value < lowReputationVisitChance));
        if (stallVisual != null) stallVisual.SetActive(IsAvailable);
        if (closedStallLock != null) closedStallLock.SetActive(!IsAvailable);
        HideShop();
        AvailabilityChanged?.Invoke();
    }

    public void RequestVisit(WorkerAssignment worker)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions) return;
        if (!IsAvailable)
        {
            if (feedback != null) feedback.ShowMessage("A barraca está fechada hoje.");
            return;
        }
        if (worker == null)
        {
            if (feedback != null) feedback.ShowMessage("Selecione um trabalhador para visitar a barraca.");
            return;
        }
        if (worker.IsUnavailableToday)
        {
            if (feedback != null) feedback.ShowMessage("Esse trabalhador está ausente hoje.");
            return;
        }
        if (actions == null || stallDestination == null)
        {
            if (feedback != null) feedback.ShowMessage("Barraca não configurada.");
            return;
        }
        if (actions.IsWorkerStoppedNear(worker, stallDestination.position, arrivalRadius))
        {
            ShowShop();
            return;
        }
        if (actions.IsBusy)
        {
            if (feedback != null) feedback.ShowMessage("Aguarde a ação em andamento.");
            return;
        }
        actions.TravelToMerchant(worker, stallDestination.position, ShowShop);
    }

    public int GetUnitPrice(SaleItem item)
    {
        if (resources == null || catalog == null) return 0;
        int basePrice = item switch
        {
            SaleItem.CornSeeds => catalog.Get(CropType.Corn).coinCost,
            SaleItem.PumpkinSeeds => catalog.Get(CropType.Pumpkin).coinCost,
            SaleItem.Food => foodCoinCost,
            _ => 0
        };
        if (basePrice <= 0) return 0;
        int adjustment = resources.Reputation < lowReputationThreshold ? 1 :
            resources.Reputation >= highReputationThreshold ? -1 : 0;
        return Mathf.Max(1, basePrice + adjustment);
    }

    public bool TryBuyCart(int cornSeeds, int pumpkinSeeds, int food)
    {
        if (!IsAvailable || !IsOpen || dayCycle == null || !dayCycle.CanAcceptActions || resources == null ||
            cornSeeds < 0 || pumpkinSeeds < 0 || food < 0) return false;
        long total = (long)cornSeeds * GetUnitPrice(SaleItem.CornSeeds) +
            (long)pumpkinSeeds * GetUnitPrice(SaleItem.PumpkinSeeds) +
            (long)food * GetUnitPrice(SaleItem.Food);
        if (total <= 0 || total > int.MaxValue || !resources.TrySpendCoins((int)total))
        {
            if (feedback != null) feedback.ShowMessage("Moedas insuficientes para a compra.");
            return false;
        }
        if (cornSeeds > 0) resources.AddSeeds(CropType.Corn, cornSeeds);
        if (pumpkinSeeds > 0) resources.AddSeeds(CropType.Pumpkin, pumpkinSeeds);
        if (food > 0) resources.AddFood(food);
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
        if (shopStatusText != null) shopStatusText.text = "Escolha as quantidades para comprar ou vender.";
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
