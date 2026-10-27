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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (preview != null)
            preview.Show(activityName, eventData.position);
        UpdateHover(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (preview != null)
            preview.Move(eventData.position);
        UpdateHover(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        UpdateHover(eventData.position);
        WorkerAssignment target = hoveredWorker;
        if (preview != null)
            preview.Hide();
        if (hoveredWorker != null)
            hoveredWorker.SetHovered(false);
        hoveredWorker = null;
        if (target != null)
            target.TryAssign(activity, activityName);
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
