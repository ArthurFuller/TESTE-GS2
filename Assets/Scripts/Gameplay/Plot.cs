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
    [SerializeField, Min(0.1f)] private float adjacentDistance = 2.5f;
    [SerializeField] private GameObject growthBar;
    [SerializeField] private GameObject[] fenceParts;
    [SerializeField, Min(0f)] private float fencePieceDelay = 0.06f;
    [SerializeField, Min(0.01f)] private float fencePieceDuration = 0.28f;
    [SerializeField] private Transform growthFill;
    [SerializeField, Min(0.01f)] private float growthFillWidth = 0.84f;

    public bool IsPlowed { get; private set; }
    public bool IsFenced { get; private set; }
    public CropType Crop { get; private set; }
    public AnimalKind Animal { get; private set; }
    public int PlantedDay { get; private set; }
    private int growthAdjustment;
    private Vector3 surfaceBaseScale;
    private Vector3 surfaceBaseWorldSize;
    private Vector3 surfaceBaseLocalPosition;
    private float workClearance = 1.3f;
    private Vector3[] fenceBasePositions;
    private Vector3[] fenceBaseScales;
    private bool joinedHorizontally;
    private bool joinedVertically;
    private Sequence joinedMotion;
    private Transform selectionOutline;
    private Mesh selectionMesh;
    private Material selectionMaterial;
    private bool pointerOver;
    private Transform animalVisual;
    private static readonly List<Plot> allPlots = new List<Plot>();
    private static Tween fenceTransition;
    public bool IsEmpty => Crop == CropType.None;
    public Vector3 WorkPosition => transform.position + Vector3.back * workClearance;
    public event Action StateChanged;

    private void Awake()
    {
        if (surfaceRenderer != null)
        {
            surfaceBaseScale = surfaceRenderer.transform.localScale;
            surfaceBaseLocalPosition = surfaceRenderer.transform.localPosition;
            surfaceBaseWorldSize = surfaceRenderer.bounds.size;
            workClearance = surfaceRenderer.bounds.extents.z + 0.5f;
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
    }

    private void OnEnable() => allPlots.Add(this);
    private void OnDisable()
    {
        allPlots.Remove(this);
        joinedMotion?.Kill();
        if (allPlots.Count == 0) fenceTransition?.Kill();
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
                !plot.IsPlowed && !plot.IsFenced && plot.Animal == AnimalKind.None :
                activity == ActivityKind.BuildFence ? !plot.IsFenced :
                activity == ActivityKind.PlaceCow || activity == ActivityKind.PlaceChicken ?
                    plot.IsFenced && plot.IsEmpty && plot.Animal == AnimalKind.None :
                (activity == ActivityKind.PlantCorn || activity == ActivityKind.PlantPumpkin ||
                 activity == ActivityKind.PlantJerimum) && plot.IsPlowed && plot.IsEmpty &&
                    !plot.IsFenced && plot.Animal == AnimalKind.None;
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
        if (IsPlowed || IsFenced || Animal != AnimalKind.None)
            return;
        IsPlowed = true;
        if (surfaceRenderer != null && plowedMaterial != null)
            surfaceRenderer.sharedMaterial = plowedMaterial;
        StateChanged?.Invoke();
    }

    public void Plant(CropType crop, int currentDay, SeedCatalog catalog)
    {
        if (!IsPlowed || IsFenced || !IsEmpty || Animal != AnimalKind.None || crop == CropType.None)
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
            age <= settings.growthDays + settings.witherDaysAfterMaturity;
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

    private static void RefreshFencedPlots()
    {
        fenceTransition?.Kill();
        float descentEnd = 0f;
        foreach (Plot plot in allPlots)
        {
            if (plot == null || !plot.IsFenced || plot.fenceParts == null ||
                plot.fenceBasePositions == null) continue;
            Plot[] neighbors = plot.FencedNeighbors();
            for (int i = 0; i < plot.fenceParts.Length; i++)
            {
                GameObject part = plot.fenceParts[i];
                if (part == null) continue;
                Transform piece = part.transform;
                piece.DOKill();
                if (!part.activeSelf || plot.FenceTarget(i, neighbors, out _, out _)) continue;
                int pieceIndex = i;
                float delay = i * plot.fencePieceDelay;
                descentEnd = Mathf.Max(descentEnd, delay + plot.fencePieceDuration);
                piece.DOLocalMoveY(plot.fenceBasePositions[i].y - 0.45f,
                    plot.fencePieceDuration).SetDelay(delay).SetEase(Ease.InSine)
                    .OnComplete(() =>
                    {
                        part.SetActive(false);
                        piece.localPosition = plot.fenceBasePositions[pieceIndex];
                        piece.localScale = plot.fenceBaseScales[pieceIndex];
                    });
            }
        }
        fenceTransition = DOVirtual.DelayedCall(descentEnd, () =>
        {
            RefreshJoinedPlots();
            foreach (Plot plot in allPlots)
            {
                if (plot == null || !plot.IsFenced || plot.fenceParts == null ||
                    plot.fenceBasePositions == null) continue;
                plot.FormFencePerimeter();
            }
        });
    }

    private void FormFencePerimeter()
    {
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

    private static void RefreshJoinedPlots()
    {
        foreach (Plot plot in allPlots)
        {
            if (plot == null || plot.surfaceRenderer == null) continue;
            float east = 0f, west = 0f, north = 0f, south = 0f;
            foreach (Plot other in allPlots)
            {
                if (other == plot) continue;
                bool sameCrop = !plot.IsFenced && !other.IsFenced &&
                    plot.Crop != CropType.None && plot.Crop == other.Crop;
                bool sameAnimal = plot.IsFenced && other.IsFenced &&
                    plot.Animal != AnimalKind.None && plot.Animal == other.Animal;
                if (!sameCrop && !sameAnimal) continue;
                Vector3 offset = other.transform.position - plot.transform.position;
                if (Mathf.Abs(offset.y) >= 0.2f) continue;
                if (Mathf.Abs(offset.x) < plot.adjacentDistance && Mathf.Abs(offset.z) < 0.2f)
                {
                    float gap = Mathf.Max(0f, Mathf.Abs(offset.x) -
                        (plot.surfaceBaseWorldSize.x + other.surfaceBaseWorldSize.x) * 0.5f + 0.02f);
                    if (offset.x > 0f) east = Mathf.Max(east, gap * 0.5f);
                    else west = Mathf.Max(west, gap * 0.5f);
                }
                if (Mathf.Abs(offset.z) < plot.adjacentDistance && Mathf.Abs(offset.x) < 0.2f)
                {
                    float gap = Mathf.Max(0f, Mathf.Abs(offset.z) -
                        (plot.surfaceBaseWorldSize.z + other.surfaceBaseWorldSize.z) * 0.5f + 0.02f);
                    if (offset.z > 0f) north = Mathf.Max(north, gap * 0.5f);
                    else south = Mathf.Max(south, gap * 0.5f);
                }
            }
            Transform surface = plot.surfaceRenderer.transform;
            bool joinX = east > 0f || west > 0f;
            bool joinZ = north > 0f || south > 0f;
            bool newlyJoined = (joinX && !plot.joinedHorizontally) ||
                (joinZ && !plot.joinedVertically);
            plot.joinedHorizontally = joinX;
            plot.joinedVertically = joinZ;
            plot.joinedMotion?.Kill();
            Vector3 target = new Vector3(plot.surfaceBaseScale.x *
                    (1f + (east + west) / Mathf.Max(0.01f, plot.surfaceBaseWorldSize.x)),
                plot.surfaceBaseScale.y,
                plot.surfaceBaseScale.z *
                    (1f + (north + south) / Mathf.Max(0.01f, plot.surfaceBaseWorldSize.z)));
            Vector3 centerShift = plot.transform.InverseTransformVector(new Vector3(
                (east - west) * 0.5f, 0f, (north - south) * 0.5f));
            Vector3 targetPosition = plot.surfaceBaseLocalPosition + centerShift;
            Sequence motion = DOTween.Sequence()
                .Append(surface.DOScale(target, 0.38f).SetEase(Ease.OutSine))
                .Join(surface.DOLocalMove(targetPosition, 0.38f).SetEase(Ease.OutSine));
            plot.joinedMotion = motion;
            if (newlyJoined)
                motion.Append(surface.DOPunchScale(new Vector3(target.x * 0.025f, 0f,
                    target.z * 0.025f), 0.18f, 1));
        }
    }
}
