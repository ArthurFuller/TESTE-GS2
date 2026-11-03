using System;
using System.Collections.Generic;
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
    [SerializeField] private Color jerimumColor = new Color(0.92f, 0.42f, 0.08f);
    [SerializeField, Min(0.1f)] private float adjacentDistance = 1.6f;
    [SerializeField, Min(1f)] private float joinedScale = 1.12f;
    [SerializeField] private GameObject growthBar;
    [SerializeField] private GameObject[] fenceParts;
    [SerializeField, Min(0f)] private float fencePieceDelay = 0.11f;
    [SerializeField, Min(0.01f)] private float fencePieceDuration = 0.2f;
    [SerializeField] private Transform growthFill;
    [SerializeField, Min(0.01f)] private float growthFillWidth = 0.84f;

    public bool IsPlowed { get; private set; }
    public bool IsFenced { get; private set; }
    public CropType Crop { get; private set; }
    public int PlantedDay { get; private set; }
    private int growthAdjustment;
    private Vector3 surfaceBaseScale;
    private static readonly List<Plot> allPlots = new List<Plot>();
    public bool IsEmpty => Crop == CropType.None;
    public event Action StateChanged;

    private void Awake()
    {
        if (surfaceRenderer != null) surfaceBaseScale = surfaceRenderer.transform.localScale;
    }

    private void OnEnable() => allPlots.Add(this);
    private void OnDisable() => allPlots.Remove(this);

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
        if (pumpkinVisual != null) pumpkinVisual.SetActive(crop == CropType.Pumpkin || crop == CropType.Jerimum);
        TintRootVisual(crop);
        if (growthBar != null) growthBar.SetActive(true);
        RefreshGrowth(currentDay, catalog);
        RefreshJoinedPlots();
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
        RefreshJoinedPlots();
        StateChanged?.Invoke();
        return harvested;
    }

    public void AdvanceGrowth(int days)
    {
        if (Crop != CropType.None) growthAdjustment += days;
        StateChanged?.Invoke();
    }

    public void BuildFence()
    {
        if (IsFenced) return;
        IsFenced = true;
        if (fenceParts == null) return;
        for (int i = 0; i < fenceParts.Length; i++)
        {
            GameObject part = fenceParts[i];
            if (part == null) continue;
            part.SetActive(true);
            Transform piece = part.transform;
            Vector3 size = piece.localScale;
            piece.localScale = Vector3.zero;
            piece.DOScale(size, fencePieceDuration).SetDelay(i * fencePieceDelay)
                .SetEase(Ease.OutBack);
        }
        StateChanged?.Invoke();
    }

    private void TintRootVisual(CropType crop)
    {
        if (pumpkinVisual == null) return;
        foreach (Renderer part in pumpkinVisual.GetComponentsInChildren<Renderer>(true))
        {
            if (part.sharedMaterial == null || !part.sharedMaterial.HasProperty("_BaseColor")) continue;
            Color original = part.sharedMaterial.GetColor("_BaseColor");
            var properties = new MaterialPropertyBlock();
            part.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", crop == CropType.Jerimum ? jerimumColor : original);
            part.SetPropertyBlock(properties);
        }
    }

    private static void RefreshJoinedPlots()
    {
        foreach (Plot plot in allPlots)
        {
            if (plot == null || plot.surfaceRenderer == null) continue;
            bool joinX = false;
            bool joinZ = false;
            foreach (Plot other in allPlots)
            {
                if (other == plot || plot.Crop == CropType.None || plot.Crop != other.Crop) continue;
                Vector3 offset = other.transform.position - plot.transform.position;
                if (Mathf.Abs(offset.y) >= 0.2f) continue;
                if (Mathf.Abs(offset.x) < plot.adjacentDistance && Mathf.Abs(offset.z) < 0.2f)
                    joinX = true;
                if (Mathf.Abs(offset.z) < plot.adjacentDistance && Mathf.Abs(offset.x) < 0.2f)
                    joinZ = true;
            }
            Transform surface = plot.surfaceRenderer.transform;
            surface.DOKill();
            surface.DOScale(new Vector3(plot.surfaceBaseScale.x * (joinX ? plot.joinedScale : 1f),
                plot.surfaceBaseScale.y,
                plot.surfaceBaseScale.z * (joinZ ? plot.joinedScale : 1f)), 0.25f)
                .SetEase(Ease.OutBack);
        }
    }
}
