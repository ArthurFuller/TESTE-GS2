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
    [SerializeField, Min(0.01f)] private float summaryLineInterval = 0.18f;
    [SerializeField, Min(0)] private float feedbackPunchDuration = 0.2f;
    [SerializeField, Min(0)] private float feedbackPunchScale = 0.08f;

    private Sequence summaryRevealSequence;

    private void Awake()
    {
        if (summaryGroup != null)
        {
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
        summaryGroup.blocksRaycasts = true;
        summaryGroup.interactable = true;
        summaryGroup.DOFade(1, summaryFadeDuration);
        if (summaryPanel != null)
        {
            summaryPanel.DOKill();
            summaryPanel.localScale = Vector3.one * 0.96f;
            summaryPanel.DOScale(1, summaryFadeDuration);
        }

        summaryRevealSequence = DOTween.Sequence().AppendInterval(summaryFadeDuration);
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
        summaryRevealSequence?.Kill();
        summaryGroup.DOKill();
        summaryGroup.blocksRaycasts = false;
        summaryGroup.interactable = false;
        summaryGroup.DOFade(0, summaryFadeDuration);
    }
}
