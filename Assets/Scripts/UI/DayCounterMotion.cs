using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DayCounterMotion : MonoBehaviour
{
    [SerializeField] private RectTransform label;
    [SerializeField] private Image glow;
    [SerializeField, Min(0f)] private float floatDistance = 2f;
    [SerializeField, Min(0.1f)] private float floatDuration = 1.4f;
    [SerializeField, Min(1f)] private float hoverScale = 1.07f;

    private RectTransform area;
    private Vector2 labelHome;
    private Camera uiCamera;
    private Tween glowMotion;
    private Tween hoverMotion;
    private bool hovering;

    private void Awake()
    {
        area = (RectTransform)transform;
        if (label != null) labelHome = label.anchoredPosition;
        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
    }

    private void OnEnable()
    {
        if (label != null)
            label.DOAnchorPosY(labelHome.y + floatDistance, floatDuration)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        if (glow != null) glow.color = new Color(1f, 1f, 1f, 0f);
    }

    private void Update()
    {
        bool over = Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(
            area, Mouse.current.position.ReadValue(), uiCamera);
        if (over == hovering) return;
        hovering = over;
        hoverMotion?.Kill();
        if (label != null) hoverMotion = label.DOScale(over ? hoverScale : 1f, 0.18f);
        glowMotion?.Kill();
        if (glow == null) return;
        glow.color = new Color(1f, 1f, 1f, 0f);
        if (!over) return;
        glow.rectTransform.localScale = Vector3.one;
        glowMotion = DOTween.Sequence().Append(glow.DOFade(0.3f, 0.12f))
            .Join(glow.rectTransform.DOScale(1.18f, 0.3f))
            .Append(glow.DOFade(0f, 0.25f));
    }

    private void OnDisable()
    {
        glowMotion?.Kill();
        hoverMotion?.Kill();
        if (label != null) { label.DOKill(); label.anchoredPosition = labelHome; }
        if (glow != null) { glow.DOKill(); glow.color = new Color(1f, 1f, 1f, 0f); }
    }
}
