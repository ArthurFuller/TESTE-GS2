using UnityEngine;

public sealed class ActivityCardAvailability : MonoBehaviour
{
    [SerializeField] private ActivityCardDrag[] cards;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private Plot[] plots;
    [SerializeField, Min(0)] private float firstCardX = 8f;
    [SerializeField, Min(1)] private float cardSpacing = 76f;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (resources != null) resources.Changed += Refresh;
        if (merchant != null) merchant.AvailabilityChanged += Refresh;
        if (dayCycle != null) dayCycle.DayChanged += Refresh;
        if (plots != null)
            foreach (Plot plot in plots)
                if (plot != null) plot.StateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        if (resources != null) resources.Changed -= Refresh;
        if (merchant != null) merchant.AvailabilityChanged -= Refresh;
        if (dayCycle != null) dayCycle.DayChanged -= Refresh;
        if (plots != null)
            foreach (Plot plot in plots)
                if (plot != null) plot.StateChanged -= Refresh;
    }

    public void Refresh()
    {
        if (!Application.isPlaying) return;
        if (cards == null || resources == null) return;
        int visible = 0;
        foreach (ActivityCardDrag card in cards)
        {
            if (card == null) continue;
            bool show = IsAvailable(card.Kind);
            card.gameObject.SetActive(show);
            if (!show) continue;
            RectTransform rect = card.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(firstCardX + visible * cardSpacing, rect.anchoredPosition.y);
            visible++;
        }
    }

    private bool IsAvailable(ActivityKind kind)
    {
        switch (kind)
        {
            case ActivityKind.BuySeeds:
                return merchant != null && merchant.IsAvailable;
            case ActivityKind.PlantCorn:
                return resources.CornSeeds > 0 && HasEmptyPlowedPlot();
            case ActivityKind.PlantPumpkin:
                return resources.PumpkinSeeds > 0 && HasEmptyPlowedPlot();
            case ActivityKind.Plow:
                return HasUnplowedPlot();
            case ActivityKind.Harvest:
                return HasReadyPlot();
            case ActivityKind.PrepareFood:
                return resources.Fish + resources.Corn + resources.Pumpkin > 0;
            default:
                return true;
        }
    }

    private bool HasEmptyPlowedPlot()
    {
        if (plots == null) return false;
        foreach (Plot plot in plots)
            if (plot != null && plot.IsPlowed && plot.IsEmpty) return true;
        return false;
    }

    private bool HasUnplowedPlot()
    {
        if (plots == null) return false;
        foreach (Plot plot in plots)
            if (plot != null && !plot.IsPlowed) return true;
        return false;
    }

    private bool HasReadyPlot()
    {
        if (plots == null || dayCycle == null || seedCatalog == null) return false;
        foreach (Plot plot in plots)
            if (plot != null && plot.IsReady(dayCycle.CurrentDay, seedCatalog)) return true;
        return false;
    }
}
