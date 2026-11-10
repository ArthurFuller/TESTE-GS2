using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class DayCounterMotion : MonoBehaviour
{
    [SerializeField] private RectTransform label;
    [SerializeField, Tooltip("Imagem visível do contador, sem margens transparentes.")]
    private RectTransform hitArea;
    [SerializeField] private Image glow;
    [SerializeField, Min(1f)] private float hoverScale = 1.07f;

    private Camera uiCamera;
    private Vector3 labelHomeScale;
    private Tween glowMotion;
    private Tween hoverMotion;
    private bool hovering;

    private void Awake()
    {
        if (label != null) labelHomeScale = label.localScale;
        if (glow != null) glow.raycastTarget = false;
        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
    }

    private void OnEnable()
    {
        hovering = false;
        if (label != null) label.localScale = labelHomeScale;
        if (glow != null)
        {
            Color color = glow.color;
            color.a = 0f;
            glow.color = color;
        }
    }

    private void Update()
    {
        // The parent RectTransform includes large empty areas. Only the visible
        // artwork should activate the hover, not the entire HUD container.
        bool over = hitArea != null && Mouse.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                hitArea, Mouse.current.position.ReadValue(), uiCamera);
        if (over == hovering) return;
        hovering = over;

        hoverMotion?.Kill();
        if (label != null)
            hoverMotion = label.DOScale(labelHomeScale * (over ? hoverScale : 1f), 0.18f)
                .SetEase(Ease.OutCubic).SetUpdate(true);

        glowMotion?.Kill();
        if (glow != null)
            glowMotion = glow.DOFade(over ? 0.9f : 0f, over ? 0.18f : 0.22f)
                .SetEase(Ease.OutSine).SetUpdate(true);
    }

    private void OnDisable()
    {
        hoverMotion?.Kill();
        glowMotion?.Kill();
        if (label != null) label.localScale = labelHomeScale;
        if (glow != null)
        {
            Color color = glow.color;
            color.a = 0f;
            glow.color = color;
        }
        hovering = false;
    }
}
