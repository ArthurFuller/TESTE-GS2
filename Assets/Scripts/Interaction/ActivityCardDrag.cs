using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class ActivityCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private string activityName;
    [SerializeField, TextArea(2, 3)] private string description;
    [SerializeField] private ActivityKind activity;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private DragCursorPreview preview;
    [SerializeField] private SeedMerchant shop;

    private WorkerAssignment hoveredWorker;
    private bool dragging;
    private bool cancelled;
    private int originalSibling;
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private bool hovered;
    private bool returning;
    private bool shopClicking;
    private Tween shopClickTween;
    private Tween shopHoverTween;
    private Vector3 dragWorldOffset;
    private RectTransform handBoundary;
    private Camera dragEventCamera;
    private Vector2 dragPointerScreen;
    private float dragCenterBlend;
    private bool miniature;
    private Tween dragCenterTween;
    private Quaternion homeRotation;
    private int dragSibling;
    private Graphic[] fadeGraphics;
    private Color[] fadeColors;
    private bool replayAnimating;
    private WorkerAssignment deliveryWorker;
    private Tween pendingFloat;
    private Vector2 homePivot;
    private float dragAngle;
    private float angleVelocity;
    private float horizontalSpeed;
    private float lastPointerMove;
    private Vector2 previousPointer;
    private bool waitingForTarget;
    private SeedCatalog seedCatalog;
    private InventoryTrading trading;
    private ActivityCardAvailability Hand => transform.parent != null ?
        transform.parent.GetComponentInChildren<ActivityCardAvailability>(true) : null;

    public ActivityKind Kind => activity;
    public string DisplayName => activityName;
    public string Description
    {
        get
        {
            CropType crop = activity switch
            {
                ActivityKind.PlantCorn => CropType.Corn,
                ActivityKind.PlantPumpkin => CropType.Pumpkin,
                ActivityKind.PlantJerimum => CropType.Jerimum,
                _ => CropType.None
            };
            if (crop == CropType.None) return description;
            if (seedCatalog == null) seedCatalog = FindFirstObjectByType<SeedCatalog>();
            if (trading == null) trading = FindFirstObjectByType<InventoryTrading>();
            CropSettings settings = seedCatalog != null ? seedCatalog.Get(crop) : null;
            if (settings == null || trading == null) return description;
            SaleItem item = crop == CropType.Corn ? SaleItem.Corn :
                crop == CropType.Pumpkin ? SaleItem.Pumpkin : SaleItem.Jerimum;
            return "Plante em terreno arado. Cresce em " + settings.growthDays +
                (settings.growthDays == 1 ? " dia" : " dias") + ", rende " +
                settings.foodYield + " por colheita e vende por " +
                trading.GetPrice(item) + " moedas/un.";
        }
    }
    public bool HasEffect => true;
    public bool IsReturning => returning;
    public bool IsDragging => dragging;
    public void EndHoverForInspection() => RestoreHover(true);
    public bool IsReplayAnimating => replayAnimating;
    public void ResetHoverForSelection() => RestoreHover(true);

    public void PlayRejectedReplay()
    {
        returning = true;
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        float center = rect.anchoredPosition.x;
        DOTween.Sequence()
            .Append(rect.DOAnchorPosX(center + 18f, 0.045f))
            .Append(rect.DOAnchorPosX(center - 18f, 0.06f))
            .Append(rect.DOAnchorPosX(center + 15f, 0.055f))
            .Append(rect.DOAnchorPosX(center - 12f, 0.055f))
            .Append(rect.DOAnchorPosX(center, 0.05f))
            .SetTarget(rect)
            .OnComplete(AnimateReturn)
            .OnKill(() => { if (!waitingForTarget) returning = false; });
    }

    public void PlayReplay(WorkerAssignment worker, ActivityKind repeated,
        System.Action onFinished)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        RectTransform rect = (RectTransform)transform;
        if (root == null || worker == null)
        {
            onFinished?.Invoke();
            return;
        }
        replayAnimating = true;
        returning = true;
        Image source = GetComponent<Image>();
        ActivityCardDrag templateCard = transform.parent
            .GetComponentInChildren<ActivityCardAvailability>(true)?.TemplateFor(repeated);
        Image template = templateCard != null ? templateCard.GetComponent<Image>() : null;
        Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, startScreen,
            canvas.worldCamera, out Vector2 start);
        var shadeObject = new GameObject("Foco do Replay", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform shade = (RectTransform)shadeObject.transform;
        shade.SetParent(root, false);
        shade.anchorMin = Vector2.zero;
        shade.anchorMax = Vector2.one;
        shade.offsetMin = shade.offsetMax = Vector2.zero;
        Image shadeImage = shadeObject.GetComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, 0f);
        shadeImage.raycastTarget = false;
        var ghostObject = new GameObject("Carta Replay", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        RectTransform ghost = (RectTransform)ghostObject.transform;
        ghost.SetParent(root, false);
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.sizeDelta = rect.rect.size;
        ghost.anchoredPosition = start;
        ghost.localScale = rect.localScale;
        Image image = ghostObject.GetComponent<Image>();
        image.sprite = source != null ? source.sprite : null;
        image.color = source != null ? source.color : Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        CanvasGroup ghostGroup = ghostObject.GetComponent<CanvasGroup>();
        CanvasGroup originalGroup = GetComponent<CanvasGroup>();
        if (originalGroup == null) originalGroup = gameObject.AddComponent<CanvasGroup>();
        originalGroup.alpha = 0f;
        rect.anchoredPosition = originalPosition;
        rect.localScale = originalScale;
        rect.localRotation = homeRotation;
        Vector2 workerScreen = Camera.main != null ?
            (Vector2)Camera.main.WorldToScreenPoint(worker.transform.position + Vector3.up) :
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, workerScreen,
            canvas.worldCamera, out Vector2 destination);
        Sequence sequence = DOTween.Sequence();
        sequence.Append(shadeImage.DOFade(0.27f, 0.52f));
        sequence.Append(ghost.DOAnchorPos(Vector2.zero, 0.58f).SetEase(Ease.OutCubic));
        sequence.Join(ghost.DOScale(1.12f, 0.58f).SetEase(Ease.OutBack));
        sequence.Append(ghost.DOPunchRotation(new Vector3(0f, 0f, 10f), 0.88f, 12));
        sequence.AppendCallback(() =>
        {
            bool hasFinishedArt = template != null && template.sprite != null &&
                !template.sprite.name.Contains("9-SlicedBox");
            if (hasFinishedArt) image.sprite = template.sprite;
            else
            {
                image.sprite = template != null && template.sprite != null ?
                    template.sprite : source != null ? source.sprite : null;
                var label = new GameObject("Nome da ação", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Text));
                RectTransform labelRect = (RectTransform)label.transform;
                labelRect.SetParent(ghost, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 8f);
                labelRect.offsetMax = new Vector2(-8f, -8f);
                Text text = label.GetComponent<Text>();
                Text reference = templateCard != null ?
                    templateCard.GetComponentInChildren<Text>(true) : null;
                if (reference == null) reference = GetComponentInChildren<Text>(true);
                if (reference != null) text.font = reference.font;
                text.text = templateCard != null ? templateCard.DisplayName : repeated.ToString();
                text.color = new Color(0.75f, 0.1f, 0.1f);
                text.alignment = TextAnchor.MiddleCenter;
                text.resizeTextForBestFit = true;
                text.raycastTarget = false;
            }
        });
        sequence.Append(ghost.DOPunchScale(Vector3.one * 0.19f, 0.25f, 5));
        sequence.Append(shadeImage.DOFade(0f, 0.28f));
        sequence.Join(ghost.DOAnchorPos(destination, 0.48f).SetEase(Ease.InOutCubic));
        sequence.Join(ghost.DOScale(0.4f, 0.48f).SetEase(Ease.InCubic));
        sequence.Join(ghost.DOLocalRotate(new Vector3(0f, 0f, -9f), 0.48f));
        sequence.Append(ghostGroup.DOFade(0f, 0.17f));
        sequence.OnComplete(() =>
        {
            if (originalGroup != null) originalGroup.alpha = 1f;
            Destroy(ghostObject);
            Destroy(shadeObject);
            replayAnimating = false;
            returning = false;
            onFinished?.Invoke();
        });
    }

    public void FadeOut(System.Action onFinished)
    {
        returning = true;
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        pendingFloat?.Kill();
        if (deliveryWorker != null && !replayAnimating)
        {
            AnimateDelivery(rect, deliveryWorker, onFinished);
            deliveryWorker = null;
            waitingForTarget = false;
            return;
        }
        deliveryWorker = null;
        fadeGraphics = GetComponentsInChildren<Graphic>(true);
        fadeColors = new Color[fadeGraphics.Length];
        for (int i = 0; i < fadeGraphics.Length; i++)
        {
            fadeColors[i] = fadeGraphics[i].color;
            fadeGraphics[i].DOFade(0f, 0.2f);
        }
        rect.DOScale(rect.localScale * 0.88f, 0.2f).SetEase(Ease.InSine)
            .OnComplete(() =>
            {
                returning = false;
                onFinished?.Invoke();
            });
    }

    public void PrepareDelivery(WorkerAssignment worker)
    {
        deliveryWorker = worker;
        pendingFloat?.Kill();
    }

    public void FlyToSelection()
    {
        RectTransform rect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || rect.parent == null) return;
        waitingForTarget = true;
        returning = true;
        rect.DOKill();
        pendingFloat?.Kill();
        rect.localRotation = homeRotation;
        RectTransform parent = (RectTransform)rect.parent;
        Vector2 screen = new Vector2(Screen.width * 0.17f, Screen.height * 0.82f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen,
                canvas.worldCamera, out Vector2 destination)) return;
        rect.DOAnchorPos(destination, 0.42f).SetEase(Ease.OutCubic);
        rect.DOScale(originalScale * 0.72f, 0.42f).SetEase(Ease.OutBack);
        rect.DOLocalRotate(new Vector3(0f, 0f, -8f), 0.42f)
            .OnComplete(() =>
            {
                if (!waitingForTarget) return;
                pendingFloat = rect.DOAnchorPosY(destination.y + 7f, 1.3f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            });
    }

    public void ReturnFromSelection()
    {
        if (!waitingForTarget) return;
        waitingForTarget = false;
        pendingFloat?.Kill();
        AnimateReturn();
    }

    private void AnimateDelivery(RectTransform rect, WorkerAssignment worker,
        System.Action onFinished)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform originalParent = rect.parent as RectTransform;
        Camera destinationCamera = worldCamera != null ? worldCamera : Camera.main;
        if (canvas == null || originalParent == null || destinationCamera == null || worker == null)
        {
            onFinished?.Invoke();
            return;
        }
        bool fromSelection = waitingForTarget;
        Vector3 visualCenter = rect.TransformPoint(rect.rect.center);
        Vector2 originalAnchorMin = rect.anchorMin;
        Vector2 originalAnchorMax = rect.anchorMax;
        RectTransform flightParent = originalParent;
        if (fromSelection && canvas.rootCanvas.transform is RectTransform root)
        {
            rect.SetParent(root, true);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            flightParent = root;
        }
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.position += visualCenter - rect.TransformPoint(rect.rect.center);
        float liftScale = Mathf.Max(rect.localScale.x, 0.1f) * 1.16f;
        Vector2 flightStart = rect.anchoredPosition;
        float flightProgress = 0f;
        Sequence delivery = DOTween.Sequence().SetTarget(rect);
        delivery.Append(rect.DOAnchorPosY(rect.anchoredPosition.y + 28f, 0.18f)
            .SetEase(Ease.OutCubic));
        delivery.Join(rect.DOScale(liftScale, 0.18f)
            .SetEase(Ease.OutCubic));
        delivery.AppendCallback(() => flightStart = rect.anchoredPosition);
        delivery.Append(DOTween.To(() => flightProgress, progress =>
        {
            flightProgress = progress;
            if (worker == null || rect == null) return;
            Vector2 screen = destinationCamera.WorldToScreenPoint(
                worker.transform.position + Vector3.up);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(flightParent,
                    screen, canvas.worldCamera, out Vector2 destination))
                rect.anchoredPosition = Vector2.LerpUnclamped(flightStart, destination, progress);
        }, 1f, 0.47f).SetEase(Ease.InCubic));
        delivery.Join(rect.DOLocalRotate(new Vector3(0f, 0f,
            rect.localEulerAngles.z + 360f), 0.47f, RotateMode.FastBeyond360));
        delivery.Join(rect.DOScale(originalScale * 0.04f, 0.47f).SetEase(Ease.InCubic));
        CanvasGroup fadeGroup = GetComponent<CanvasGroup>();
        if (fadeGroup == null) fadeGroup = gameObject.AddComponent<CanvasGroup>();
        delivery.Append(rect.DOScale(originalScale * 0.14f, 0.08f)
            .SetEase(Ease.OutBack));
        delivery.Join(fadeGroup.DOFade(0f, 0.08f));
        delivery.OnComplete(() =>
        {
            if (fromSelection && rect != null && originalParent != null)
            {
                rect.SetParent(originalParent, false);
                rect.anchorMin = originalAnchorMin;
                rect.anchorMax = originalAnchorMax;
            }
            rect.pivot = homePivot;
            returning = false;
            onFinished?.Invoke();
        });
    }

    public void RestoreAppearance()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
        if (fadeGraphics == null) return;
        for (int i = 0; i < fadeGraphics.Length; i++)
            if (fadeGraphics[i] != null) fadeGraphics[i].color = fadeColors[i];
    }

    public void ConfigureRuntime(Camera camera, DragCursorPreview cursorPreview)
    {
        worldCamera = camera;
        preview = cursorPreview;
    }

    private void OnEnable()
    {
        returning = false;
        waitingForTarget = false;
        if (preview != null) preview.CancelRequested += CancelDrag;
        originalScale = transform.localScale;
        if (activity == ActivityKind.BuySeeds)
        {
            shopClicking = false;
            transform.localScale = Vector3.one;
        }
        homePivot = ((RectTransform)transform).pivot;
    }

    private void OnDisable()
    {
        if (preview != null) preview.CancelRequested -= CancelDrag;
        shopClickTween?.Kill();
        shopHoverTween?.Kill();
        pendingFloat?.Kill();
        if (activity == ActivityKind.BuySeeds)
        {
            shopClicking = false;
            hovered = false;
            transform.localScale = Vector3.one;
        }
        CancelDrag();
        RestoreHover(true);
        ((RectTransform)transform).DOKill();
        returning = false;
        waitingForTarget = false;
    }

    private void Update()
    {
        if (!dragging) return;
        FollowDragPointer();
        float idle = Time.unscaledTime - lastPointerMove;
        float targetAngle = Mathf.Clamp(-horizontalSpeed * 0.018f, -16f, 16f);
        if (idle > 0.07f)
        {
            float elapsed = idle - 0.07f;
            targetAngle = Mathf.Sin(elapsed * 10f) * Mathf.Exp(-elapsed * 3.5f) *
                Mathf.Clamp(-horizontalSpeed * 0.018f, -9f, 9f);
        }
        if (hoveredWorker != null)
            targetAngle = Mathf.Sin(Time.unscaledTime * 48f) * 5f;
        dragAngle = Mathf.SmoothDampAngle(dragAngle, targetAngle, ref angleVelocity,
            hoveredWorker != null ? 0.035f : 0.11f, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.localRotation = homeRotation * Quaternion.Euler(0f, 0f, dragAngle);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragging || hovered || returning) return;
        if (activity != ActivityKind.BuySeeds && Hand != null && Hand.IsSelectionLocked) return;
        hovered = true;
        if (activity == ActivityKind.BuySeeds)
        {
            if (!shopClicking)
            {
                shopHoverTween?.Kill();
                shopHoverTween = transform.DOScale(Vector3.one * 1.1f, 0.16f)
                    .SetEase(Ease.OutSine);
            }
            return;
        }
        originalScale = transform.localScale;
        originalSibling = transform.GetSiblingIndex();
        originalPosition = ((RectTransform)transform).anchoredPosition;
        transform.SetAsLastSibling();
        transform.DOKill();
        transform.DOScale(originalScale * 1.18f, 0.18f).SetEase(Ease.OutCubic);
        ((RectTransform)transform).DOAnchorPosY(originalPosition.y + 30f, 0.18f).SetEase(Ease.OutCubic);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (activity == ActivityKind.BuySeeds)
        {
            hovered = false;
            if (shopClicking) return;
            shopHoverTween?.Kill();
            shopHoverTween = transform.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutSine);
            return;
        }
        RestoreHover(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (activity == ActivityKind.BuySeeds)
        {
            if (shop == null || shopClicking) return;
            shopClicking = true;
            shopHoverTween?.Kill();
            shopClickTween = DOTween.Sequence()
                .Append(transform.DOScale(Vector3.one * 0.93f, 0.08f).SetEase(Ease.InSine))
                .Append(transform.DOScale(Vector3.one * 1.12f, 0.12f).SetEase(Ease.OutBack))
                .Append(transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutSine))
                .OnComplete(() =>
                {
                    shopClicking = false;
                    shop.ToggleShop();
                });
            return;
        }
        if (!eventData.dragging && !dragging && !returning &&
            eventData.button == PointerEventData.InputButton.Left)
            Hand?.InspectCard(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasEffect || activity == ActivityKind.BuySeeds || returning) return;
        if (Hand != null && Hand.IsSelectionLocked) return;
        GetComponent<CardHoverOutline>()?.SetHighlighted(false);
        Hand?.ClosePopupsForDrag();
        RestoreHover(true);
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        originalPosition = rect.anchoredPosition;
        originalScale = rect.localScale;
        homeRotation = rect.localRotation;
        homePivot = rect.pivot;
        Vector3 cardCenter = rect.TransformPoint(rect.rect.center);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.position += cardCenter - rect.TransformPoint(rect.rect.center);
        dragAngle = angleVelocity = horizontalSpeed = 0f;
        previousPointer = eventData.position;
        lastPointerMove = Time.unscaledTime;
        dragSibling = rect.GetSiblingIndex();
        ActivityCardAvailability hand = rect.parent != null ?
            rect.parent.GetComponentInChildren<ActivityCardAvailability>(true) : null;
        handBoundary = hand != null ? hand.DragBoundary : null;
        dragEventCamera = eventData.pressEventCamera;
        dragPointerScreen = eventData.position;
        float pointerDelta = eventData.position.x - previousPointer.x;
        if (Mathf.Abs(pointerDelta) > 0.5f)
        {
            horizontalSpeed = Mathf.Lerp(horizontalSpeed,
                pointerDelta / Mathf.Max(Time.unscaledDeltaTime, 0.01f), 0.4f);
            lastPointerMove = Time.unscaledTime;
        }
        previousPointer = eventData.position;
        dragCenterBlend = 0f;
        miniature = false;
        dragCenterTween?.Kill();
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)rect.parent, eventData.position, eventData.pressEventCamera,
                out Vector3 cursorWorld))
            dragWorldOffset = rect.position - cursorWorld;
        else dragWorldOffset = Vector3.zero;
        rect.SetAsLastSibling();
        dragging = true;
        AudioManager.Play(SoundCue.CardPick);
        cancelled = false;
        OnDrag(eventData);
        UpdateHover(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        dragPointerScreen = eventData.position;
        bool outside = handBoundary == null || !RectTransformUtility.RectangleContainsScreenPoint(
            handBoundary, eventData.position, dragEventCamera);
        if (outside != miniature)
        {
            miniature = outside;
            RectTransform rect = (RectTransform)transform;
            rect.DOKill();
            rect.DOScale(originalScale * (outside ? 0.42f : 1f), 0.16f)
                .SetEase(Ease.OutCubic);
            dragCenterTween?.Kill();
            dragCenterTween = DOTween.To(() => dragCenterBlend,
                value => dragCenterBlend = value, outside ? 1f : 0f, 0.16f)
                .SetEase(Ease.OutCubic);
        }
        FollowDragPointer();
        UpdateHover(eventData.position);
        transform.parent.GetComponentInChildren<ActivityCardAvailability>(true)?
            .UpdateDucked(eventData.position, dragEventCamera);
    }

    private void FollowDragPointer()
    {
        RectTransform rect = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)rect.parent, dragPointerScreen, dragEventCamera,
                out Vector3 cursorWorld)) return;
        Vector3 centerOffset = rect.position - rect.TransformPoint(rect.rect.center);
        rect.position = cursorWorld + Vector3.Lerp(dragWorldOffset, centerOffset,
            dragCenterBlend);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || cancelled)
        {
            ClearDrag();
            return;
        }
        OnDrag(eventData);
        dragging = false;
        transform.localRotation = homeRotation * Quaternion.Euler(0f, 0f, dragAngle);
        dragCenterTween?.Kill();
        UpdateHover(eventData.position);
        WorkerAssignment target = hoveredWorker;
        ClearDrag();
        if (target != null)
        {
            target.TryAssign(activity, activityName, this);
        }
        ActivityCardAvailability hand = transform.parent != null ?
            transform.parent.GetComponentInChildren<ActivityCardAvailability>(true) : null;
        if (!replayAnimating && !returning && gameObject.activeInHierarchy &&
            (hand == null || hand.ContainsCard(this)))
            AnimateReturn();
        hand?.EndExternalDrag();
    }

    public void CancelDrag()
    {
        if (!dragging) return;
        dragging = false;
        cancelled = true;
        dragCenterTween?.Kill();
        ClearDrag();
        if (gameObject.activeInHierarchy) AnimateReturn();
        transform.parent.GetComponentInChildren<ActivityCardAvailability>(true)?.EndExternalDrag();
    }

    private void ClearDrag()
    {
        if (hoveredWorker != null) hoveredWorker.SetHovered(false);
        hoveredWorker = null;
    }

    private void AnimateReturn()
    {
        waitingForTarget = false;
        pendingFloat?.Kill();
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        returning = true;
        AudioManager.Play(SoundCue.CardReturn);
        Vector3 cardCenter = rect.TransformPoint(rect.rect.center);
        rect.pivot = homePivot;
        rect.position += cardCenter - rect.TransformPoint(rect.rect.center);
        Vector2 belowScreen = new Vector2(originalPosition.x,
            -rect.rect.height * Mathf.Max(0.4f, originalScale.y) - 90f);
        DOTween.Sequence().Append(rect.DOAnchorPos(belowScreen, 0.18f).SetEase(Ease.InCubic))
            .AppendCallback(() => rect.localRotation = homeRotation)
            .Append(rect.DOAnchorPos(originalPosition, 0.36f).SetEase(Ease.OutBack))
            .Join(rect.DOScale(originalScale, 0.36f).SetEase(Ease.OutBack))
            .SetTarget(rect)
            .OnComplete(() => RestoreDragSibling(rect))
            .OnKill(() => { if (!waitingForTarget) returning = false; });
    }

    private void RestoreDragSibling(RectTransform rect)
    {
        if (rect != null && rect.parent != null)
            rect.SetSiblingIndex(Mathf.Min(dragSibling, rect.parent.childCount - 1));
        returning = false;
    }

    private void RestoreHover(bool immediate)
    {
        if (!hovered) return;
        hovered = false;
        transform.DOKill();
        RectTransform rect = (RectTransform)transform;
        if (immediate)
        {
            transform.localScale = originalScale;
            rect.anchoredPosition = originalPosition;
        }
        else
        {
            transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutCubic);
            rect.DOAnchorPos(originalPosition, 0.15f).SetEase(Ease.OutCubic);
        }
        transform.SetSiblingIndex(originalSibling);
    }

    private void UpdateHover(Vector2 screenPosition)
    {
        WorkerAssignment target = null;
        if (worldCamera != null)
        {
            Ray ray = worldCamera.ScreenPointToRay(screenPosition);
            foreach (RaycastHit hit in Physics.RaycastAll(ray, 200f))
            {
                target = hit.collider.GetComponentInParent<WorkerAssignment>();
                if (target != null)
                    break;
            }
        }

        if (target == hoveredWorker)
            return;
        if (hoveredWorker != null)
            hoveredWorker.SetHovered(false);
        hoveredWorker = target;
        if (hoveredWorker != null)
            hoveredWorker.SetHovered(true);
    }
}
