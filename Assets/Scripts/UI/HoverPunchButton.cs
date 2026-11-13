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
    [SerializeField] private bool tintOnHover = true;
    [SerializeField] private bool outlineOnHover = true;

    private Button button;
    private Image image;
    private Outline outline;
    private RectTransform rect;
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private Quaternion originalRotation;
    private Color originalColor;
    private bool hovered;
    private bool pressedEligible;
    private bool animating;
    private int animationStartedFrame = -1;
    public bool IsAnimating => animating;
    public bool BlocksNewClicks => animating && animationStartedFrame != Time.frameCount;

    public void UseScaleOnly()
    {
        tintOnHover = false;
        outlineOnHover = false;
        if (outline != null) outline.enabled = false;
        if (image != null) image.color = originalColor;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        originalScale = rect.localScale;
        originalPosition = rect.anchoredPosition;
        originalRotation = rect.localRotation;
        if (image != null) originalColor = image.color;
        outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.78f, 0.32f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.enabled = false;
    }

    private void OnDisable()
    {
        if (rect != null)
        {
            rect.DOKill();
            rect.localScale = originalScale;
            rect.anchoredPosition = originalPosition;
            rect.localRotation = originalRotation;
        }
        if (image != null)
        {
            image.DOKill();
            image.color = originalColor;
        }
        hovered = false;
        pressedEligible = false;
        animating = false;
        if (outline != null) outline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.interactable) return;
        hovered = true;
        if (outline != null) outline.enabled = outlineOnHover;
        if (animating) return;
        rect.DOKill();
        rect.DOScale(originalScale * hoverScale, duration).SetEase(Ease.OutBack).SetUpdate(true);
        if (tintOnHover && image != null)
            image.DOColor(new Color(1f, 0.96f, 0.76f, originalColor.a), duration).SetUpdate(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (outline != null) outline.enabled = false;
        if (animating) return;
        rect.DOKill();
        rect.DOScale(originalScale, duration).SetEase(Ease.OutCubic).SetUpdate(true);
        if (image != null) image.DOColor(originalColor, duration).SetUpdate(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!pressedEligible || animating) return;
        pressedEligible = false;
        AudioManager.Play(SoundCue.UiClick);
        animating = true;
        animationStartedFrame = Time.frameCount;
        rect.DOKill();
        DOTween.Sequence().SetTarget(rect).SetUpdate(true)
            .Append(rect.DOScale(originalScale * pressedScale, 0.08f))
            .Append(rect.DOScale(originalScale * (hovered ? hoverScale : 1f), duration)
                .SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                rect.localScale = originalScale * (hovered ? hoverScale : 1f);
                if (image != null) image.color = tintOnHover && hovered ?
                    new Color(1f, 0.96f, 0.76f, originalColor.a) : originalColor;
                animating = false;
            })
            .OnKill(() => animating = false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressedEligible = !animating && button != null && button.interactable;
    }

    public void ShakeRejected()
    {
        if (rect == null || BlocksNewClicks) return;
        AudioManager.Play(SoundCue.UiRejected);
        pressedEligible = false;
        rect.DOKill();
        animating = true;
        animationStartedFrame = Time.frameCount;
        rect.localScale = originalScale * (hovered ? hoverScale : 1f);
        rect.anchoredPosition = originalPosition;
        rect.localRotation = originalRotation;
        rect.DOShakeRotation(0.32f, new Vector3(0f, 0f, 8f), 12, 90f).SetUpdate(true)
            .OnComplete(() =>
            {
                rect.localRotation = originalRotation;
                rect.localScale = originalScale * (hovered ? hoverScale : 1f);
                if (image != null) image.color = tintOnHover && hovered ?
                    new Color(1f, 0.96f, 0.76f, originalColor.a) : originalColor;
                animating = false;
            })
            .OnKill(() =>
            {
                rect.localRotation = originalRotation;
                animating = false;
            });
    }
}
