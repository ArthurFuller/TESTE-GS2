using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ActivityCardAvailability : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
{
    [SerializeField] private ActivityCardDrag[] cards;
    [SerializeField] private ActivityCardDrag[] cardPrefabs;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private DragCursorPreview dragPreview;
    [Header("Disposição da mão")]
    [SerializeField, Range(5, 15)] private int maxHandCards = 15;
    [SerializeField, Min(140f)] private float handWidth = 480f;
    [SerializeField, Min(1f)] private float cardSpacing = 110f;
    [SerializeField] private float cardBaseY = 16f;
    [SerializeField, Min(0f)] private float arcHeight = 10f;
    [SerializeField, Range(0f, 30f)] private float arcAngle = 12f;
    [SerializeField, Min(0f)] private float containerPadding = 8f;
    [Header("Carta selecionada")]
    [SerializeField, Min(0f)] private float hoverLift = 18f;
    [SerializeField, Min(1f)] private float hoverScale = 1.08f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.16f;
    [SerializeField, Min(0f)] private float hoverSpread = 22f;
    [SerializeField] private RectTransform descriptionPanel;
    [SerializeField] private Text descriptionText;
    [SerializeField, Min(0f)] private float descriptionGap = 12f;
    [Header("Teste durante Play Mode")]
    [SerializeField, Range(0, 15)] private int testCardCount;
    [SerializeField] private ActivityCardDrag testCardSource;

    private readonly List<ActivityCardDrag> shown = new List<ActivityCardDrag>();
    private readonly List<ActivityCardDrag> testCards = new List<ActivityCardDrag>();
    private readonly HashSet<ActivityCardDrag> blocked = new HashSet<ActivityCardDrag>();
    private readonly Dictionary<Graphic, Color> originalColors = new Dictionary<Graphic, Color>();
    private ActivityCardDrag hovered;
    private ActivityCardDrag dragging;
    private bool pointerInside;
    private float firstCenter;
    private float spacing;
    private float maxCardHeight;
    private float maxCardWidth;
    private float fanRise;
    private float curveStrength;
    private float edgeY;
    private float availableWidth;
    private float availableHeight;
    private float normalScale = 1f;
    private Vector2 lastContainerSize;
    private int nextCardId;
    public bool HasRoom => shown.Count < maxHandCards || testCards.Count > 0;
    public int FreeSlots => Mathf.Max(0, maxHandCards - shown.Count + testCards.Count);

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += ApplyInspectorChanges;
            return;
        }
#endif
        if (resources != null) resources.Changed += Refresh;
        if (merchant != null) merchant.AvailabilityChanged += Refresh;
        if (actions != null) actions.CardUsed += OnCardUsed;
        if (dayCycle != null) dayCycle.DayChanged += UnblockCards;
        Refresh();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorChanges;
