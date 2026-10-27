using UnityEngine;

public sealed class WorkProgress : MonoBehaviour
{
    [SerializeField] private GameObject barRoot;
    [SerializeField] private Transform fill;
    [SerializeField, Min(0.01f)] private float fillWidth = 0.84f;

    public void Show()
    {
        if (barRoot != null)
            barRoot.SetActive(true);
        SetProgress(0);
    }

    public void SetProgress(float progress)
    {
        if (fill == null)
            return;
        float width = fillWidth * Mathf.Clamp01(progress);
        Vector3 size = fill.localScale;
        size.x = Mathf.Max(0.001f, width);
        fill.localScale = size;
        Vector3 position = fill.localPosition;
        position.x = -fillWidth * 0.5f + width * 0.5f;
        fill.localPosition = position;
    }

    public void Hide()
    {
        if (barRoot != null)
            barRoot.SetActive(false);
    }
}
