using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TimelineCardInspection : MonoBehaviour
{
    private float maxTilt;
    private float maxShift;
    private float followSpeed;

    private RectTransform rect;
    private Canvas canvas;
    private bool active;
    private Vector2 center;

    public void Configure(float tilt, float shift, float speed)
    {
        maxTilt = tilt;
        maxShift = shift;
        followSpeed = speed;
    }

    public void Begin(Canvas owner)
    {
        rect = (RectTransform)transform;
        canvas = owner;
        center = rect.anchoredPosition;
        active = true;
    }

    public void Stop() => active = false;

    private void Update()
    {
        if (!active || rect == null || canvas == null || Mouse.current == null) return;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ?
            null : canvas.worldCamera;
        Quaternion target = Quaternion.identity;
        Vector2 destination = center;
        Vector2 pointer = Mouse.current.position.ReadValue();
        if (RectTransformUtility.RectangleContainsScreenPoint(rect,
            pointer, camera) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,
                pointer, camera, out Vector2 local))
        {
            float x = Mathf.Clamp(local.x / (rect.rect.width * 0.5f), -1f, 1f);
            float y = Mathf.Clamp(local.y / (rect.rect.height * 0.5f), -1f, 1f);
            target = Quaternion.Euler(-y * maxTilt, x * maxTilt, -x * maxTilt * 0.2f);
            destination += new Vector2(x, y) * maxShift;
        }
        float follow = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        rect.localRotation = Quaternion.Slerp(rect.localRotation, target, follow);
        rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition,
            destination, follow);
    }
}
