using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class HoverPunchButton : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    [SerializeField, Min(1f)] private float hoverScale = 1.08f;
    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.9f;
    [SerializeField, Min(0.05f)] private float duration = 0.16f;

    private Button button;
    private Image image;
    private RectTransform rect;
    private Vector3 originalScale;
    private Color originalColor;
    private bool hovered;
    private bool pressedEligible;

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        originalScale = rect.localScale;
        if (image != null) originalColor = image.color;
    }

    private void OnDisable()
    {
        if (rect != null)
        {
            rect.DOKill();
            rect.localScale = originalScale;
        }
        if (image != null)
        {
            image.DOKill();
            image.color = originalColor;
        }
        hovered = false;
        pressedEligible = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.interactable) return;
        hovered = true;
        rect.DOKill();
        rect.DOScale(originalScale * hoverScale, duration).SetEase(Ease.OutBack);
        if (image != null)
            image.DOColor(new Color(1f, 0.96f, 0.76f, originalColor.a), duration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        rect.DOKill();
        rect.DOScale(originalScale, duration).SetEase(Ease.OutCubic);
        if (image != null) image.DOColor(originalColor, duration);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!pressedEligible) return;
        pressedEligible = false;
        rect.DOKill();
        DOTween.Sequence().SetTarget(rect)
            .Append(rect.DOScale(originalScale * pressedScale, 0.08f))
            .Append(rect.DOScale(originalScale * (hovered ? hoverScale : 1f), duration)
                .SetEase(Ease.OutBack));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressedEligible = button != null && button.interactable;
    }
}
