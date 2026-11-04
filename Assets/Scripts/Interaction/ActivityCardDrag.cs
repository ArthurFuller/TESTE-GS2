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

    public ActivityKind Kind => activity;
    public string DisplayName => activityName;
    public string Description => description;
    public bool HasEffect => true;
    public bool IsReturning => returning;

    public void ConfigureRuntime(Camera camera, DragCursorPreview cursorPreview)
    {
        worldCamera = camera;
        preview = cursorPreview;
    }

    private void OnEnable()
    {
        if (preview != null) preview.CancelRequested += CancelDrag;
        originalScale = transform.localScale;
        if (activity == ActivityKind.BuySeeds)
        {
            shopClicking = false;
            transform.localScale = Vector3.one;
        }
    }

    private void OnDisable()
    {
        if (preview != null) preview.CancelRequested -= CancelDrag;
        shopClickTween?.Kill();
        shopHoverTween?.Kill();
        if (activity == ActivityKind.BuySeeds)
        {
            shopClicking = false;
            hovered = false;
            transform.localScale = Vector3.one;
        }
        CancelDrag();
        RestoreHover(true);
    }

    private void Update()
    {
        if (dragging) FollowDragPointer();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragging || hovered || returning) return;
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
        if (eventData.dragging || activity != ActivityKind.BuildHouse) return;
        WorkerAssignment worker = WorkerAssignment.Selected;
        if (worker != null) worker.TryAssign(activity, activityName, this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasEffect || activity == ActivityKind.BuySeeds || returning) return;
        RestoreHover(true);
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        originalPosition = rect.anchoredPosition;
        originalScale = rect.localScale;
        homeRotation = rect.localRotation;
        dragSibling = rect.GetSiblingIndex();
        ActivityCardAvailability hand = rect.parent != null ?
            rect.parent.GetComponentInChildren<ActivityCardAvailability>(true) : null;
        handBoundary = hand != null ? hand.DragBoundary : null;
        dragEventCamera = eventData.pressEventCamera;
        dragPointerScreen = eventData.position;
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
            float angle = homeRotation.eulerAngles.z + (outside ? -8f : 0f);
            rect.DOLocalRotate(new Vector3(0f, 0f, angle), 0.16f)
                .SetEase(Ease.OutCubic);
            dragCenterTween?.Kill();
            dragCenterTween = DOTween.To(() => dragCenterBlend,
                value => dragCenterBlend = value, outside ? 1f : 0f, 0.16f)
                .SetEase(Ease.OutCubic);
        }
        FollowDragPointer();
        UpdateHover(eventData.position);
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
        dragCenterTween?.Kill();
        UpdateHover(eventData.position);
        WorkerAssignment target = hoveredWorker;
        ClearDrag();
        int beforeAssignment = target != null ? target.AssignmentVersion : 0;
        if (target != null)
            target.TryAssign(activity, activityName, this);
        bool accepted = target != null && target.AssignmentVersion != beforeAssignment;
        ActivityCardAvailability hand = transform.parent != null ?
            transform.parent.GetComponentInChildren<ActivityCardAvailability>(true) : null;
        if (gameObject.activeInHierarchy && (hand == null || hand.ContainsCard(this)))
            AnimateReturn(accepted && activity != ActivityKind.BuildHouse &&
                activity != ActivityKind.BuildFence);
    }

    public void CancelDrag()
    {
        if (!dragging) return;
        dragging = false;
        cancelled = true;
        dragCenterTween?.Kill();
        ClearDrag();
        if (gameObject.activeInHierarchy) AnimateReturn(false);
    }

    private void ClearDrag()
    {
        if (hoveredWorker != null) hoveredWorker.SetHovered(false);
        hoveredWorker = null;
    }

    private void AnimateReturn(bool used)
    {
        returning = true;
        RectTransform rect = (RectTransform)transform;
        rect.DOKill();
        Vector3 homeAngles = homeRotation.eulerAngles;
        if (!used)
        {
            DOTween.Sequence().Append(rect.DOAnchorPos(originalPosition, 0.38f).SetEase(Ease.OutBack))
                .Join(rect.DOScale(originalScale, 0.38f).SetEase(Ease.OutBack))
                .Join(rect.DOLocalRotate(new Vector3(0f, 0f, homeAngles.z + 360f), 0.38f,
                    RotateMode.FastBeyond360))
                .OnComplete(() => RestoreDragSibling(rect));
            return;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        var colors = new Color[graphics.Length];
        var originalMaterials = new Material[graphics.Length];
        var dissolveMaterials = new Material[graphics.Length];
        Shader dissolve = Shader.Find("UI/CardDissolve");
        for (int i = 0; i < graphics.Length; i++)
        {
            colors[i] = graphics[i].color;
            originalMaterials[i] = graphics[i].material;
            if (dissolve != null)
            {
                Material material = new Material(dissolve);
                dissolveMaterials[i] = material;
                graphics[i].material = material;
                material.DOFloat(1f, "_Cutoff", 0.2f);
            }
            else graphics[i].DOFade(0f, 0.18f);
        }
        DOTween.Sequence().Append(rect.DOScale(originalScale * 0.08f, 0.2f))
            .Join(rect.DOLocalRotate(new Vector3(0f, 0f, homeAngles.z + 20f), 0.2f))
            .OnComplete(() =>
            {
                if (rect == null) return;
                rect.anchoredPosition = originalPosition + Vector2.down * 95f;
                rect.localScale = originalScale * 0.55f;
                rect.localRotation = Quaternion.Euler(0f, 0f, homeAngles.z - 8f);
                for (int i = 0; i < graphics.Length; i++)
                    if (graphics[i] != null)
                    {
                        graphics[i].material = originalMaterials[i];
                        if (dissolveMaterials[i] != null) Destroy(dissolveMaterials[i]);
                        graphics[i].color = new Color(colors[i].r, colors[i].g, colors[i].b, 0f);
                        graphics[i].DOFade(colors[i].a, 0.32f);
                    }
                DOTween.Sequence().Append(rect.DOAnchorPos(originalPosition, 0.34f).SetEase(Ease.OutBack))
                    .Join(rect.DOScale(originalScale, 0.34f).SetEase(Ease.OutBack))
                    .Join(rect.DOLocalRotate(homeAngles, 0.34f))
                    .OnComplete(() =>
                    {
                        RestoreDragSibling(rect);
                        rect.DOPunchScale(Vector3.one * 0.08f, 0.22f, 1);
                    });
            });
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
