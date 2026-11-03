using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(RectTransform))]
public sealed class ActivityCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string activityName;
    [SerializeField, TextArea(2, 3)] private string description;
    [SerializeField] private ActivityKind activity;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private DragCursorPreview preview;

    private WorkerAssignment hoveredWorker;
    private bool dragging;
    private bool cancelled;
    private int originalSibling;
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private bool hovered;

    public ActivityKind Kind => activity;
    public string DisplayName => activityName;
    public string Description => description;
    public bool HasEffect => activity <= ActivityKind.PrepareFood;

    public void ConfigureRuntime(Camera camera, DragCursorPreview cursorPreview)
    {
        worldCamera = camera;
        preview = cursorPreview;
    }

    private void OnEnable()
    {
        if (preview != null) preview.CancelRequested += CancelDrag;
        originalScale = transform.localScale;
    }

    private void OnDisable()
    {
        if (preview != null) preview.CancelRequested -= CancelDrag;
        CancelDrag();
        RestoreHover(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragging || hovered) return;
        hovered = true;
        originalSibling = transform.GetSiblingIndex();
        originalPosition = ((RectTransform)transform).anchoredPosition;
        transform.SetAsLastSibling();
        transform.DOKill();
        transform.DOScale(originalScale * 1.18f, 0.18f).SetEase(Ease.OutCubic);
        ((RectTransform)transform).DOAnchorPosY(originalPosition.y + 30f, 0.18f).SetEase(Ease.OutCubic);
    }

    public void OnPointerExit(PointerEventData eventData) => RestoreHover(false);

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!HasEffect) return;
        RestoreHover(true);
        dragging = true;
        cancelled = false;
        if (preview != null)
            preview.Show(activityName, eventData.position);
        UpdateHover(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging) return;
        if (preview != null)
            preview.Move(eventData.position);
        UpdateHover(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || cancelled)
        {
            ClearDrag();
            return;
        }
        dragging = false;
        UpdateHover(eventData.position);
        WorkerAssignment target = hoveredWorker;
        ClearDrag();
        RestoreHover(false);
        if (target != null)
            target.TryAssign(activity, activityName, this);
    }

    public void CancelDrag()
    {
        if (!dragging) return;
        dragging = false;
        cancelled = true;
        ClearDrag();
    }

    private void ClearDrag()
    {
        if (preview != null) preview.Hide();
        if (hoveredWorker != null) hoveredWorker.SetHovered(false);
        hoveredWorker = null;
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
