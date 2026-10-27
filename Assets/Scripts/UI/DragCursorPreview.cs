using UnityEngine;
using UnityEngine.UI;

public sealed class DragCursorPreview : MonoBehaviour
{
    public event System.Action CancelRequested;
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform previewRect;
    [SerializeField] private CanvasGroup previewGroup;
    [SerializeField] private Text activityText;
    [SerializeField] private Vector2 cursorOffset = new Vector2(18, -18);

    private void Awake()
    {
        Hide();
    }

    public void Show(string activityName, Vector2 screenPosition)
    {
        if (activityText != null)
            activityText.text = activityName;
        if (previewGroup != null)
            previewGroup.alpha = 1;
        Move(screenPosition);
    }

    public void Move(Vector2 screenPosition)
    {
        if (canvas == null || previewRect == null)
            return;
        var canvasRect = (RectTransform)canvas.transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint))
            previewRect.anchoredPosition = localPoint + cursorOffset;
    }

    public void Hide()
    {
        if (previewGroup != null)
            previewGroup.alpha = 0;
    }

    public void CancelCurrentDrag()
    {
        CancelRequested?.Invoke();
        Hide();
    }
}
