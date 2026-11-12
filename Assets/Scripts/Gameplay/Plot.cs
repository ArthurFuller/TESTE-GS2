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
    [SerializeField] private GameObject plowedVisual; // Visual opcional: mantém o solo lógico e collider.
    [SerializeField, Tooltip("Slot de água: bloqueia ações de cultivo, cercas e animais.")]
    private bool isWater;
    [SerializeField] private GameObject cornVisual;
    [SerializeField] private GameObject pumpkinVisual;
    [SerializeField] private Color jerimumColor = new Color(0.92f, 0.42f, 0.08f);
    [SerializeField, Min(0.1f)] private float adjacentDistance = 2.5f;
    [SerializeField] private GameObject growthBar;
    [SerializeField] private GameObject[] fenceParts;
    [SerializeField] private GameObject joinedFenceModel; // Modelo FBX para cercas unificadas.
    [SerializeField, Min(0f)] private float fencePieceDelay = 0.06f;
    [SerializeField, Min(0.01f)] private float fencePieceDuration = 0.28f;
    [SerializeField] private Transform growthFill;
    [SerializeField, Min(0.01f)] private float growthFillWidth = 0.84f;
    [SerializeField] private Color readyGrowthColor = new Color(0.16f, 0.75f, 0.2f);

    public bool IsPlowed { get; private set; }
    public bool IsFenced { get; private set; }
    public bool IsWater => isWater;
    public CropType Crop { get; private set; }
    public AnimalKind Animal { get; private set; }
    public int PlantedDay { get; private set; }
    private int growthAdjustment;
    private bool fertilized;
    private Bounds surfaceBounds;
    private float workClearance = 1.3f;
    private Vector3[] fenceBasePositions;
    private Vector3[] fenceBaseScales;
    private JoinedVisual joinedVisual;
    private bool fenceInitialized;
    private Transform selectionOutline;
    private Mesh selectionMesh;
    private Material selectionMaterial;
    private bool pointerOver;
    private Transform animalVisual;
    private Renderer growthFillRenderer;
    private Color growingFillColor;
    private MaterialPropertyBlock growthFillBlock;
    private static readonly List<Plot> allPlots = new List<Plot>();
    private static readonly Dictionary<string, JoinedVisual> joinedVisuals =
        new Dictionary<string, JoinedVisual>();
    public bool IsEmpty => !isWater && Crop == CropType.None;
    public Vector3 WorkPosition => transform.position + Vector3.back * workClearance;
    // Mesma regra de vizinhança ortogonal usada pelos cercados.
    public bool IsAdjacentTo(Plot other)
    {
        if (other == null || other == this) return false;
        Vector3 d = other.transform.position - transform.position;
        if (Mathf.Abs(d.y) >= 0.2f) return false;
        return (Mathf.Abs(d.x) < 0.2f && Mathf.Abs(d.z) < adjacentDistance && Mathf.Abs(d.z) > 0.2f) ||
               (Mathf.Abs(d.z) < 0.2f && Mathf.Abs(d.x) < adjacentDistance && Mathf.Abs(d.x) > 0.2f);
    }

    public event Action StateChanged;

    private void Awake()
    {
        if (growthFill != null)
        {
            growthFillRenderer = growthFill.GetComponent<Renderer>();
            if (growthFillRenderer != null && growthFillRenderer.sharedMaterial != null)
                growingFillColor = growthFillRenderer.sharedMaterial.color;
        }
        if (surfaceRenderer != null)
        {
            surfaceBounds = surfaceRenderer.bounds;
            workClearance = surfaceBounds.extents.z + 0.5f;
        }
        if (fenceParts != null)
        {
            fenceBasePositions = new Vector3[fenceParts.Length];
            fenceBaseScales = new Vector3[fenceParts.Length];
            for (int i = 0; i < fenceParts.Length; i++)
            {
                if (fenceParts[i] == null) continue;
                fenceBasePositions[i] = fenceParts[i].transform.localPosition;
                fenceBaseScales[i] = fenceParts[i].transform.localScale;
            }
        }
        if (plowedVisual != null) plowedVisual.SetActive(false);
    }

    private void OnEnable() => allPlots.Add(this);
    private void OnDisable()
    {
        allPlots.Remove(this);
        if (allPlots.Count == 0)
        {
            foreach (JoinedVisual joined in joinedVisuals.Values) joined.Dispose();
            joinedVisuals.Clear();
        }
        SetHighlight(false);
    }

    private void OnDestroy()
    {
        if (selectionMesh != null) Destroy(selectionMesh);
        if (selectionMaterial != null) Destroy(selectionMaterial);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isWater) return;
        pointerOver = true;
        PulseOutline();
        joinedVisual?.SetHovered(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerOver = false;
        PulseOutline();
        joinedVisual?.SetHovered(false);
    }

    public bool CanUse(ActivityKind activity, int currentDay, SeedCatalog catalog)
    {
        if (isWater) return false;
        return activity switch
        {
            ActivityKind.Plow or ActivityKind.BuildFence =>
                !IsPlowed && !IsFenced && IsEmpty && Animal == AnimalKind.None,
            ActivityKind.PlantCorn or ActivityKind.PlantPumpkin or ActivityKind.PlantJerimum =>
                IsPlowed && !IsFenced && IsEmpty && Animal == AnimalKind.None,
            ActivityKind.PlaceCow or ActivityKind.PlaceChicken =>
                IsFenced && IsEmpty && Animal == AnimalKind.None,
            ActivityKind.FeedAnimals => IsFenced && Animal != AnimalKind.None,
            ActivityKind.Harvest => !IsFenced && IsReady(currentDay, catalog),
            ActivityKind.WateringCan => !IsFenced && !IsEmpty &&
                !IsReady(currentDay, catalog) && catalog != null,
            ActivityKind.Fertilizer => !IsFenced && !IsEmpty && !fertilized,
            _ => false
        };
    }

    public static void ShowAvailable(ActivityKind activity, int currentDay, SeedCatalog catalog)
    {
        foreach (Plot plot in allPlots)
        {
            if (plot == null) continue;
            bool groupAvailable = plot.joinedVisual != null &&
                plot.joinedVisual.AllCanUse(activity, currentDay, catalog);
            plot.SetHighlight(!groupAvailable &&
                plot.CanUse(activity, currentDay, catalog));
        }
        foreach (JoinedVisual joined in joinedVisuals.Values)
            joined.SetHighlight(joined.AllCanUse(activity, currentDay, catalog));
    }

    public static bool HasAvailable(ActivityKind activity, int currentDay, SeedCatalog catalog)
    {
        foreach (Plot plot in allPlots)
            if (plot != null && plot.CanUse(activity, currentDay, catalog)) return true;
        return false;
    }

    public static void ClearHighlights()
    {
        foreach (Plot plot in allPlots)
            if (plot != null) plot.SetHighlight(false);
        foreach (JoinedVisual joined in joinedVisuals.Values) joined.SetHighlight(false);
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
        if (isWater) return;
        TutorialHints.Once("plots", "Use os terrenos destacados para arar, plantar, cercar ou cuidar das culturas.");
        if (actionResolver != null)
            actionResolver.TryUsePlot(this);
    }

    public void Plow()
    {
        if (isWater || IsPlowed || IsFenced || Animal != AnimalKind.None)
            return;
        IsPlowed = true;
        if (surfaceRenderer != null && plowedMaterial != null)
            surfaceRenderer.sharedMaterial = plowedMaterial;
        RefreshSurfaceVisual();
        StateChanged?.Invoke();
    }

    public void Plant(CropType crop, int currentDay, SeedCatalog catalog)
    {
        if (isWater || !IsPlowed || IsFenced || !IsEmpty || Animal != AnimalKind.None || crop == CropType.None)
            return;
        Crop = crop;
        PlantedDay = currentDay;
        growthAdjustment = 0;
        fertilized = false;
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
            age <= settings.growthDays + settings.witherDaysAfterMaturity;
    }

    public List<Plot> ReadyConnectedPlots(int currentDay, SeedCatalog catalog)
    {
        return ConnectedPlots(plot => plot.IsReady(currentDay, catalog));
    }

    public List<Plot> ConnectedPlotsFor(ActivityKind activity, int currentDay, SeedCatalog catalog)
    {
        return ConnectedPlots(plot => plot.CanUse(activity, currentDay, catalog));
    }

    private List<Plot> ConnectedPlots(System.Predicate<Plot> eligible)
    {
        var ready = new List<Plot>();
        if (eligible(this)) ready.Add(this);
        if (joinedVisual == null) return ready;

        var remaining = new List<Plot>();
        foreach (Plot member in joinedVisual.Members)
            if (member != null && member != this && eligible(member))
                remaining.Add(member);
        Plot previous = this;
        while (remaining.Count > 0)
        {
            int nearest = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                float candidate = (remaining[i].transform.position - previous.transform.position).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate;
                nearest = i;
            }
            previous = remaining[nearest];
            ready.Add(previous);
            remaining.RemoveAt(nearest);
        }
        return ready;
    }

    public int HarvestYield(int currentDay, SeedCatalog catalog)
    {
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        return settings == null ? 0 : (settings.foodYield +
            Mathf.Max(0, currentDay + growthAdjustment - PlantedDay - settings.growthDays) *
            settings.bonusYieldPerExtraDay) * (fertilized ? 2 : 1) +
            (Crop == CropType.Pumpkin && MandalaSynergy.Instance != null ?
                MandalaSynergy.Instance.ExtraCassavaYield(this) : 0);
    }

    public void RefreshGrowth(int currentDay, SeedCatalog catalog)
    {
        GameObject visual = Crop == CropType.Corn ? cornVisual : pumpkinVisual;
        if (Crop == CropType.None)
            return;
        CropSettings settings = catalog != null ? catalog.Get(Crop) : null;
        int growthDays = settings != null ? settings.growthDays : 2;
        int age = currentDay + growthAdjustment - PlantedDay;
        if (settings != null && age > growthDays + settings.witherDaysAfterMaturity)
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
            if (growthFillRenderer != null)
            {
                growthFillBlock ??= new MaterialPropertyBlock();
                growthFillRenderer.GetPropertyBlock(growthFillBlock);
                Color fillColor = IsReady(currentDay, catalog) ?
                    readyGrowthColor : growingFillColor;
                growthFillBlock.SetColor("_Color", fillColor);
                growthFillBlock.SetColor("_BaseColor", fillColor);
                growthFillRenderer.SetPropertyBlock(growthFillBlock);
            }
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
        if (isWater) return CropType.None;
        CropType harvested = Crop;
        Crop = CropType.None;
        fertilized = false;
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

    public void AdvanceGrowthTowardMaturity(int days, int currentDay, SeedCatalog catalog)
    {
        if (isWater || Crop == CropType.None || days <= 0 || catalog == null) return;
        CropSettings settings = catalog.Get(Crop);
        if (settings == null) return;
        int remaining = settings.growthDays - (currentDay + growthAdjustment - PlantedDay);
        if (remaining > 0) AdvanceGrowth(Mathf.Min(days, remaining));
    }

    public void Fertilize()
    {
        if (isWater || Crop == CropType.None || fertilized) return;
        fertilized = true;
        StateChanged?.Invoke();
    }

    public void BuildFence()
    {
        if (isWater || IsPlowed || IsFenced || !IsEmpty || Animal != AnimalKind.None) return;
        IsFenced = true;
        if (fenceParts != null && fenceBasePositions != null) FormFencePerimeter();
        RefreshJoinedPlots();
        StateChanged?.Invoke();
    }

    private Plot[] FencedNeighbors()
    {
        var neighbors = new Plot[4]; // norte, leste, sul, oeste
        foreach (Plot other in allPlots)
        {
            if (other == this || other == null || Animal == AnimalKind.None ||
                other.Animal != Animal || !other.IsFenced) continue;
            Vector3 offset = other.transform.position - transform.position;
            if (Mathf.Abs(offset.y) >= 0.2f) continue;
            if (Mathf.Abs(offset.x) < 0.2f && Mathf.Abs(offset.z) < adjacentDistance)
                neighbors[offset.z > 0f ? 0 : 2] = other;
            else if (Mathf.Abs(offset.z) < 0.2f && Mathf.Abs(offset.x) < adjacentDistance)
                neighbors[offset.x > 0f ? 1 : 3] = other;
        }
        return neighbors;
    }

    private float FenceHalfX => fenceBasePositions != null && fenceBasePositions.Length > 3 ?
        Mathf.Abs(fenceBasePositions[3].x) + fenceBaseScales[3].x * 0.5f : 0.74f;
    private float FenceHalfZ => fenceBasePositions != null && fenceBasePositions.Length > 0 ?
        Mathf.Abs(fenceBasePositions[0].z) + fenceBaseScales[0].z * 0.5f : 0.785f;

    private bool FenceTarget(int index, Plot[] neighbors, out Vector3 position, out Vector3 scale)
    {
        position = fenceBasePositions[index];
        scale = fenceBaseScales[index];
        int side = index / 3;
        if (neighbors[side] != null) return false;

        int section = index % 3;
        int direction = side == 0 ? (section == 0 ? -1 : section == 2 ? 1 : 0) :
            side == 2 ? (section == 0 ? 1 : section == 2 ? -1 : 0) :
            side == 1 ? (section == 0 ? 1 : section == 2 ? -1 : 0) :
            (section == 0 ? -1 : section == 2 ? 1 : 0);
        if (direction == 0) return true;

        bool horizontal = side == 0 || side == 2;
        Plot adjacent = neighbors[horizontal ? (direction > 0 ? 1 : 3) :
            (direction > 0 ? 0 : 2)];
        if (adjacent == null) return true;
        float ownAxisScale = Mathf.Max(0.01f, Mathf.Abs(horizontal ? transform.lossyScale.x : transform.lossyScale.z));
        float neighborAxisScale = Mathf.Abs(horizontal ? adjacent.transform.lossyScale.x : adjacent.transform.lossyScale.z);
        float distance = Mathf.Abs(horizontal ? adjacent.transform.position.x - transform.position.x :
            adjacent.transform.position.z - transform.position.z);
        float ownHalf = (horizontal ? FenceHalfX : FenceHalfZ) * ownAxisScale;
        float neighborHalf = (horizontal ? adjacent.FenceHalfX : adjacent.FenceHalfZ) * neighborAxisScale;
        float extension = Mathf.Max(0f, (distance - ownHalf - neighborHalf) * 0.5f) / ownAxisScale;
        if (horizontal)
        {
            position.x += direction * extension * 0.5f;
            scale.x += extension;
        }
        else
        {
            position.z += direction * extension * 0.5f;
            scale.z += extension;
        }
        return true;
    }

    private static void RefreshFencedPlots() => RefreshJoinedPlots();

    private void HideOriginalFenceParts()
    {
        if (fenceParts == null || fenceBasePositions == null) return;
        fenceInitialized = false;
        for (int i = 0; i < fenceParts.Length; i++)
        {
            GameObject part = fenceParts[i];
            if (part == null) continue;
            Transform piece = part.transform;
            piece.DOKill();
            if (!part.activeSelf) continue;
            int index = i;
            piece.DOLocalMoveY(fenceBasePositions[i].y - 0.45f,
                fencePieceDuration).SetDelay(i * fencePieceDelay * 0.5f)
                .SetEase(Ease.InSine).OnComplete(() =>
                {
                    part.SetActive(false);
                    piece.localPosition = fenceBasePositions[index];
                    piece.localScale = fenceBaseScales[index];
                });
        }
    }

    private void FormFencePerimeter()
    {
        fenceInitialized = true;
        Plot[] neighbors = FencedNeighbors();
        for (int i = 0; i < fenceParts.Length; i++)
        {
            GameObject part = fenceParts[i];
            if (part == null) continue;
            Transform piece = part.transform;
            bool visible = FenceTarget(i, neighbors, out Vector3 position, out Vector3 scale);
            if (!visible)
            {
                part.SetActive(false);
                piece.localPosition = fenceBasePositions[i];
                piece.localScale = fenceBaseScales[i];
                continue;
            }
            bool arriving = !part.activeSelf;
            if (arriving)
            {
                piece.localPosition = position - Vector3.up * 0.45f;
                piece.localScale = scale;
                DOTween.Sequence().SetTarget(piece)
                    .AppendInterval(i * fencePieceDelay)
                    .AppendCallback(() => part.SetActive(true))
                    .Append(piece.DOLocalMove(position, fencePieceDuration + 0.1f)
                        .SetEase(Ease.OutBack));
                continue;
            }
            if ((piece.localPosition - position).sqrMagnitude < 0.0001f &&
                (piece.localScale - scale).sqrMagnitude < 0.0001f) continue;
            piece.DOLocalMove(position, fencePieceDuration + 0.1f).SetDelay(0.12f)
                .SetEase(Ease.OutSine);
            piece.DOScale(scale, fencePieceDuration + 0.1f).SetDelay(0.12f)
                .SetEase(Ease.OutSine);
        }
    }

    public bool PlaceAnimal(AnimalKind kind)
    {
        if (isWater || !IsFenced || !IsEmpty || Animal != AnimalKind.None || kind == AnimalKind.None)
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
        RefreshFencedPlots();
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

    // O modelo detalhado é somente visual; toda lógica continua no Solo original.
    private void RefreshSurfaceVisual()
    {
        bool showPlowed = plowedVisual != null && IsPlowed &&
            (joinedVisual == null || joinedVisual.UsesDetailedPlowed);
        if (plowedVisual != null && plowedVisual.activeSelf != showPlowed)
            plowedVisual.SetActive(showPlowed);
        if (surfaceRenderer != null)
            surfaceRenderer.enabled = joinedVisual == null && !showPlowed;
    }

    private static void RefreshJoinedPlots()
    {
        var components = new Dictionary<string, List<Plot>>();
        var visited = new HashSet<Plot>();
        foreach (Plot plot in allPlots)
        {
            if (plot == null || plot.surfaceRenderer == null || visited.Contains(plot) ||
                plot.Crop == CropType.None && plot.Animal == AnimalKind.None) continue;
            var members = new List<Plot>();
            var queue = new Queue<Plot>();
            queue.Enqueue(plot);
            visited.Add(plot);
            while (queue.Count > 0)
            {
                Plot current = queue.Dequeue();
                members.Add(current);
                foreach (Plot other in allPlots)
                    if (other != null && !visited.Contains(other) && SameResource(current, other) &&
                        Adjacent(current, other))
                    {
                        visited.Add(other);
                        queue.Enqueue(other);
                    }
            }
            if (members.Count < 2) continue;
            var ids = new List<int>();
            foreach (Plot member in members) ids.Add(member.GetInstanceID());
            ids.Sort();
            components[string.Join(",", ids)] = members;
        }

        var obsolete = new List<string>();
        foreach (string key in joinedVisuals.Keys)
            if (!components.ContainsKey(key)) obsolete.Add(key);
        foreach (string key in obsolete)
        {
            joinedVisuals[key].Dispose(true);
            joinedVisuals.Remove(key);
        }
        foreach (KeyValuePair<string, List<Plot>> entry in components)
        {
            if (!joinedVisuals.TryGetValue(entry.Key, out JoinedVisual joined))
            {
                joined = new JoinedVisual(entry.Value);
                joinedVisuals.Add(entry.Key, joined);
            }
            foreach (Plot member in entry.Value)
                member.joinedVisual = joined;
        }
        foreach (Plot plot in allPlots)
            if (plot != null) plot.RefreshSurfaceVisual();
        foreach (Plot plot in allPlots)
            if (plot != null && plot.IsFenced && plot.joinedVisual == null &&
                !plot.fenceInitialized && plot.fenceParts != null)
                plot.FormFencePerimeter();
    }

    private static bool SameResource(Plot a, Plot b) =>
        (!a.IsFenced && !b.IsFenced && a.Crop != CropType.None && a.Crop == b.Crop) ||
        (a.IsFenced && b.IsFenced && a.Animal != AnimalKind.None && a.Animal == b.Animal);

    private static bool Adjacent(Plot a, Plot b)
    {
        Vector3 distance = b.transform.position - a.transform.position;
        if (Mathf.Abs(distance.y) >= 0.2f) return false;
        float x = Mathf.Abs(distance.x), z = Mathf.Abs(distance.z);
        float reach = Mathf.Min(a.adjacentDistance, b.adjacentDistance);
        return (x > 0.2f && x < reach && z < 0.2f) ||
            (z > 0.2f && z < reach && x < 0.2f);
    }

    private struct BorderEdge
    {
        public bool Horizontal;
        public float Line;
        public float Start;
        public float End;
        public int Normal;
        public BorderEdge(bool horizontal, float line, float start, float end, int normal)
        {
            Horizontal = horizontal;
            Line = line;
            Start = start;
            End = end;
            Normal = normal;
        }
    }

    private sealed class JoinedVisual
    {
        private readonly List<Plot> members;
        public IReadOnlyList<Plot> Members => members;
        private readonly GameObject root;
        private readonly Mesh surfaceMesh;
        private readonly Mesh outlineMesh;
        private readonly Material outlineMaterial;
        private readonly Transform outline;
        private readonly List<Transform> fencePieces = new List<Transform>();
        private bool hovered;
        public bool IsAnimalGroup { get; }
        public bool UsesDetailedPlowed { get; }

        public bool AllCanUse(ActivityKind activity, int currentDay, SeedCatalog catalog)
        {
            foreach (Plot plot in members)
                if (plot == null || !plot.CanUse(activity, currentDay, catalog)) return false;
            return true;
        }

        public JoinedVisual(List<Plot> connected)
        {
            members = connected;
            IsAnimalGroup = connected[0].Animal != AnimalKind.None;
            UsesDetailedPlowed = !IsAnimalGroup;
            foreach (Plot plot in connected)
                if (!plot.IsPlowed || plot.plowedVisual == null)
                    UsesDetailedPlowed = false;
            var regions = BuildRegions(connected);
            float left = float.MaxValue, right = float.MinValue;
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (Rect region in regions)
            {
                left = Mathf.Min(left, region.xMin);
                right = Mathf.Max(right, region.xMax);
                bottom = Mathf.Min(bottom, region.yMin);
                top = Mathf.Max(top, region.yMax);
            }
            Vector2 center = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
            root = new GameObject("Área unificada - " + (IsAnimalGroup ? connected[0].Animal.ToString() :
                connected[0].Crop.ToString()), typeof(MeshFilter), typeof(MeshRenderer),
                typeof(MeshCollider), typeof(PlotJoinedSurface));
            root.transform.SetParent(connected[0].transform.parent, true);
            root.transform.position = new Vector3(center.x, 0f, center.y);
            root.layer = connected[0].gameObject.layer;
            float surfaceTop = connected[0].surfaceBounds.max.y + 0.002f;
            float surfaceBottom = connected[0].surfaceBounds.min.y;
            surfaceMesh = BuildSurface(regions, center, surfaceTop, surfaceBottom,
                out List<BorderEdge> border);
            root.GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
            MeshRenderer surface = root.GetComponent<MeshRenderer>();
            surface.sharedMaterial = connected[0].surfaceRenderer.sharedMaterial;
            root.GetComponent<MeshCollider>().sharedMesh = surfaceMesh;
            if (UsesDetailedPlowed)
            {
                // A união continua recebendo cliques pelo collider existente, mas os FBX
                // individuais permanecem visíveis. Esta malha fica sob o relevo e
                // preenche as pequenas frestas causadas pelas bordas irregulares.
                surface.enabled = false;
                var underlay = new GameObject("Terra de união - preenchimento",
                    typeof(MeshFilter), typeof(MeshRenderer));
                underlay.transform.SetParent(root.transform, false);
                underlay.GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
                underlay.GetComponent<MeshRenderer>().sharedMaterial = surface.sharedMaterial;

                float lowest = surfaceTop;
                foreach (Plot plot in connected)
                    foreach (Renderer part in plot.plowedVisual.GetComponentsInChildren<Renderer>(true))
                        lowest = Mathf.Min(lowest, part.bounds.min.y);
                float depth = Mathf.Clamp(surfaceTop - lowest + 0.015f, 0.035f, 0.15f);
                underlay.transform.localPosition = Vector3.down * depth;
            }
            root.GetComponent<PlotJoinedSurface>().Configure(connected, SetHovered);

            outlineMesh = BuildOutline(border, center, surfaceTop + 0.015f);
            Shader shader = Resources.Load<Shader>("PlotOutline");
            outlineMaterial = shader != null ? new Material(shader) : null;
            var outlineObject = new GameObject("Contorno da área", typeof(MeshFilter),
                typeof(MeshRenderer));
            outline = outlineObject.transform;
            outline.SetParent(root.transform, false);
            outlineObject.GetComponent<MeshFilter>().sharedMesh = outlineMesh;
            MeshRenderer outlineRenderer = outlineObject.GetComponent<MeshRenderer>();
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineObject.SetActive(false);

            if (IsAnimalGroup)
            {
                foreach (Plot plot in members) plot.HideOriginalFenceParts();
                BuildFence(border, center);
            }
            root.transform.localScale = new Vector3(0.975f, 1f, 0.975f);
            DOTween.Sequence().SetTarget(root.transform)
                .Append(root.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutCubic))
                .Append(root.transform.DOPunchScale(new Vector3(0.055f, 0f, 0.055f),
                    0.26f, 4));
        }

        public void SetHighlight(bool visible)
        {
            if (outline == null || outlineMaterial == null) return;
            outline.gameObject.SetActive(visible);
            PulseOutline();
        }

        public void SetHovered(bool value)
        {
            hovered = value;
            PulseOutline();
        }

        private void PulseOutline()
        {
            if (outline == null) return;
            outline.DOKill();
            outline.localScale = Vector3.one;
            if (hovered && outline.gameObject.activeSelf)
                outline.DOScale(1.045f, 0.35f).SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
        }

        public void Dispose(bool animateFence = false)
        {
            foreach (Plot plot in members)
                if (plot != null)
                {
                    plot.joinedVisual = null;
                    if (plot.surfaceRenderer != null) plot.surfaceRenderer.enabled = true;
                }
            if (outline != null) outline.DOKill();
            if (root != null)
            {
                root.transform.DOKill();
                root.transform.localScale = Vector3.one;
            }
            if (animateFence && IsAnimalGroup && fencePieces.Count > 0 && root != null)
            {
                root.GetComponent<MeshRenderer>().enabled = false;
                root.GetComponent<MeshCollider>().enabled = false;
                if (outline != null) outline.gameObject.SetActive(false);
                float finish = 0f;
                for (int i = 0; i < fencePieces.Count; i++)
                {
                    Transform piece = fencePieces[i];
                    if (piece == null) continue;
                    piece.DOKill();
                    float delay = i * 0.012f;
                    finish = delay + 0.2f;
                    piece.DOLocalMoveY(piece.localPosition.y - 0.45f, 0.2f)
                        .SetDelay(delay).SetEase(Ease.InSine);
                }
                DOVirtual.DelayedCall(finish, DestroyAssets);
                return;
            }
            foreach (Transform piece in fencePieces) if (piece != null) piece.DOKill();
            DestroyAssets();
        }

        private void DestroyAssets()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (surfaceMesh != null) UnityEngine.Object.Destroy(surfaceMesh);
            if (outlineMesh != null) UnityEngine.Object.Destroy(outlineMesh);
            if (outlineMaterial != null) UnityEngine.Object.Destroy(outlineMaterial);
        }

        private static Plot Neighbor(List<Plot> plots, Plot origin, int x, int z)
        {
            if (origin == null) return null;
            foreach (Plot plot in plots)
            {
                if (plot == origin || !Adjacent(origin, plot)) continue;
                Vector3 offset = plot.transform.position - origin.transform.position;
                if ((x != 0 && Mathf.Sign(offset.x) == x && Mathf.Abs(offset.z) < 0.2f) ||
                    (z != 0 && Mathf.Sign(offset.z) == z && Mathf.Abs(offset.x) < 0.2f))
                    return plot;
            }
            return null;
        }

        private static List<Rect> BuildRegions(List<Plot> plots)
        {
            const float overlap = 0.01f;
            var regions = new List<Rect>();
            foreach (Plot plot in plots)
            {
                Bounds bounds = plot.surfaceBounds;
                regions.Add(new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z));
            }
            foreach (Plot plot in plots)
            {
                Plot east = Neighbor(plots, plot, 1, 0);
                Plot north = Neighbor(plots, plot, 0, 1);
                Bounds own = plot.surfaceBounds;
                if (east != null)
                {
                    Bounds next = east.surfaceBounds;
                    float z0 = Mathf.Max(own.min.z, next.min.z);
                    float z1 = Mathf.Min(own.max.z, next.max.z);
                    if (next.min.x > own.max.x && z1 > z0)
                        regions.Add(Rect.MinMaxRect(own.max.x - overlap, z0,
                            next.min.x + overlap, z1));
                }
                if (north != null)
                {
                    Bounds next = north.surfaceBounds;
                    float x0 = Mathf.Max(own.min.x, next.min.x);
                    float x1 = Mathf.Min(own.max.x, next.max.x);
                    if (next.min.z > own.max.z && x1 > x0)
                        regions.Add(Rect.MinMaxRect(x0, own.max.z - overlap,
                            x1, next.min.z + overlap));
                }
                if (east == null || north == null || Neighbor(plots, east, 0, 1) == null)
                    continue;
                Bounds right = east.surfaceBounds;
                Bounds above = north.surfaceBounds;
                if (right.min.x > own.max.x && above.min.z > own.max.z)
                    regions.Add(Rect.MinMaxRect(own.max.x - overlap,
                        own.max.z - overlap, right.min.x + overlap,
                        above.min.z + overlap));
            }
            return regions;
        }

        private static List<float> Coordinates(List<Rect> regions, bool xAxis)
        {
            var values = new List<float>();
            foreach (Rect region in regions)
            {
                values.Add(xAxis ? region.xMin : region.yMin);
                values.Add(xAxis ? region.xMax : region.yMax);
            }
            values.Sort();
            var unique = new List<float>();
            foreach (float value in values)
                if (unique.Count == 0 || value - unique[unique.Count - 1] > 0.001f)
                    unique.Add(value);
            return unique;
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
        }

        private static Mesh BuildSurface(List<Rect> regions, Vector2 center, float top,
            float bottom, out List<BorderEdge> border)
        {
            List<float> xs = Coordinates(regions, true);
            List<float> zs = Coordinates(regions, false);
            var filled = new bool[xs.Count - 1, zs.Count - 1];
            for (int x = 0; x < xs.Count - 1; x++)
                for (int z = 0; z < zs.Count - 1; z++)
                {
                    Vector2 point = new Vector2((xs[x] + xs[x + 1]) * 0.5f,
                        (zs[z] + zs[z + 1]) * 0.5f);
                    foreach (Rect region in regions)
                        if (region.Contains(point)) { filled[x, z] = true; break; }
                }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            border = new List<BorderEdge>();
            for (int x = 0; x < xs.Count - 1; x++)
                for (int z = 0; z < zs.Count - 1; z++)
                {
                    if (!filled[x, z]) continue;
                    float x0 = xs[x] - center.x, x1 = xs[x + 1] - center.x;
                    float z0 = zs[z] - center.y, z1 = zs[z + 1] - center.y;
                    AddQuad(vertices, triangles, new Vector3(x0, top, z0),
                        new Vector3(x0, top, z1), new Vector3(x1, top, z1),
                        new Vector3(x1, top, z0));
                    if (z == 0 || !filled[x, z - 1])
                    {
                        border.Add(new BorderEdge(true, zs[z], xs[x], xs[x + 1], -1));
                        AddQuad(vertices, triangles, new Vector3(x0, bottom, z0),
                            new Vector3(x0, top, z0), new Vector3(x1, top, z0),
                            new Vector3(x1, bottom, z0));
                    }
                    if (z == zs.Count - 2 || !filled[x, z + 1])
                    {
                        border.Add(new BorderEdge(true, zs[z + 1], xs[x], xs[x + 1], 1));
                        AddQuad(vertices, triangles, new Vector3(x1, bottom, z1),
                            new Vector3(x1, top, z1), new Vector3(x0, top, z1),
                            new Vector3(x0, bottom, z1));
                    }
                    if (x == 0 || !filled[x - 1, z])
                    {
                        border.Add(new BorderEdge(false, xs[x], zs[z], zs[z + 1], -1));
                        AddQuad(vertices, triangles, new Vector3(x0, bottom, z1),
                            new Vector3(x0, top, z1), new Vector3(x0, top, z0),
                            new Vector3(x0, bottom, z0));
                    }
                    if (x == xs.Count - 2 || !filled[x + 1, z])
                    {
                        border.Add(new BorderEdge(false, xs[x + 1], zs[z], zs[z + 1], 1));
                        AddQuad(vertices, triangles, new Vector3(x1, bottom, z0),
                            new Vector3(x1, top, z0), new Vector3(x1, top, z1),
                            new Vector3(x1, bottom, z1));
                    }
                }
            var mesh = new Mesh { name = "Solo unificado" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildOutline(List<BorderEdge> border, Vector2 center, float y)
        {
            const float halfWidth = 0.035f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (BorderEdge edge in border)
            {
                float a = edge.Start - (edge.Horizontal ? center.x : center.y);
                float b = edge.End - (edge.Horizontal ? center.x : center.y);
                float line = edge.Line - (edge.Horizontal ? center.y : center.x);
                if (edge.Horizontal)
                    AddQuad(vertices, triangles, new Vector3(a, y, line - halfWidth),
                        new Vector3(a, y, line + halfWidth),
                        new Vector3(b, y, line + halfWidth),
                        new Vector3(b, y, line - halfWidth));
                else
                    AddQuad(vertices, triangles,
                        new Vector3(line - halfWidth, y, a),
                        new Vector3(line - halfWidth, y, b),
                        new Vector3(line + halfWidth, y, b),
                        new Vector3(line + halfWidth, y, a));
            }
            var mesh = new Mesh { name = "Contorno unificado" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void BuildFence(List<BorderEdge> border, Vector2 center)
        {
            Plot source = members[0];
            if (source.fenceParts == null || source.fenceParts.Length == 0 ||
                source.fenceParts[0] == null) return;
            Renderer original = source.fenceParts[0].GetComponent<Renderer>();
            if (original == null || source.fenceBaseScales == null) return;
            Transform fenceParent = source.fenceParts[0].transform.parent;
            Vector3 parentScale = fenceParent.lossyScale;
            float height = source.fenceBaseScales[0].y * Mathf.Abs(parentScale.y);
            float thickness = source.fenceBaseScales[0].z * Mathf.Abs(parentScale.z);
            float y = fenceParent.TransformPoint(source.fenceBasePositions[0]).y;
            border.Sort((a, b) =>
            {
                int orientation = a.Horizontal.CompareTo(b.Horizontal);
                if (orientation != 0) return orientation;
                int line = a.Line.CompareTo(b.Line);
                if (line != 0) return line;
                int normal = a.Normal.CompareTo(b.Normal);
                return normal != 0 ? normal : a.Start.CompareTo(b.Start);
            });
            var merged = new List<BorderEdge>();
            foreach (BorderEdge edge in border)
            {
                int last = merged.Count - 1;
                if (last >= 0 && merged[last].Horizontal == edge.Horizontal &&
                    Mathf.Abs(merged[last].Line - edge.Line) < 0.001f &&
                    merged[last].Normal == edge.Normal &&
                    edge.Start <= merged[last].End + 0.001f)
                {
                    BorderEdge extended = merged[last];
                    extended.End = Mathf.Max(extended.End, edge.End);
                    merged[last] = extended;
                }
                else merged.Add(edge);
            }
            int pieceIndex = 0;
            float startDelay = source.fencePieceDuration +
                (source.fenceParts.Length - 1) * source.fencePieceDelay * 0.5f + 0.05f;
            foreach (BorderEdge edge in merged)
            {
                int count = Mathf.Max(1, Mathf.CeilToInt((edge.End - edge.Start) / 0.45f));
                float length = (edge.End - edge.Start) / count;
                for (int i = 0; i < count; i++)
                {
                    GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    part.name = "Cerca modular " + ++pieceIndex;
                    part.transform.SetParent(root.transform, false);
                    float middle = edge.Start + (i + 0.5f) * length;
                    Vector3 position = edge.Horizontal ?
                        new Vector3(middle - center.x, y,
                            edge.Line - center.y - edge.Normal * thickness * 0.5f) :
                        new Vector3(edge.Line - center.x - edge.Normal * thickness * 0.5f,
                            y, middle - center.y);
                    part.transform.localPosition = position - Vector3.up * 0.45f;
                    part.transform.localScale = edge.Horizontal ?
                        new Vector3(length + 0.015f, height, thickness) :
                        new Vector3(thickness, height, length + 0.015f);
                    UnityEngine.Object.Destroy(part.GetComponent<Collider>());
                    Renderer fenceRenderer = part.GetComponent<Renderer>();
                    fenceRenderer.sharedMaterial = original.sharedMaterial;
                    if (source.joinedFenceModel != null)
                    {
                        // Mantém a peça lógica e a animação existentes; troca só a malha visível.
                        GameObject visual = new GameObject("Visual - Cerca FBX");
                        visual.transform.SetParent(part.transform, false);
                        GameObject model = UnityEngine.Object.Instantiate(
                            source.joinedFenceModel, visual.transform);
                        if (GS2VisualFit.Fit(visual.transform, model.transform,
                            new Bounds(Vector3.zero, Vector3.one),
                            GS2VisualFit.Axis.LongX, false))
                            fenceRenderer.enabled = false;
                        else UnityEngine.Object.Destroy(visual);
                    }
                    fencePieces.Add(part.transform);
                    part.transform.DOLocalMove(position, source.fencePieceDuration + 0.1f)
                        .SetDelay(startDelay + pieceIndex * source.fencePieceDelay * 0.45f)
                        .SetEase(Ease.OutBack);
                }
            }
        }
    }
}
