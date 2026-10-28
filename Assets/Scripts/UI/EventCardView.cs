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
    private Vector2 homePosition;
    private string pendingDescription;
    private bool flipped;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (card != null) homePosition = card.anchoredPosition;
        HideImmediate();
    }

    public void Show(string title, string description)
    {
        if (card == null || backdrop == null) return;
        IsOpen = true;
        pendingDescription = description;
        flipped = false;
        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (descriptionGroup != null) descriptionGroup.alpha = 0;
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
            if (descriptionText != null)
            {
                descriptionText.Reveal(pendingDescription, textDuration);
            }
            descriptionGroup.DOFade(1, flipDuration);
            card.DOScaleX(1, flipDuration * 0.5f);
        });
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (descriptionText != null) descriptionText.DOKill();
        if (backdrop != null) backdrop.DOFade(0, 0.2f).OnComplete(HideImmediate);
    }

    private void HideImmediate()
    {
        if (backdrop != null)
        {
            backdrop.alpha = 0;
            backdrop.blocksRaycasts = false;
            backdrop.gameObject.SetActive(false);
        }
        if (card != null) card.localScale = Vector3.one;
        IsOpen = false;
    }
}
