using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameFeedback : MonoBehaviour
{
    [SerializeField] private Text messageText;
    [SerializeField] private CanvasGroup summaryGroup;
    [SerializeField] private RectTransform summaryPanel;
    [SerializeField] private Text summaryText;
    [SerializeField, Min(0)] private float summaryFadeDuration = 0.25f;
    [SerializeField, Min(0.05f)] private float summaryTransitionDuration = 0.62f;
    [SerializeField, Min(0f)] private float summarySlideDistance = 80f;
    [SerializeField, Min(0.01f)] private float summaryLineInterval = 0.18f;
    [SerializeField, Min(0)] private float feedbackPunchDuration = 0.2f;
    [SerializeField, Min(0)] private float feedbackPunchScale = 0.08f;

    private Sequence summaryRevealSequence;
    private Vector2 summaryHome;
    private Image summaryBackdrop;
    private Color summaryBackdropColor;
    public bool IsSummaryTransitioning { get; private set; }

    private void Awake()
    {
        if (summaryPanel != null) summaryHome = summaryPanel.anchoredPosition;
        if (summaryGroup != null)
        {
            summaryBackdrop = summaryGroup.GetComponent<Image>();
            if (summaryBackdrop != null) summaryBackdropColor = summaryBackdrop.color;
            summaryGroup.alpha = 0;
            summaryGroup.blocksRaycasts = false;
            summaryGroup.interactable = false;
        }
    }

    public void ShowMessage(string message)
    {
        if (messageText == null)
            return;
        messageText.text = message;
        var rect = messageText.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOPunchScale(Vector3.one * feedbackPunchScale, feedbackPunchDuration);
    }

    public void ShowSummary(string summary)
    {
        if (summaryGroup == null || summaryText == null)
            return;
        summaryRevealSequence?.Kill();
        summaryText.text = string.Empty;
        summaryGroup.DOKill();
        IsSummaryTransitioning = true;
        if (summaryBackdrop != null)
        {
            summaryBackdrop.DOKill();
            summaryBackdrop.color = new Color(summaryBackdropColor.r, summaryBackdropColor.g,
                summaryBackdropColor.b, 0f);
            summaryBackdrop.DOFade(summaryBackdropColor.a, summaryTransitionDuration)
                .SetEase(Ease.OutSine);
        }
        summaryGroup.alpha = 1f;
        summaryGroup.blocksRaycasts = true;
        summaryGroup.interactable = true;
        if (summaryPanel != null)
        {
            summaryPanel.DOKill();
            RectTransform root = summaryGroup.transform as RectTransform;
            float below = root != null ? -root.rect.height - summarySlideDistance :
                summaryHome.y - summaryPanel.rect.height - summarySlideDistance;
            summaryPanel.anchoredPosition = new Vector2(summaryHome.x, below);
            summaryPanel.DOAnchorPos(summaryHome, summaryTransitionDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => IsSummaryTransitioning = false);
        }
        else IsSummaryTransitioning = false;

        summaryRevealSequence = DOTween.Sequence().AppendInterval(summaryTransitionDuration);
        string revealed = string.Empty;
        foreach (string line in summary.Replace("\r", string.Empty).Split('\n'))
        {
            if (line.Length == 0) continue;
            string next = revealed.Length == 0 ? line : revealed + "\n" + line;
            summaryRevealSequence.AppendCallback(() => summaryText.text = next);
            summaryRevealSequence.AppendInterval(summaryLineInterval);
            revealed = next;
        }
    }

    public void HideSummary()
    {
        if (summaryGroup == null)
            return;
        IsSummaryTransitioning = false;
        summaryRevealSequence?.Kill();
        summaryGroup.DOKill();
        summaryGroup.blocksRaycasts = false;
        summaryGroup.interactable = false;
        summaryGroup.DOFade(0, summaryFadeDuration);
        if (summaryPanel != null)
        {
            summaryPanel.DOKill();
            summaryPanel.DOAnchorPosX(summaryHome.x - summarySlideDistance,
                summaryFadeDuration).SetEase(Ease.InCubic)
                .OnComplete(() => summaryPanel.anchoredPosition = summaryHome);
        }
    }
}