#endif
        if (!Application.isPlaying) return;
        if (resources != null) resources.Changed -= Refresh;
        if (merchant != null) merchant.AvailabilityChanged -= Refresh;
        if (actions != null) actions.CardUsed -= OnCardUsed;
        if (dayCycle != null) dayCycle.DayChanged -= UnblockCards;
        ClearHover(true);
        if (dragging != null) dragging.CancelDrag();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyInspectorChanges;
        UnityEditor.EditorApplication.delayCall += ApplyInspectorChanges;
    }

    private void ApplyInspectorChanges()
    {
        if (this == null || cards == null || dragging != null) return;
        ActivityCardDrag selected = hovered;
        if (Application.isPlaying) ClearHover(true);
        if (Application.isPlaying) Refresh();
        else CollectEditorCards();
        if (Application.isPlaying && SyncTestCards()) Refresh();
        Layout(true);
        if (Application.isPlaying && selected != null && shown.Contains(selected)) SetHover(selected);
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying || dragging != null) return;
        Vector2 size = ((RectTransform)transform.parent).rect.size;
        if (size != lastContainerSize)
        {
            lastContainerSize = size;
            ClearHover(true);
            Layout(true);
        }
        if (!pointerInside || Mouse.current == null) return;
        SetHover(CardAt(Mouse.current.position.ReadValue(), null));
    }

    public void Refresh()
    {
        if (!Application.isPlaying || cards == null || resources == null) return;
        bool changed = false;
        if (shown.Count == 0)
        {
            foreach (ActivityCardDrag card in cards)
            {
                if (card == null || IsSeed(card.Kind) || card.Kind == ActivityKind.PrepareFood ||
                    card.Kind == ActivityKind.BuySeeds ||
                    card.Kind == ActivityKind.BuildHouse || card.Kind == ActivityKind.BuildFence) continue;
                SetVisible(card, true);
                shown.Add(card);
            }
            foreach (ActivityCardDrag card in cards)
                if (card != null && card.Kind == ActivityKind.PrepareFood)
                    SetVisible(card, false);
            changed = true;
        }

        foreach (ActivityCardDrag card in cards)
            if (card != null && card.Kind == ActivityKind.BuySeeds)
                SetVisible(card, merchant != null && merchant.IsAvailable);

        if (shown.Count > maxHandCards)
        {
            ClearHover(true);
            while (shown.Count > maxHandCards && RemoveLastSeed()) { }
            changed = true;
        }
        if (changed) Layout(true);
    }

    private void CollectEditorCards()
    {
        shown.Clear();
        foreach (ActivityCardDrag card in cards)
            if (card != null && card.gameObject.activeSelf && card.Kind != ActivityKind.BuySeeds &&
                card.Kind != ActivityKind.PrepareFood &&
                !IsSeed(card.Kind))
                shown.Add(card);
    }

    private static bool IsSeed(ActivityKind kind) =>
        kind == ActivityKind.PlantCorn || kind == ActivityKind.PlantPumpkin ||
        kind == ActivityKind.PlantJerimum;

    private void OnCardUsed(ActivityCardDrag card)
    {
        if (card == null || !shown.Contains(card)) return;
        if (card.Kind == ActivityKind.BuildFence || card.Kind == ActivityKind.BuildHouse)
        {
            DissolveAndRemove(card);
            return;
        }
        ClearHover(true);
        blocked.Add(card);
        foreach (Graphic graphic in card.GetComponentsInChildren<Graphic>(true))
        {
            originalColors[graphic] = graphic.color;
            Color color = graphic.color;
            graphic.color = new Color(color.r * 0.45f, color.g * 0.45f, color.b * 0.45f, color.a);
        }
    }

    private void DissolveAndRemove(ActivityCardDrag card)
    {
        ClearHover(true);
        shown.Remove(card);
        Shader shader = Shader.Find("UI/CardDissolve");
        Graphic[] graphics = card.GetComponentsInChildren<Graphic>(true);
        var materials = new List<Material>();
        foreach (Graphic graphic in graphics)
        {
            if (graphic == null) continue;
            if (shader != null)
            {
                Material material = new Material(shader);
                graphic.material = material;
                materials.Add(material);
                material.DOFloat(1f, "_Cutoff", 0.45f);
            }
            else graphic.DOFade(0f, 0.45f);
        }
        DOVirtual.DelayedCall(0.45f, () =>
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
            if (card != null) Destroy(card.gameObject);
        });
        Layout(true);
    }

    private void UnblockCards()
    {
        foreach (var entry in originalColors)
            if (entry.Key != null) entry.Key.color = entry.Value;
        originalColors.Clear();
        blocked.Clear();
    }

    private int CountCards(ActivityKind kind)
    {
        int count = 0;
        foreach (ActivityCardDrag card in shown)
            if (card.Kind == kind && !testCards.Contains(card)) count++;
        return count;
    }

#if UNITY_EDITOR
    private bool SyncTestCards()
    {
        bool changed = false;
        int target = Mathf.Min(testCardCount, Mathf.Max(0, maxHandCards - shown.Count + testCards.Count));
        while (testCards.Count > target)
        {
            ActivityCardDrag card = testCards[testCards.Count - 1];
            testCards.RemoveAt(testCards.Count - 1);
            RemoveCard(card);
            changed = true;
        }

        ActivityCardDrag source = testCardSource;
        if (source == null && cards != null)
            foreach (ActivityCardDrag card in cards)
                if (card != null && card.Kind != ActivityKind.BuySeeds)
                {
                    source = card;
                    break;
                }

        while (source != null && testCards.Count < target && shown.Count < maxHandCards)
        {
            ActivityCardDrag copy = Instantiate(source, transform.parent);
            copy.name = "Teste - " + source.name + " " + (testCards.Count + 1);
            if (!AddCard(copy, false))
            {
                Destroy(copy.gameObject);
                break;
            }
            testCards.Add(copy);
            changed = true;
        }
        return changed;
    }
