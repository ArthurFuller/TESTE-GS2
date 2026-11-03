using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class FeedingFoodIcon : MonoBehaviour, IPointerClickHandler, IBeginDragHandler,
    IDragHandler, IEndDragHandler
{
    private FeedingMinigame game;
    private RectTransform rect;
    private Vector2 home;
    private bool moving;

    private void Awake()
    {
        rect = (RectTransform)transform;
        home = rect.anchoredPosition;
    }

    public void Configure(FeedingMinigame feedingGame) => game = feedingGame;

    public void ResetPosition(bool fromTop, float delay, float duration)
    {
        rect ??= (RectTransform)transform;
        rect.DOKill();
        moving = false;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = home + (fromTop ? Vector2.up * 110f :
            Vector2.right * (home.x < 0 ? -110f : 110f));
        rect.DOAnchorPos(home, duration).SetDelay(delay).SetEase(Ease.OutBack)
            .OnComplete(() => rect.DOAnchorPosY(home.y + 5f, 0.9f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!moving && !eventData.dragging) game?.FeedFromIcon(this, eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (moving) return;
        rect.DOKill();
        rect.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (moving) return;
        rect.anchoredPosition += eventData.delta / rect.GetComponentInParent<Canvas>().scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (moving) return;
        game?.FeedFromIcon(this, eventData);
        if (!moving) rect.DOAnchorPos(home, 0.2f).SetEase(Ease.OutCubic);
    }

    public void PlayPlop(RectTransform target, Action finished)
    {
        moving = true;
        rect.DOKill();
        Vector3 destination = rect.parent.InverseTransformPoint(target.position);
        DOTween.Sequence().Append(rect.DOLocalMoveY(rect.localPosition.y + 15f, 0.1f))
            .Append(rect.DOLocalMove(destination, 0.2f).SetEase(Ease.InCubic))
            .Join(rect.DOScale(0.2f, 0.2f)).OnComplete(() =>
            {
                moving = false;
                finished?.Invoke();
            });
    }

    public void StopAnimation()
    {
        if (rect != null) rect.DOKill();
    }
}
