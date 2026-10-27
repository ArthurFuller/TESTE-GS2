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
    [SerializeField] private Transform stallDestination;
    [SerializeField] private CanvasGroup shopGroup;
    [SerializeField] private Text shopStatusText;
    [SerializeField] private int[] marketDays = { 1, 4, 7, 10 };
    [SerializeField, Min(0)] private float panelFadeDuration = 0.25f;

    public bool IsAvailable { get; private set; }

    private void Awake()
    {
        HideShop();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        RequestVisit(WorkerAssignment.Selected);
    }

    public void SetDay(int day)
    {
        IsAvailable = System.Array.IndexOf(marketDays, day) >= 0;
        if (stallVisual != null) stallVisual.SetActive(IsAvailable);
        HideShop();
    }

    public void RequestVisit(WorkerAssignment worker)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (!IsAvailable)
        {
            feedback.ShowMessage("A barraca não está disponível hoje.");
            return;
        }
        if (worker == null)
        {
            feedback.ShowMessage("Selecione um trabalhador para visitar a barraca.");
            return;
        }
        if (actions == null || actions.IsBusy || stallDestination == null)
        {
            feedback.ShowMessage("Aguarde a ação em andamento.");
            return;
        }
        actions.TravelToMerchant(worker, stallDestination.position, ShowShop);
    }

    public void BuyCorn() => Buy(CropType.Corn);
    public void BuyPumpkin() => Buy(CropType.Pumpkin);

    private void Buy(CropType crop)
    {
        if (!IsAvailable || resources == null || catalog == null || shopGroup == null ||
            !shopGroup.interactable || dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        CropSettings settings = catalog.Get(crop);
        if (settings == null || !resources.TrySpendWood(settings.woodCost))
        {
            if (shopStatusText != null) shopStatusText.text = "Madeira insuficiente.";
            feedback.ShowMessage("Madeira insuficiente para comprar sementes.");
            return;
        }
        resources.AddSeeds(crop, settings.seedsPerPurchase);
        if (shopStatusText != null)
            shopStatusText.text = "+" + settings.seedsPerPurchase + " sementes de " + settings.displayName;
        feedback.ShowMessage("Sementes de " + settings.displayName + " compradas.");
    }

    public void ShowShop()
    {
        if (shopGroup == null)
            return;
        shopGroup.DOKill();
        shopGroup.blocksRaycasts = true;
        shopGroup.interactable = true;
        shopGroup.DOFade(1, panelFadeDuration);
        if (shopStatusText != null) shopStatusText.text = "Escolha as sementes.";
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
