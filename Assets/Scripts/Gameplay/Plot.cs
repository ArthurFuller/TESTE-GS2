using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public enum AnimalKind { None, Cow, Chicken }

public sealed class Plot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
    public AnimalKind Animal { get; private set; }
    public int PlantedDay { get; private set; }
    private int growthAdjustment;
    private Vector3 surfaceBaseScale;
    private Transform selectionOutline;
    private Mesh selectionMesh;
    private Material selectionMaterial;
    private bool pointerOver;
    private Transform animalVisual;
    private static readonly List<Plot> allPlots = new List<Plot>();
    public bool IsEmpty => Crop == CropType.None;
    public event Action StateChanged;

    private void Awake()
    {
        if (surfaceRenderer != null) surfaceBaseScale = surfaceRenderer.transform.localScale;
    }

    private void OnEnable() => allPlots.Add(this);
    private void OnDisable()
    {
        allPlots.Remove(this);
        SetHighlight(false);
    }

    private void OnDestroy()
    {
        if (selectionMesh != null) Destroy(selectionMesh);
        if (selectionMaterial != null) Destroy(selectionMaterial);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerOver = true;
        PulseOutline();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        PulseOutline();
    }

    public static void ShowAvailable(ActivityKind activity)
    {
        foreach (Plot plot in allPlots)
        {
            if (plot == null) continue;
            bool valid = activity == ActivityKind.Plow ?
                !plot.IsPlowed && plot.Animal == AnimalKind.None :
                activity == ActivityKind.BuildFence ? !plot.IsFenced :
                activity == ActivityKind.PlaceCow || activity == ActivityKind.PlaceChicken ?
                    plot.IsFenced && plot.IsEmpty && plot.Animal == AnimalKind.None :
                (activity == ActivityKind.PlantCorn || activity == ActivityKind.PlantPumpkin ||
                 activity == ActivityKind.PlantJerimum) && plot.IsPlowed && plot.IsEmpty &&
                    plot.Animal == AnimalKind.None;
            plot.SetHighlight(valid);
        }
    }

    public static void ClearHighlights()
    {
        foreach (Plot plot in allPlots)
            if (plot != null) plot.SetHighlight(false);
    }

    private void SetHighlight(bool visible)
    {
        if (!visible && selectionOutline == null) return;
        if (visible && selectionOutline == null) CreateOutline();
        if (selectionOutline == null) return;
        selectionOutline.gameObject.SetActive(visible);
        PulseOutline();
    }

    private void PulseOutline()
    {
        if (selectionOutline == null) return;
        selectionOutline.DOKill();
        selectionOutline.localScale = Vector3.one;
        if (selectionOutline.gameObject.activeSelf && pointerOver)
            selectionOutline.DOScale(1.07f, 0.35f).SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
    }

    private void CreateOutline()
    {
        MeshFilter surfaceMesh = surfaceRenderer != null ? surfaceRenderer.GetComponent<MeshFilter>() : null;
        Shader shader = Resources.Load<Shader>("PlotOutline");
        if (surfaceMesh == null || surfaceMesh.sharedMesh == null || shader == null) return;
        Bounds bounds = surfaceMesh.sharedMesh.bounds;
        float left = bounds.min.x, right = bounds.max.x;
        float bottom = bounds.min.z, top = bounds.max.z;
        float width = Mathf.Min(right - left, top - bottom) * 0.045f;
        float y = bounds.max.y + 0.015f;
        var vertices = new Vector3[16];
        var triangles = new int[24];
        AddEdge(vertices, triangles, 0, left, left + width, bottom, top, y);
        AddEdge(vertices, triangles, 1, right - width, right, bottom, top, y);
        AddEdge(vertices, triangles, 2, left + width, right - width, bottom, bottom + width, y);
        AddEdge(vertices, triangles, 3, left + width, right - width, top - width, top, y);
        selectionMesh = new Mesh { vertices = vertices, triangles = triangles };
        selectionMesh.RecalculateBounds();
        selectionMaterial = new Material(shader);
        var outline = new GameObject("Contorno de seleção", typeof(MeshFilter), typeof(MeshRenderer));
        selectionOutline = outline.transform;
        selectionOutline.SetParent(surfaceRenderer.transform, false);
        outline.GetComponent<MeshFilter>().sharedMesh = selectionMesh;
        MeshRenderer outlineRenderer = outline.GetComponent<MeshRenderer>();
        outlineRenderer.sharedMaterial = selectionMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
    }

    private static void AddEdge(Vector3[] vertices, int[] triangles, int edge,
        float minX, float maxX, float minZ, float maxZ, float y)
    {
        int v = edge * 4, t = edge * 6;
        vertices[v] = new Vector3(minX, y, minZ);
        vertices[v + 1] = new Vector3(maxX, y, minZ);
        vertices[v + 2] = new Vector3(maxX, y, maxZ);
        vertices[v + 3] = new Vector3(minX, y, maxZ);
        triangles[t] = v;
        triangles[t + 1] = v + 2;
        triangles[t + 2] = v + 1;
        triangles[t + 3] = v;
        triangles[t + 4] = v + 3;
        triangles[t + 5] = v + 2;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (actionResolver != null)
            actionResolver.TryUsePlot(this);
    }

    public void Plow()
    {
        if (IsPlowed || Animal != AnimalKind.None)
            return;
        IsPlowed = true;
        if (surfaceRenderer != null && plowedMaterial != null)
            surfaceRenderer.sharedMaterial = plowedMaterial;
        StateChanged?.Invoke();
    }

    public void Plant(CropType crop, int currentDay, SeedCatalog catalog)
    {
        if (!IsPlowed || !IsEmpty || Animal != AnimalKind.None || crop == CropType.None)
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

    public bool PlaceAnimal(AnimalKind kind)
    {
        if (!IsFenced || !IsEmpty || Animal != AnimalKind.None || kind == AnimalKind.None)
            return false;
        Animal = kind;
        var root = new GameObject(kind == AnimalKind.Cow ? "Vaca" : "Galinha");
        root.transform.SetParent(transform, false);
        animalVisual = root.transform;
        if (kind == AnimalKind.Cow)
        {
            AddAnimalPart(root.transform, "Corpo", new Vector3(0f, 0.43f, 0f),
                new Vector3(0.62f, 0.38f, 0.46f), new Color(0.92f, 0.9f, 0.8f));
            AddAnimalPart(root.transform, "Cabeça", new Vector3(0.33f, 0.47f, 0f),
                new Vector3(0.29f, 0.29f, 0.28f), new Color(0.3f, 0.24f, 0.19f));
            AddAnimalPart(root.transform, "Mancha", new Vector3(-0.09f, 0.64f, 0.07f),
                new Vector3(0.21f, 0.04f, 0.19f), new Color(0.29f, 0.23f, 0.18f));
        }
        else
        {
            AddAnimalPart(root.transform, "Corpo", new Vector3(0f, 0.38f, 0f),
                new Vector3(0.36f, 0.32f, 0.35f), new Color(0.98f, 0.83f, 0.37f));
            AddAnimalPart(root.transform, "Cabeça", new Vector3(0.18f, 0.53f, 0f),
                new Vector3(0.2f, 0.2f, 0.2f), new Color(1f, 0.9f, 0.52f));
            AddAnimalPart(root.transform, "Bico", new Vector3(0.31f, 0.51f, 0f),
                new Vector3(0.12f, 0.07f, 0.12f), new Color(0.95f, 0.39f, 0.12f));
        }
        root.transform.localScale = Vector3.zero;
        root.transform.DOScale(1f, 0.32f).SetEase(Ease.OutBack)
            .OnComplete(() => root.transform.DOPunchScale(Vector3.one * 0.08f, 0.2f));
        StateChanged?.Invoke();
        return true;
    }

    public void PulseAnimal()
    {
        if (animalVisual == null) return;
        animalVisual.DOKill();
        animalVisual.localScale = Vector3.one;
        animalVisual.DOPunchScale(Vector3.one * 0.12f, 0.3f);
    }

    private static void AddAnimalPart(Transform parent, string name, Vector3 position,
        Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Collider hitbox = part.GetComponent<Collider>();
        if (hitbox != null) Destroy(hitbox);
        Renderer renderer = part.GetComponent<Renderer>();
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", color);
        tint.SetColor("_Color", color);
        renderer.SetPropertyBlock(tint);
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
