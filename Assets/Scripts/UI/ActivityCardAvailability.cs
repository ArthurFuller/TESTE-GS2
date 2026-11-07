using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class ActivityCardAvailability : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
{
    [SerializeField] private ActivityCardDrag[] cards;
    [SerializeField] private ActivityCardDrag[] cardPrefabs;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DayTimeline timeline;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
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
    private readonly List<ActivityCardDrag> spent = new List<ActivityCardDrag>();
    private readonly List<ActivityCardDrag> deckOrder = new List<ActivityCardDrag>();
    private readonly List<ActivityCardDrag> testCards = new List<ActivityCardDrag>();
    private readonly List<ActivityKind> pendingRewards = new List<ActivityKind>();
    private ActivityCardDrag hovered;
    private ActivityCardDrag dragging;
    private Tween shopPositionTween;
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
    private bool initialized;
    private bool ducked;
    private bool entranceSuspended;
    private bool selectionLocked;
    [SerializeField, Min(0.1f)] private float dayExitDuration = 0.38f;
    [SerializeField, Min(0f)] private float dayExitWaveDelay = 0.07f;
    public bool HasRoom => shown.Count + spent.Count + pendingRewards.Count < maxHandCards ||
        testCards.Count > 0;
    public int FreeSlots => Mathf.Max(0, maxHandCards - shown.Count - spent.Count -
        pendingRewards.Count + testCards.Count);
    public bool ContainsCard(ActivityCardDrag card) => shown.Contains(card);
    public bool IsSelectionLocked => selectionLocked;
    public bool IsDraggingCard
    {
        get
        {
            if (dragging != null) return true;
            foreach (ActivityCardDrag card in shown)
                if (card != null && card.IsDragging) return true;
            return false;
        }
    }
    public RectTransform InspectionDescriptionTemplate => descriptionPanel;
    public float InspectionDescriptionGap => descriptionGap;
    public void InspectCard(ActivityCardDrag card)
    {
        if (card == null || !shown.Contains(card) || selectionLocked ||
            entranceSuspended || dragging != null || card.IsReturning ||
            timeline == null) return;
        timeline.InspectHandCard(card);
    }
    public void SuspendHoverForInspection()
    {
        pointerInside = false;
        ClearHover(true);
    }
    public void ClosePopupsForDrag()
    {
        if (timeline != null) timeline.ClosePanel();
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (merchant != null && merchant.IsOpen) merchant.HideShop();
    }
    public bool HasDisposable(ActivityKind kind) => FindDisposable(kind) != null;
    public bool ConsumeReplayDisposable(ActivityKind kind)
    {
        ActivityCardDrag card = FindDisposable(kind);
        if (card == null) return false;
        OnCardUsed(card);
        return true;
    }

    private ActivityCardDrag FindDisposable(ActivityKind kind)
    {
        if (kind != ActivityKind.BuildFence && kind != ActivityKind.BuildHouse) return null;
        foreach (ActivityCardDrag card in shown)
            if (card != null && card.Kind == kind) return card;
        return null;
    }
    public RectTransform DragBoundary => (RectTransform)transform.parent;

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
        if (actions != null) actions.PendingSelectionChanged += SetSelectionLocked;
        if (dayCycle == null) dayCycle = FindFirstObjectByType<DayCycle>();
        if (dayCycle != null) dayCycle.DayChanged += OnDayChanged;
        if (descriptionPanel != null) descriptionPanel.gameObject.SetActive(false);
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
        if (actions != null) actions.PendingSelectionChanged -= SetSelectionLocked;
        if (dayCycle != null) dayCycle.DayChanged -= OnDayChanged;
        DOTween.Kill(this);
        shopPositionTween?.Kill();
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
        if (entranceSuspended || selectionLocked || !pointerInside || Mouse.current == null) return;
        SetHover(CardAt(Mouse.current.position.ReadValue(), null));
    }

    public void Refresh()
    {
        if (!Application.isPlaying || cards == null || resources == null) return;
        bool changed = false;
        if (!initialized)
        {
            initialized = true;
            foreach (ActivityCardDrag card in cards)
            {
                if (card == null || IsSeed(card.Kind) || card.Kind == ActivityKind.PrepareFood ||
                    card.Kind == ActivityKind.BuySeeds ||
                    card.Kind == ActivityKind.BuildHouse || card.Kind == ActivityKind.BuildFence) continue;
                SetVisible(card, true);
                shown.Add(card);
                deckOrder.Add(card);
            }
            foreach (ActivityCardDrag card in cards)
                if (card != null && card.Kind == ActivityKind.PrepareFood)
                    SetVisible(card, false);
            changed = true;
        }

        foreach (ActivityCardDrag card in cards)
            if (card != null && card.Kind == ActivityKind.BuySeeds)
            {
                SetVisible(card, !entranceSuspended);
                RectTransform shopCard = (RectTransform)card.transform;
                float targetY = merchant != null && merchant.IsAvailable ? 77f : -43f;
                shopPositionTween?.Kill();
                shopPositionTween = shopCard.DOAnchorPosY(targetY, 0.2f)
                    .SetEase(Ease.OutCubic);
            }

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
        ClearHover(true);
        shown.Remove(card);
        if (card.Kind == ActivityKind.BuildFence || card.Kind == ActivityKind.BuildHouse)
        {
            deckOrder.Remove(card);
            testCards.Remove(card);
            card.FadeOut(() => { if (card != null) Destroy(card.gameObject); });
        }
        else
        {
            spent.Add(card);
            card.FadeOut(() => { if (card != null) card.gameObject.SetActive(false); });
        }
        Layout(true, true);
    }

    private void OnDayChanged()
    {
        AnimateDayEntrance(true);
    }

    private void SetSelectionLocked(bool locked)
    {
        selectionLocked = locked;
        ClearHover(true);
        if (locked)
            foreach (ActivityCardDrag card in shown)
                if (card != null && !card.IsDragging && !card.IsReturning)
                    card.ResetHoverForSelection();
        ducked = locked;
        Layout(true, true);
    }

    public void ReturnAfterCancelledDay()
    {
        AnimateDayEntrance(false);
    }

    private void AnimateDayEntrance(bool returnSpent)
    {
        entranceSuspended = false;
        if (returnSpent) AddPendingRewards();
        Refresh();
        if (!initialized || shown.Count == 0 && spent.Count == 0) return;
        ClearHover(true);
        ducked = false;
        Layout(true);
        int returningCount = returnSpent ? spent.Count : 0;
        float waveEnd = 0f;
        for (int i = 0; i < shown.Count; i++)
        {
            ActivityCardDrag card = shown[i];
            SetVisible(card, true);
            RectTransform rect = (RectTransform)card.transform;
            Vector2 target = CardPosition(i);
            rect.DOKill();
            rect.anchoredPosition = new Vector2(target.x, -rect.rect.height * normalScale - 80f);
            float delay = i * 0.07f;
            rect.DOAnchorPos(target, 0.42f).SetDelay(delay).SetEase(Ease.OutBack);
            rect.DOLocalRotate(new Vector3(0f, 0f, CardAngle(target.x)), 0.42f)
                .SetDelay(delay).SetEase(Ease.OutCubic);
            waveEnd = delay + 0.42f;
        }
        if (returningCount > 0)
            DOVirtual.DelayedCall(waveEnd + 0.08f, ReturnSpentCards).SetTarget(this);
    }

    private void AddPendingRewards()
    {
        for (int i = 0; i < pendingRewards.Count;)
        {
            if (AddCard(pendingRewards[i], false)) pendingRewards.RemoveAt(i);
            else i++;
        }
    }

    public void SuspendEntranceForEvent()
    {
        if (!initialized) Refresh();
        entranceSuspended = true;
        ClearHover(true);
        foreach (ActivityCardDrag card in shown)
            if (card != null) SetVisible(card, false);
        foreach (ActivityCardDrag card in cards)
            if (card != null && card.Kind == ActivityKind.BuySeeds) SetVisible(card, false);
    }

    public void ExitForDay(System.Action onExited)
    {
        ClearHover(true);
        entranceSuspended = true;
        pointerInside = false;
        float waveEnd = 0f;
        for (int i = 0; i < shown.Count; i++)
        {
            ActivityCardDrag card = shown[i];
            if (card == null || !card.gameObject.activeSelf) continue;
            RectTransform rect = (RectTransform)card.transform;
            rect.DOKill();
            float delay = i * dayExitWaveDelay;
            rect.DOAnchorPosY(-rect.rect.height * normalScale - 80f, dayExitDuration)
                .SetDelay(delay).SetEase(Ease.InCubic);
            waveEnd = Mathf.Max(waveEnd, delay + dayExitDuration);
        }
        foreach (ActivityCardDrag card in cards)
        {
            if (card == null || card.Kind != ActivityKind.BuySeeds || !card.gameObject.activeSelf) continue;
            RectTransform rect = (RectTransform)card.transform;
            shopPositionTween?.Kill();
            rect.DOKill();
            rect.DOAnchorPosY(-rect.rect.height - 80f, dayExitDuration)
                .SetEase(Ease.InCubic);
            waveEnd = Mathf.Max(waveEnd, dayExitDuration);
        }
        DOVirtual.DelayedCall(waveEnd, () =>
        {
            foreach (ActivityCardDrag card in shown)
                if (card != null) SetVisible(card, false);
            foreach (ActivityCardDrag card in cards)
                if (card != null && card.Kind == ActivityKind.BuySeeds) SetVisible(card, false);
            onExited?.Invoke();
        }).SetTarget(this);
    }

    private void ReturnSpentCards()
    {
        var returningCards = new List<ActivityCardDrag>(spent);
        spent.Clear();
        foreach (ActivityCardDrag card in returningCards)
        {
            if (card == null) continue;
            card.RestoreAppearance();
            card.gameObject.SetActive(true);
            shown.Add(card);
        }
        shown.Sort((a, b) => deckOrder.IndexOf(a).CompareTo(deckOrder.IndexOf(b)));
        Layout(true, true);
        for (int i = 0; i < returningCards.Count; i++)
        {
            ActivityCardDrag card = returningCards[i];
            if (card == null) continue;
            int index = shown.IndexOf(card);
            RectTransform rect = (RectTransform)card.transform;
            Vector2 target = CardPosition(index);
            rect.DOKill();
            rect.anchoredPosition = new Vector2(target.x, -rect.rect.height * normalScale - 80f);
            rect.localScale = Vector3.one * normalScale * 0.85f;
            rect.DOAnchorPos(target, 0.38f).SetDelay(i * 0.08f).SetEase(Ease.OutBack);
            rect.DOScale(normalScale, 0.38f).SetDelay(i * 0.08f).SetEase(Ease.OutBack);
            rect.DOLocalRotate(new Vector3(0f, 0f, CardAngle(target.x)), 0.38f)
                .SetDelay(i * 0.08f);
        }
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

    public bool AddCommunityHouseCard()
    {
        if (TemplateFor(ActivityKind.BuildHouse) == null) return false;
        maxHandCards = Mathf.Max(maxHandCards, shown.Count + spent.Count + pendingRewards.Count + 1);
        return AddCard(ActivityKind.BuildHouse);
    }

    public bool QueueRewardCard(ActivityKind kind)
    {
        if (TemplateFor(kind) == null || !HasRoom) return false;
        pendingRewards.Add(kind);
        return true;
    }

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
        if (prefab == null || shown.Count + spent.Count >= maxHandCards &&
            !RemoveOneTestCard()) return false;
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
        if (shown.Count + spent.Count >= maxHandCards && !RemoveOneTestCard()) return false;
        ClearHover(true);
        card.gameObject.SetActive(false);
        card.transform.SetParent(transform.parent, false);
        card.ConfigureRuntime(worldCamera, dragPreview);
        card.gameObject.SetActive(true);
        shown.Add(card);
        deckOrder.Add(card);
        if (arrange)
        {
            Layout(true, true);
            int index = shown.IndexOf(card);
            RectTransform rect = (RectTransform)card.transform;
            Vector2 target = CardPosition(index);
            rect.DOKill();
            rect.anchoredPosition = new Vector2(target.x,
                -rect.rect.height * normalScale - 80f);
            rect.localScale = Vector3.one * normalScale * 0.85f;
            rect.DOAnchorPos(target, 0.38f).SetEase(Ease.OutBack);
            rect.DOScale(normalScale, 0.38f).SetEase(Ease.OutBack);
            rect.DOLocalRotate(new Vector3(0f, 0f, CardAngle(target.x)), 0.38f);
        }
        return true;
    }

    public bool RemoveCard(ActivityCardDrag card)
    {
        if (card == null || !shown.Contains(card)) return false;
        ClearHover(true);
        shown.Remove(card);
        deckOrder.Remove(card);
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

    private void Layout(bool orderCards, bool animate = false)
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
        float riseLimit = availableHeight - padding - hoverLift -
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
            ActivityCardDrag card = shown[i];
            RectTransform rect = card.GetComponent<RectTransform>();
            if (card == dragging || card.IsDragging || card.IsReturning) continue;
            rect.DOKill();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            Vector2 position = CardPosition(i) + Vector2.down * (ducked ? 40f : 0f);
            if (animate)
            {
                rect.DOAnchorPos(position, 0.24f)
                    .SetEase(Ease.OutCubic);
                rect.DOLocalRotate(new Vector3(0f, 0f, CardAngle(position.x)), 0.24f);
                rect.DOScale(normalScale, 0.24f);
            }
            else
            {
                rect.anchoredPosition = position;
                rect.localRotation = Quaternion.Euler(0f, 0f, CardAngle(position.x));
                rect.localScale = Vector3.one * normalScale;
            }
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
        if (entranceSuspended || selectionLocked || shown.Count == 0) return false;
        for (int i = 0; i < shown.Count; i++)
            if (!shown[i].IsReturning && ContainsCard(i, screenPoint, eventCamera)) return true;
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
            if (shown[i].IsReturning) continue;
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
        if (selectionLocked) return;
        pointerInside = true;
        SetHover(CardAt(eventData.position, eventData.enterEventCamera));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (dragging == null) ClearHover(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            eventData.dragging || dragging != null || selectionLocked) return;
        InspectCard(CardAt(eventData.position, eventData.pressEventCamera));
    }

    private void SetHover(ActivityCardDrag card)
    {
        if (selectionLocked) return;
        if (card == hovered) return;
        ClearHover(false);
        hovered = card;
        if (hovered == null) return;
        RectTransform rect = hovered.GetComponent<RectTransform>();
        CardHoverOutline.Ensure(rect)?.SetHighlighted(true);
        rect.SetAsLastSibling();
        int index = shown.IndexOf(hovered);
        MoveNeighbours(index, true);
        float padding = containerPadding;
        float scale = normalScale * Mathf.Min(hoverScale,
            (availableWidth - 2f * padding) / (rect.rect.width * normalScale),
            (availableHeight - 2f * padding) / (rect.rect.height * normalScale));
        float top = Mathf.Max(padding + rect.rect.height * scale,
            Mathf.Min(availableHeight - padding,
                CardPosition(index).y + rect.rect.height * normalScale + hoverLift));
        float x = Mathf.Clamp(CardPosition(index).x,
            -availableWidth * 0.5f + padding + rect.rect.width * scale * 0.5f,
            availableWidth * 0.5f - padding - rect.rect.width * scale * 0.5f);
        AnimateCard(rect, new Vector2(x, top - rect.rect.height * scale), scale, 0f, false);
    }

    private void ClearHover(bool immediate)
    {
        if (hovered == null) return;
        RectTransform rect = hovered.GetComponent<RectTransform>();
        hovered.GetComponent<CardHoverOutline>()?.SetHighlighted(false);
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
            if (!hovered.IsReturning)
            {
                Vector2 position = CardPosition(index);
                AnimateCard(rect, position, normalScale, CardAngle(position.x), immediate);
            }
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
        if (selectionLocked) return;
        ActivityCardDrag card = CardAt(eventData.pressPosition, eventData.pressEventCamera);
        if (card == null || testCards.Contains(card) || !card.HasEffect) return;
        ClearHover(true);
        dragging = card;
        dragging.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragging == null) return;
        dragging.OnDrag(eventData);
        UpdateDucked(eventData.position, eventData.pressEventCamera);
    }

    public void UpdateDucked(Vector2 screenPosition, Camera eventCamera)
    {
        if (selectionLocked) return;
        bool outside = !RectTransformUtility.RectangleContainsScreenPoint(DragBoundary,
            screenPosition, eventCamera);
        if (outside == ducked) return;
        ducked = outside;
        Layout(true, true);
    }

    public void EndExternalDrag()
    {
        if (selectionLocked)
        {
            ducked = true;
            Layout(true, true);
            return;
        }
        if (!ducked) return;
        ducked = false;
        Layout(true, true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragging == null) return;
        dragging.OnEndDrag(eventData);
        dragging = null;
        ducked = selectionLocked;
        Layout(true, true);
        if (pointerInside && !selectionLocked)
            SetHover(CardAt(eventData.position, eventData.enterEventCamera));
    }

}
