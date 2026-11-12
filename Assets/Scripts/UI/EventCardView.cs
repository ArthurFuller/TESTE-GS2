using System;
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
    [SerializeField] private Button revealButton;
    [SerializeField] private Button skipButton;
    [Header("Animação configurável")]
    [SerializeField, Min(0)] private float entranceDuration = 0.6f;
    [SerializeField, Min(0)] private float flipDuration = 0.35f;
    [SerializeField, Min(0)] private float textDuration = 1.2f;
    [SerializeField] private float entranceOffset = -650f;
    [SerializeField] private float swingDegrees = 7f;
    [SerializeField, Min(0)] private float descriptionSlideOffset = 35f;
    [SerializeField, Min(0)] private float revealedGap = 0f;
    [SerializeField] private Vector2 illustratedCardSize = new Vector2(360f, 360f);
    private Vector2 homePosition;
    private Vector2 descriptionHomePosition;
    private Vector2 revealedCardPosition;
    private Vector2 originalCardSize;
    private Vector2 originalRevealPosition;
    private RectTransform descriptionRect;
    private Image cardImage;
    private Color frontColor;
    private Sprite originalSprite;
    private bool originalPreserveAspect;
    private string pendingDescription;
    private bool flipped;
    private Sequence revealMotion;
    public bool IsOpen { get; private set; }
    public event Action Closed;

    private void Awake()
    {
        if (card != null)
        {
            originalCardSize = card.sizeDelta;
            cardImage = card.GetComponent<Image>();
            if (revealButton == null) revealButton = card.GetComponentInChildren<Button>(true);
            if (revealButton != null)
                originalRevealPosition = ((RectTransform)revealButton.transform).anchoredPosition;
        }
        if (cardImage != null)
        {
            frontColor = cardImage.color;
            originalSprite = cardImage.sprite;
            originalPreserveAspect = cardImage.preserveAspect;
        }
        if (descriptionGroup != null)
            descriptionRect = descriptionGroup.GetComponent<RectTransform>();
        UpdatePositions();
        HideImmediate();
    }

    private static Vector2 CenteredPosition(RectTransform rect) => new Vector2(
        (rect.pivot.x - 0.5f) * rect.rect.width,
        (rect.pivot.y - 0.5f) * rect.rect.height);

    private void UpdatePositions()
    {
        if (card == null) return;
        homePosition = CenteredPosition(card);
        if (revealButton != null)
        {
            RectTransform button = (RectTransform)revealButton.transform;
            button.anchoredPosition = originalRevealPosition + new Vector2(
                (0.5f - button.anchorMin.x) * (card.rect.width - originalCardSize.x),
                -(1f - button.anchorMin.y) * (card.rect.height - originalCardSize.y));
        }
        if (descriptionRect == null) return;
        float left = -(card.rect.width + revealedGap + descriptionRect.rect.width) * 0.5f;
        revealedCardPosition = new Vector2(left + card.pivot.x * card.rect.width,
            homePosition.y);
        descriptionHomePosition = new Vector2(
            left + card.rect.width + revealedGap +
                descriptionRect.pivot.x * descriptionRect.rect.width,
            (descriptionRect.pivot.y - 0.5f) * descriptionRect.rect.height);
    }

    public void Show(string title, string description, Sprite art = null)
    {
        if (card == null || backdrop == null) return;
        IsOpen = true;
        pendingDescription = description;
        flipped = false;
        revealMotion?.Kill();
        card.DOKill();
        backdrop.DOKill();
        if (descriptionGroup != null) descriptionGroup.DOKill();
        if (descriptionRect != null) descriptionRect.DOKill();
        card.sizeDelta = art != null ? illustratedCardSize : originalCardSize;
        UpdatePositions();
        if (cardImage != null)
        {
            cardImage.sprite = art != null ? art : originalSprite;
            cardImage.color = art != null ? Color.white : frontColor;
            cardImage.preserveAspect = art != null || originalPreserveAspect;
        }
        if (titleText != null)
        {
            titleText.text = title;
            titleText.transform.localRotation = Quaternion.identity;
            titleText.gameObject.SetActive(art == null);
        }
        if (revealButton != null)
        {
            revealButton.gameObject.SetActive(true);
            revealButton.interactable = true;
        }
        if (skipButton != null) skipButton.gameObject.SetActive(true);
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (descriptionGroup != null)
        {
            descriptionGroup.alpha = 0;
            descriptionGroup.blocksRaycasts = false;
            descriptionGroup.interactable = false;
        }
        if (descriptionRect != null)
            descriptionRect.anchoredPosition = descriptionHomePosition + Vector2.left * descriptionSlideOffset;
        backdrop.gameObject.SetActive(true);
        AudioManager.Play(SoundCue.EventReveal);
        backdrop.transform.SetAsLastSibling();
        backdrop.alpha = 0;
        backdrop.blocksRaycasts = true;
        card.anchoredPosition = homePosition + Vector2.up * entranceOffset;
        card.localScale = Vector3.one;
        card.localRotation = Quaternion.identity;
        // O CanvasGroup também afeta os filhos: o card precisa chegar a alpha 1.
        backdrop.DOFade(1f, entranceDuration);
        card.DOAnchorPos(homePosition, entranceDuration).SetEase(Ease.OutCubic)
            .OnComplete(() => card.DORotate(new Vector3(0, 0, swingDegrees), 0.12f)
                .SetLoops(2, LoopType.Yoyo).OnComplete(() => card.localRotation = Quaternion.identity));
    }

    public void Flip()
    {
        if (!IsOpen || flipped || card == null || descriptionGroup == null) return;
        flipped = true;
        card.DOKill();
        if (revealButton != null)
        {
            revealButton.interactable = false;
            revealButton.gameObject.SetActive(false);
        }
        if (skipButton != null) skipButton.gameObject.SetActive(false);
        float duration = Mathf.Max(0.15f, flipDuration);
        revealMotion = DOTween.Sequence().SetTarget(this);
        revealMotion.Append(card.DOAnchorPos(revealedCardPosition, duration)
            .SetEase(Ease.InOutCubic));
        if (descriptionRect != null)
            revealMotion.Insert(0f,
                descriptionRect.DOAnchorPos(descriptionHomePosition, duration)
                    .SetEase(Ease.OutCubic));
        revealMotion.Insert(0f, descriptionGroup.DOFade(1f, duration));
        revealMotion.InsertCallback(duration * 0.4f, () =>
        {
            if (descriptionText != null)
                descriptionText.Reveal(pendingDescription, textDuration);
            descriptionGroup.blocksRaycasts = true;
            descriptionGroup.interactable = true;
        });
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        revealMotion?.Kill();
        if (descriptionText != null) descriptionText.DOKill();
        if (descriptionGroup != null) descriptionGroup.DOKill();
        if (descriptionRect != null) descriptionRect.DOKill();
        if (card != null) card.DOKill();
        if (backdrop != null) backdrop.DOFade(0, 0.2f).OnComplete(() =>
        {
            HideImmediate();
            Closed?.Invoke();
        });
    }

    private void HideImmediate()
    {
        if (descriptionGroup != null)
        {
            descriptionGroup.alpha = 0;
            descriptionGroup.blocksRaycasts = false;
            descriptionGroup.interactable = false;
        }
        if (descriptionRect != null) descriptionRect.anchoredPosition = descriptionHomePosition;
        if (backdrop != null)
        {
            backdrop.alpha = 0;
            backdrop.blocksRaycasts = false;
            backdrop.gameObject.SetActive(false);
        }
        if (card != null)
        {
            card.sizeDelta = originalCardSize;
            card.anchoredPosition = CenteredPosition(card);
            card.localScale = Vector3.one;
            card.localRotation = Quaternion.identity;
        }
        if (revealButton != null)
            ((RectTransform)revealButton.transform).anchoredPosition = originalRevealPosition;
        if (cardImage != null)
        {
            cardImage.sprite = originalSprite;
            cardImage.color = frontColor;
            cardImage.preserveAspect = originalPreserveAspect;
        }
        if (titleText != null) titleText.transform.localRotation = Quaternion.identity;
        if (revealButton != null) revealButton.gameObject.SetActive(true);
        if (skipButton != null) skipButton.gameObject.SetActive(true);
        IsOpen = false;
    }
}
