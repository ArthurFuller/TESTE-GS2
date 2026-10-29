using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class EventCardView : MonoBehaviour
{
    [SerializeField] private CanvasGroup backdrop;
    [SerializeField] private RectTransform card;
    [SerializeField] private Text titleText;
    [SerializeField] private LetterJumpText descriptionText;
    [SerializeField] private CanvasGroup descriptionGroup;
    [Header("Animação configurável")]
    [SerializeField, Min(0)] private float entranceDuration = 0.6f;
    [SerializeField, Min(0)] private float flipDuration = 0.35f;
    [SerializeField, Min(0)] private float textDuration = 1.2f;
    [SerializeField] private float entranceOffset = -650f;
    [SerializeField] private float swingDegrees = 7f;
    [SerializeField, Min(0)] private float descriptionSlideOffset = 35f;
    [SerializeField] private Color backColor = new Color(0.72f, 0.60f, 0.43f, 0.95f);
    private Vector2 homePosition;
    private Vector2 descriptionHomePosition;
    private RectTransform descriptionRect;
    private Image cardImage;
    private Color frontColor;
    private string pendingDescription;
    private bool flipped;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (card != null) homePosition = card.anchoredPosition;
        if (card != null) cardImage = card.GetComponent<Image>();
        if (cardImage != null) frontColor = cardImage.color;
        if (descriptionGroup != null)
        {
            descriptionRect = descriptionGroup.GetComponent<RectTransform>();
            if (descriptionRect != null) descriptionHomePosition = descriptionRect.anchoredPosition;
        }
        HideImmediate();
    }

    public void Show(string title, string description)
    {
        if (card == null || backdrop == null) return;
        IsOpen = true;
        pendingDescription = description;
        flipped = false;
        card.DOKill();
        backdrop.DOKill();
        if (descriptionGroup != null) descriptionGroup.DOKill();
        if (descriptionRect != null) descriptionRect.DOKill();
        if (cardImage != null) cardImage.color = frontColor;
        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (descriptionGroup != null)
        {
            descriptionGroup.alpha = 0;
            descriptionGroup.blocksRaycasts = false;
        }
        if (descriptionRect != null)
            descriptionRect.anchoredPosition = descriptionHomePosition + Vector2.left * descriptionSlideOffset;
        backdrop.gameObject.SetActive(true);
        backdrop.alpha = 0;
        backdrop.blocksRaycasts = true;
        card.anchoredPosition = homePosition + Vector2.up * entranceOffset;
        card.localRotation = Quaternion.identity;
        backdrop.DOFade(0.65f, entranceDuration);
        card.DOAnchorPos(homePosition, entranceDuration).SetEase(Ease.OutCubic)
            .OnComplete(() => card.DORotate(new Vector3(0, 0, swingDegrees), 0.12f)
                .SetLoops(2, LoopType.Yoyo).OnComplete(() => card.localRotation = Quaternion.identity));
    }

    public void Flip()
    {
        if (!IsOpen || flipped || card == null || descriptionGroup == null) return;
        flipped = true;
        card.DOKill();
        card.DOScaleX(0, flipDuration * 0.5f).OnComplete(() =>
        {
            if (cardImage != null) cardImage.color = backColor;
            if (titleText != null) titleText.text = "DETALHES";
            if (descriptionText != null)
            {
                descriptionText.Reveal(pendingDescription, textDuration);
            }
            descriptionGroup.blocksRaycasts = true;
            descriptionGroup.DOFade(1, flipDuration);
            if (descriptionRect != null)
                descriptionRect.DOAnchorPos(descriptionHomePosition, flipDuration).SetEase(Ease.OutCubic);
            card.DOScaleX(1, flipDuration * 0.5f);
        });
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (descriptionText != null) descriptionText.DOKill();
        if (descriptionGroup != null) descriptionGroup.DOKill();
        if (descriptionRect != null) descriptionRect.DOKill();
        if (card != null) card.DOKill();
        if (backdrop != null) backdrop.DOFade(0, 0.2f).OnComplete(HideImmediate);
    }

    private void HideImmediate()
    {
        if (descriptionGroup != null)
        {
            descriptionGroup.alpha = 0;
            descriptionGroup.blocksRaycasts = false;
        }
        if (descriptionRect != null) descriptionRect.anchoredPosition = descriptionHomePosition;
        if (backdrop != null)
        {
            backdrop.alpha = 0;
            backdrop.blocksRaycasts = false;
            backdrop.gameObject.SetActive(false);
        }
        if (card != null) card.localScale = Vector3.one;
        if (cardImage != null) cardImage.color = frontColor;
        IsOpen = false;
    }
}
