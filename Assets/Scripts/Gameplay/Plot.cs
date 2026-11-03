using System;
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
    [SerializeField] private GameObject growthBar;
    [SerializeField] private Transform growthFill;
    [SerializeField, Min(0.01f)] private float growthFillWidth = 0.84f;

    public bool IsPlowed { get; private set; }
    public CropType Crop { get; private set; }
    public int PlantedDay { get; private set; }
    private int growthAdjustment;
    public bool IsEmpty => Crop == CropType.None;
    public event Action StateChanged;

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
        StateChanged?.Invoke();
    }

    public void Plant(CropType crop, int currentDay, SeedCatalog catalog)
    {
        if (!IsPlowed || !IsEmpty || crop == CropType.None)
            return;
        Crop = crop;
        PlantedDay = currentDay;
        growthAdjustment = 0;
        if (cornVisual != null) cornVisual.SetActive(crop == CropType.Corn);
        if (pumpkinVisual != null) pumpkinVisual.SetActive(crop == CropType.Pumpkin);
        if (growthBar != null) growthBar.SetActive(true);
        RefreshGrowth(currentDay, catalog);
        StateChanged?.Invoke();
    }

    public bool IsReady(int currentDay, SeedCatalog catalog)
    {
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        int age = currentDay + growthAdjustment - PlantedDay;
        return settings != null && age >= settings.growthDays &&
            age < settings.growthDays + settings.witherDaysAfterMaturity;
    }

    public int HarvestYield(int currentDay, SeedCatalog catalog)
    {
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        return settings == null ? 0 : settings.foodYield +
            Mathf.Max(0, currentDay + growthAdjustment - PlantedDay - settings.growthDays) *
            settings.bonusYieldPerExtraDay;
    }

    public void RefreshGrowth(int currentDay, SeedCatalog catalog)
    {
        GameObject visual = Crop == CropType.Corn ? cornVisual : pumpkinVisual;
        if (Crop == CropType.None)
            return;
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        int growthDays = settings != null ? settings.growthDays : 2;
        int age = currentDay + growthAdjustment - PlantedDay;
        if (settings != null && age >= growthDays + settings.witherDaysAfterMaturity)
        {
            Harvest();
            return;
        }
        float progress = Mathf.Clamp01((float)age / growthDays);
        if (visual != null)
        {
            visual.transform.DOKill();
            visual.transform.DOScale(new Vector3(1f, Mathf.Lerp(0.45f, 1f, progress), 1f), 0.35f);
        }
        if (growthFill != null)
        {
            float width = Mathf.Max(0.001f, growthFillWidth * progress);
            Vector3 size = growthFill.localScale;
            growthFill.localScale = new Vector3(width, size.y, size.z);
            Vector3 position = growthFill.localPosition;
            growthFill.localPosition = new Vector3(-growthFillWidth * 0.5f + width * 0.5f,
                position.y, position.z);
        }
    }

    public CropType Harvest()
    {
        CropType harvested = Crop;
        Crop = CropType.None;
        if (cornVisual != null) cornVisual.SetActive(false);
        if (pumpkinVisual != null) pumpkinVisual.SetActive(false);
        if (growthBar != null) growthBar.SetActive(false);
        StateChanged?.Invoke();
        return harvested;
    }

    public void AdvanceGrowth(int days)
    {
        if (Crop != CropType.None) growthAdjustment += days;
        StateChanged?.Invoke();
    }
}
