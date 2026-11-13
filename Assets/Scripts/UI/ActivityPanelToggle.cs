using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActivityPanelToggle : MonoBehaviour
{
    [SerializeField] private RectTransform cardsPanel;
    [SerializeField] private CanvasGroup cardsGroup;
    [SerializeField] private Text buttonText;
    [SerializeField, Min(0)] private float hiddenOffset = 140f;
    [SerializeField, Min(0)] private float animationDuration = 0.35f;
    [SerializeField] private Ease openingEase = Ease.OutCubic;

    private Vector2 openPosition;
    private bool isOpen;

    private void Awake()
    {
        if (cardsPanel == null || cardsGroup == null)
            return;
        openPosition = cardsPanel.anchoredPosition;
        cardsPanel.anchoredPosition = openPosition + Vector2.up * hiddenOffset;
        cardsGroup.alpha = 0;
        cardsGroup.blocksRaycasts = false;
        cardsGroup.interactable = false;
        UpdateButtonText();
    }

    public void Toggle()
    {
        if (cardsPanel == null || cardsGroup == null)
            return;
        isOpen = !isOpen;
        cardsPanel.DOKill();
        cardsGroup.DOKill();
        cardsPanel.DOAnchorPos(isOpen ? openPosition : openPosition + Vector2.up * hiddenOffset,
            animationDuration).SetEase(openingEase).OnComplete(() =>
            {
                if (isOpen) cardsGroup.blocksRaycasts = true;
            });
        if (isOpen) cardsGroup.alpha = 1f;
        else cardsGroup.DOFade(0, animationDuration);
        cardsGroup.blocksRaycasts = false;
        cardsGroup.interactable = isOpen;
        UpdateButtonText();
    }

    private void UpdateButtonText()
    {
        if (buttonText != null)
            buttonText.text = isOpen ? "FECHAR" : "AÇÕES";
    }
}
