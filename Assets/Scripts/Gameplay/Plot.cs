using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public sealed class Plot : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ActionResolver actionResolver;
    [SerializeField] private Renderer surfaceRenderer;
    [SerializeField] private Material plowedMaterial;
    [SerializeField] private GameObject cornVisual;
    [SerializeField] private GameObject pumpkinVisual;

    public bool IsPlowed { get; private set; }
    public CropType Crop { get; private set; }
    public int PlantedDay { get; private set; }
    public bool IsEmpty => Crop == CropType.None;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (actionResolver != null)
            actionResolver.TryUsePlot(this);
    }

    public void Plow()
    {
        if (IsPlowed)
            return;
        IsPlowed = true;
        if (surfaceRenderer != null && plowedMaterial != null)
            surfaceRenderer.sharedMaterial = plowedMaterial;
    }

    public void Plant(CropType crop, int currentDay)
    {
        if (!IsPlowed || !IsEmpty || crop == CropType.None)
            return;
        Crop = crop;
        PlantedDay = currentDay;
        if (cornVisual != null) cornVisual.SetActive(crop == CropType.Corn);
        if (pumpkinVisual != null) pumpkinVisual.SetActive(crop == CropType.Pumpkin);
        RefreshGrowth(currentDay, null);
    }

    public bool IsReady(int currentDay, SeedCatalog catalog)
    {
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        return settings != null && currentDay >= PlantedDay + settings.growthDays;
    }

    public void RefreshGrowth(int currentDay, SeedCatalog catalog)
    {
        GameObject visual = Crop == CropType.Corn ? cornVisual : pumpkinVisual;
        if (Crop == CropType.None || visual == null)
            return;
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        int growthDays = settings != null ? settings.growthDays : 2;
        float progress = Mathf.Clamp01((float)(currentDay - PlantedDay) / growthDays);
        visual.transform.DOKill();
        visual.transform.DOScale(new Vector3(1f, Mathf.Lerp(0.45f, 1f, progress), 1f), 0.35f);
    }

    public CropType Harvest()
    {
        CropType harvested = Crop;
        Crop = CropType.None;
        if (cornVisual != null) cornVisual.SetActive(false);
        if (pumpkinVisual != null) pumpkinVisual.SetActive(false);
        return harvested;
    }
}