#endif

    public bool AddCard(ActivityKind kind) => AddCard(kind, true);

    public ActivityCardDrag TemplateFor(ActivityKind kind)
    {
        if (cardPrefabs != null)
            foreach (ActivityCardDrag prefab in cardPrefabs)
                if (prefab != null && prefab.Kind == kind) return prefab;
        if (cards != null)
            foreach (ActivityCardDrag card in cards)
                if (card != null && card.Kind == kind) return card;
        return null;
    }

    private bool AddCard(ActivityKind kind, bool arrange)
    {
        ActivityCardDrag prefab = TemplateFor(kind);
        if (prefab == null || shown.Count >= maxHandCards && !RemoveOneTestCard()) return false;
        ActivityCardDrag copy = Instantiate(prefab, transform.parent);
        copy.name = prefab.name + " " + ++nextCardId;
        if (AddCard(copy, arrange)) return true;
        Destroy(copy.gameObject);
        return false;
    }

    public bool AddCard(ActivityCardDrag card) => AddCard(card, true);

    private bool AddCard(ActivityCardDrag card, bool arrange)
    {
        if (card == null || shown.Contains(card)) return false;
        if (shown.Count >= maxHandCards && !RemoveOneTestCard()) return false;
        ClearHover(true);
        card.gameObject.SetActive(false);
        card.transform.SetParent(transform.parent, false);
        card.ConfigureRuntime(worldCamera, dragPreview);
        card.gameObject.SetActive(true);
        shown.Add(card);
        if (arrange) Layout(true);
        return true;
    }

    public bool RemoveCard(ActivityCardDrag card)
    {
        if (card == null || !shown.Contains(card)) return false;
        ClearHover(true);
        shown.Remove(card);
        card.gameObject.SetActive(false);
        Destroy(card.gameObject);
        Layout(true);
        return true;
    }

    private bool RemoveOneTestCard()
    {
        if (testCards.Count == 0) return false;
        ActivityCardDrag card = testCards[testCards.Count - 1];
        testCards.RemoveAt(testCards.Count - 1);
        return RemoveCard(card);
    }

    private void RemoveLast(ActivityKind kind)
    {
        for (int i = shown.Count - 1; i >= 0; i--)
        {
            if (shown[i].Kind != kind || testCards.Contains(shown[i])) continue;
            GameObject removed = shown[i].gameObject;
            shown.RemoveAt(i);
            removed.SetActive(false);
            Destroy(removed);
            return;
        }
    }

    private bool RemoveLastSeed()
    {
        for (int i = shown.Count - 1; i >= 0; i--)
            if (IsSeed(shown[i].Kind) && !testCards.Contains(shown[i]))
            {
                RemoveLast(shown[i].Kind);
                return true;
            }
        return false;
    }

    private static void SetVisible(ActivityCardDrag card, bool visible)
    {
        if (card.gameObject.activeSelf != visible) card.gameObject.SetActive(visible);
    }

    private void Layout(bool orderCards)
    {
        if (shown.Count == 0) return;
        RectTransform area = (RectTransform)transform.parent;
        availableWidth = area.rect.width;
        availableHeight = area.rect.height;
        lastContainerSize = area.rect.size;
        maxCardWidth = 0f;
        maxCardHeight = 0f;
        foreach (ActivityCardDrag card in shown)
        {
            Rect rect = card.GetComponent<RectTransform>().rect;
            maxCardWidth = Mathf.Max(maxCardWidth, rect.width);
            maxCardHeight = Mathf.Max(maxCardHeight, rect.height);
        }
        float padding = Mathf.Min(containerPadding, availableWidth * 0.25f, availableHeight * 0.25f);
        float radians = arcAngle * Mathf.Deg2Rad;
        float rotatedWidth = maxCardWidth * Mathf.Cos(radians) + maxCardHeight * Mathf.Sin(radians);
        float rotatedHeight = maxCardHeight * Mathf.Cos(radians) + maxCardWidth * Mathf.Sin(radians);
        normalScale = Mathf.Min(1f, (availableWidth - 2f * padding) / rotatedWidth,
            (availableHeight - 2f * padding) / rotatedHeight);
        normalScale = Mathf.Max(0.01f, normalScale);
        edgeY = Mathf.Max(padding, cardBaseY) + maxCardWidth * normalScale * Mathf.Sin(radians) * 0.5f;
        edgeY = Mathf.Min(edgeY, Mathf.Max(padding, availableHeight - padding - maxCardHeight * normalScale));
        float descriptionRoom = descriptionPanel == null ? 0f : descriptionPanel.rect.height + descriptionGap;
        if (descriptionRoom + maxCardHeight * normalScale + padding * 2f > availableHeight)
            descriptionRoom = 0f;
        float riseLimit = availableHeight - padding - descriptionRoom - hoverLift -
            edgeY - maxCardHeight * normalScale;
        float width = Mathf.Min(handWidth, availableWidth - 2f * padding);
        float halfSpan = Mathf.Max(1f, (width - rotatedWidth * normalScale) * 0.5f);
        fanRise = Mathf.Min(arcHeight * 3.5f, halfSpan * Mathf.Tan(radians) * 0.5f,
            Mathf.Max(0f, riseLimit));
        curveStrength = fanRise / (halfSpan * halfSpan);
        spacing = shown.Count == 1 ? 0f : Mathf.Max(0f,
            Mathf.Min(cardSpacing, (width - rotatedWidth * normalScale) / (shown.Count - 1)));
        firstCenter = -spacing * (shown.Count - 1) * 0.5f;
        for (int i = 0; i < shown.Count; i++)
        {
            RectTransform rect = shown[i].GetComponent<RectTransform>();
            rect.DOKill();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            Vector2 position = CardPosition(i);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, CardAngle(position.x));
            rect.localScale = Vector3.one * normalScale;
        }
        if (orderCards) OrderCards();
    }

    private Vector2 CardPosition(int index)
    {
        float heightOffset = (maxCardHeight - shown[index].GetComponent<RectTransform>().rect.height) * normalScale;
        float x = firstCenter + index * spacing;
        return new Vector2(x, edgeY + heightOffset + fanRise - curveStrength * x * x);
    }

    private float CardAngle(float x) => -Mathf.Atan(2f * curveStrength * x) * Mathf.Rad2Deg;

    private void OrderCards()
    {
        foreach (ActivityCardDrag card in shown) card.transform.SetAsLastSibling();
        if (descriptionPanel != null && descriptionPanel.gameObject.activeSelf) descriptionPanel.SetAsLastSibling();
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (shown.Count == 0) return false;
        for (int i = 0; i < shown.Count; i++)
            if (ContainsCard(i, screenPoint, eventCamera)) return true;
        return false;
    }

    private ActivityCardDrag CardAt(Vector2 screenPoint, Camera eventCamera)
    {
        RectTransform area = (RectTransform)transform.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, eventCamera, out Vector2 point))
            return null;
        float x = point.x - area.rect.center.x;
        ActivityCardDrag result = null;
        float nearest = float.MaxValue;
        for (int i = 0; i < shown.Count; i++)
        {
            if (!ContainsCard(i, screenPoint, eventCamera)) continue;
            float distance = Mathf.Abs(x - CardPosition(i).x);
            if (distance >= nearest) continue;
            nearest = distance;
            result = shown[i];
        }
        return result;
    }

    private bool ContainsCard(int index, Vector2 screenPoint, Camera eventCamera)
    {
        if (blocked.Contains(shown[index])) return false;
        RectTransform rect = shown[index].GetComponent<RectTransform>();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera,
                out Vector2 current)) return false;
        Rect visible = VisibleRect(rect, shown[index].GetComponent<Image>());
        if (visible.Contains(current)) return true;

        RectTransform parent = (RectTransform)rect.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, eventCamera,
                out Vector2 point)) return false;
        Vector2 baseline = Quaternion.Euler(0f, 0f, -CardAngle(CardPosition(index).x)) *
            (point - CardPosition(index)) / normalScale;
        return visible.Contains(baseline);
    }

    private static Rect VisibleRect(RectTransform rect, Image image)
    {
        Rect bounds = rect.rect;
        if (image == null || !image.preserveAspect || image.sprite == null) return bounds;
        Vector2 sprite = image.sprite.rect.size;
        float scale = Mathf.Min(bounds.width / sprite.x, bounds.height / sprite.y);
        Vector2 size = sprite * scale;
        return new Rect(bounds.center - size * 0.5f, size);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        SetHover(CardAt(eventData.position, eventData.enterEventCamera));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (dragging == null) ClearHover(false);
    }

    private void SetHover(ActivityCardDrag card)
    {
        if (card == hovered) return;
        ClearHover(false);
        hovered = card;
        if (hovered == null) return;
        RectTransform rect = hovered.GetComponent<RectTransform>();
        rect.SetAsLastSibling();
        int index = shown.IndexOf(hovered);
        MoveNeighbours(index, true);
        float padding = containerPadding;
        float descriptionRoom = descriptionPanel == null ? 0f : descriptionPanel.rect.height + descriptionGap;
        if (descriptionRoom + rect.rect.height * normalScale + padding * 2f > availableHeight)
            descriptionRoom = 0f;
        float scale = normalScale * Mathf.Min(hoverScale,
            (availableWidth - 2f * padding) / (rect.rect.width * normalScale),
            (availableHeight - 2f * padding - descriptionRoom) / (rect.rect.height * normalScale));
        float top = Mathf.Max(padding + rect.rect.height * scale,
            Mathf.Min(availableHeight - padding - descriptionRoom,
                CardPosition(index).y + rect.rect.height * normalScale + hoverLift));
        float x = Mathf.Clamp(CardPosition(index).x,
            -availableWidth * 0.5f + padding + rect.rect.width * scale * 0.5f,
            availableWidth * 0.5f - padding - rect.rect.width * scale * 0.5f);
        AnimateCard(rect, new Vector2(x, top - rect.rect.height * scale), scale, 0f, false);
        if (descriptionPanel == null || descriptionText == null || descriptionRoom == 0f) return;
        descriptionText.text = hovered.Description;
        descriptionPanel.DOKill();
        CanvasGroup group = descriptionPanel.GetComponent<CanvasGroup>();
        if (group != null) group.DOKill();
        float tooltipX = Mathf.Clamp(x,
            -availableWidth * 0.5f + padding + descriptionPanel.rect.width * 0.5f,
            availableWidth * 0.5f - padding - descriptionPanel.rect.width * 0.5f);
        Vector2 target = new Vector2(tooltipX, top + descriptionGap);
        descriptionPanel.anchoredPosition = target - Vector2.up * 8f;
        descriptionPanel.gameObject.SetActive(!string.IsNullOrWhiteSpace(hovered.Description));
        descriptionPanel.SetAsLastSibling();
        if (descriptionPanel.gameObject.activeSelf)
        {
            descriptionPanel.DOAnchorPos(target, hoverDuration).SetEase(Ease.OutCubic);
            if (group != null)
            {
                group.alpha = 0f;
                group.DOFade(1f, hoverDuration);
            }
        }
    }

    private void ClearHover(bool immediate)
    {
        if (hovered == null) return;
        RectTransform rect = hovered.GetComponent<RectTransform>();
        if (descriptionPanel != null)
        {
            descriptionPanel.DOKill();
            CanvasGroup group = descriptionPanel.GetComponent<CanvasGroup>();
            if (group != null) group.DOKill();
            descriptionPanel.gameObject.SetActive(false);
        }
        int index = shown.IndexOf(hovered);
        if (index >= 0)
        {
            MoveNeighbours(index, false, immediate);
            Vector2 position = CardPosition(index);
            AnimateCard(rect, position, normalScale, CardAngle(position.x), immediate);
        }
        hovered = null;
        OrderCards();
    }

    private void MoveNeighbours(int selected, bool apart, bool immediate = false)
    {
        for (int i = 0; i < shown.Count; i++)
        {
            if (i == selected) continue;
            RectTransform rect = shown[i].GetComponent<RectTransform>();
            float offset = apart ? Mathf.Sign(i - selected) * hoverSpread : 0f;
            Vector2 baseline = CardPosition(i);
            Vector2 position = baseline + Vector2.right * offset;
            float rotation = CardAngle(baseline.x);
            float angle = Mathf.Abs(rotation) * Mathf.Deg2Rad;
            float half = (rect.rect.width * Mathf.Cos(angle) + rect.rect.height * Mathf.Sin(angle)) *
                normalScale * 0.5f;
            position.x = Mathf.Clamp(position.x,
                -availableWidth * 0.5f + containerPadding + half,
                availableWidth * 0.5f - containerPadding - half);
            AnimateCard(rect, position, normalScale, rotation, immediate);
        }
    }

    private void AnimateCard(RectTransform rect, Vector2 position, float scale, float angle, bool immediate)
    {
        rect.DOKill();
        if (immediate)
        {
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            return;
        }
        rect.DOAnchorPos(position, hoverDuration).SetEase(Ease.OutCubic);
        rect.DOScale(scale, hoverDuration).SetEase(Ease.OutCubic);
        rect.DOLocalRotate(new Vector3(0f, 0f, angle), hoverDuration).SetEase(Ease.OutCubic);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ActivityCardDrag card = CardAt(eventData.pressPosition, eventData.pressEventCamera);
        if (card == null || testCards.Contains(card) || !card.HasEffect) return;
        ClearHover(true);
        dragging = card;
        dragging.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging != null) dragging.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragging == null) return;
        dragging.OnEndDrag(eventData);
        dragging = null;
        if (pointerInside) SetHover(CardAt(eventData.position, eventData.enterEventCamera));
    }

}
