using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public sealed class ActivityCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private string activityName;
    [SerializeField] private ActivityKind activity;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private DragCursorPreview preview;

    private WorkerAssignment hoveredWorker;
    private bool dragging;
    private bool cancelled;

    public ActivityKind Kind => activity;

    private void OnEnable()
    {
        if (preview != null) preview.CancelRequested += CancelDrag;
    }

    private void OnDisable()
    {
        if (preview != null) preview.CancelRequested -= CancelDrag;
        CancelDrag();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
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
        if (target != null)
            target.TryAssign(activity, activityName);
    }

    private void CancelDrag()
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
