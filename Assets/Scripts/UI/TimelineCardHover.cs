using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TimelineCardHover : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler, IPointerClickHandler
{
    private RectTransform rect;
    private float restingAngle;
    private float hoverScale;
    private float restingY;
    private int restingSibling;
    private Action<RectTransform> inspect;
    private CardHoverOutline outline;

    public void Setup(float angle, float scale, float homeY,
        Action<RectTransform> onInspect)
    {
        rect = (RectTransform)transform;
        restingAngle = angle;
        hoverScale = scale;
        restingY = homeY;
        restingSibling = transform.GetSiblingIndex();
        inspect = onInspect;
        outline = CardHoverOutline.Ensure(rect);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!Application.isPlaying) return;
        restingSibling = transform.GetSiblingIndex();
        transform.SetAsLastSibling();
        outline?.SetHighlighted(true);
        rect.DOKill();
        rect.DOScale(hoverScale, 0.18f).SetEase(Ease.OutCubic);
        rect.DOAnchorPosY(restingY + 12f, 0.18f).SetEase(Ease.OutCubic);
        rect.DOLocalRotate(Vector3.zero, 0.18f).SetEase(Ease.OutCubic);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!Application.isPlaying) return;
        outline?.SetHighlighted(false);
        rect.DOKill();
        rect.DOScale(1f, 0.18f).SetEase(Ease.OutCubic);
        rect.DOAnchorPosY(restingY, 0.18f).SetEase(Ease.OutCubic);
        rect.DOLocalRotate(Vector3.forward * restingAngle, 0.18f)
            .SetEase(Ease.OutCubic);
        transform.SetSiblingIndex(restingSibling);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Application.isPlaying && eventData.button == PointerEventData.InputButton.Left)
            inspect?.Invoke(rect);
    }
}
