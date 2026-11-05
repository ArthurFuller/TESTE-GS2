using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FeedingFoodIcon : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Text nameLabel;
    [SerializeField] private Text quantityLabel;

    private FeedingMinigame game;
    private RectTransform rect;
    private RectTransform draggedUnit;
    private Canvas canvas;
    private Image image;
    private bool reserved;

    public SaleItem Kind { get; private set; }

    private void Awake()
    {
        rect = (RectTransform)transform;
        image = GetComponent<Image>();
    }

    public void Configure(FeedingMinigame feedingGame, SaleItem kind, Sprite sprite, Font font)
    {
        game = feedingGame;
        Kind = kind;
        if (image != null)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : new Color(0.85f, 0.95f, 1f);
            image.preserveAspect = true;
        }
        if (nameLabel != null)
        {
            if (font != null) nameLabel.font = font;
            nameLabel.text = kind == SaleItem.Food ? "PRONTA" :
                kind == SaleItem.Fish ? "PEIXE" : kind == SaleItem.Corn ? "MILHO" :
                kind == SaleItem.Pumpkin ? "MACAXEIRA" :
                kind == SaleItem.Jerimum ? "JERIMUM" :
                kind == SaleItem.Milk ? "LEITE" : "OVOS";
        }
        if (quantityLabel != null && font != null) quantityLabel.font = font;
    }

    public void SetQuantity(int amount)
    {
        if (quantityLabel != null) quantityLabel.text = "× " + amount;
    }

    public void SetHome(Vector2 home, bool enterFromTop, float delay, float duration)
    {
        rect.DOKill();
        if (enterFromTop) rect.anchoredPosition = home + Vector2.up * 110f;
        rect.DOAnchorPos(home, duration).SetDelay(delay).SetEase(Ease.OutBack)
            .OnComplete(() => rect.DOAnchorPosY(home.y + 5f, 0.9f)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (game == null || !game.Reserve(this)) return;
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            game.CancelReservation(this);
            return;
        }
        reserved = true;
        var unit = new GameObject("Unidade de alimento", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        draggedUnit = (RectTransform)unit.transform;
        draggedUnit.SetParent(canvas.transform, false);
        draggedUnit.anchorMin = draggedUnit.anchorMax = new Vector2(0.5f, 0.5f);
        draggedUnit.sizeDelta = new Vector2(55f, 55f);
        Image art = unit.GetComponent<Image>();
        art.sprite = image != null ? image.sprite : null;
        art.color = image != null ? image.color : Color.white;
        art.preserveAspect = true;
        art.raycastTarget = false;
        MoveDraggedUnit(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (reserved) MoveDraggedUnit(eventData);
    }

    private void MoveDraggedUnit(PointerEventData eventData)
    {
        if (draggedUnit == null || canvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform, eventData.position, eventData.pressEventCamera,
            out Vector2 localPoint);
        draggedUnit.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!reserved) return;
        reserved = false;
        RectTransform target = game.FeedFromIcon(this, eventData);
        if (target != null) PlayPlop(target);
        else
        {
            game.CancelReservation(this);
            ReturnUnit();
        }
    }

    private void PlayPlop(RectTransform target)
    {
        if (draggedUnit == null) return;
        RectTransform unit = draggedUnit;
        draggedUnit = null;
        Vector2 destination = canvas.transform.InverseTransformPoint(target.position);
        DOTween.Sequence().Append(unit.DOAnchorPosY(unit.anchoredPosition.y + 15f, 0.1f))
            .Append(unit.DOAnchorPos(destination, 0.2f).SetEase(Ease.InCubic))
            .Join(unit.DOScale(0.2f, 0.2f))
            .OnComplete(() => Destroy(unit.gameObject));
    }

    private void ReturnUnit()
    {
        if (draggedUnit == null) return;
        RectTransform unit = draggedUnit;
        draggedUnit = null;
        Vector2 destination = canvas.transform.InverseTransformPoint(transform.position);
        unit.DOAnchorPos(destination, 0.18f).SetEase(Ease.OutCubic)
            .OnComplete(() => Destroy(unit.gameObject));
    }

    public void StopAnimation()
    {
        if (rect != null) rect.DOKill();
        if (draggedUnit != null)
        {
            draggedUnit.DOKill();
            Destroy(draggedUnit.gameObject);
            draggedUnit = null;
        }
        if (reserved && game != null) game.CancelReservation(this);
        reserved = false;
    }
}
