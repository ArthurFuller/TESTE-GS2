using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardRewardChoice : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField] private ActivityCardDrag[] rewards;
    [SerializeField] private Button[] choiceButtons;
    [SerializeField] private Image[] choiceArt;
    [SerializeField] private Text[] choiceNames;
    [SerializeField] private RectTransform[] choiceCards;
    [SerializeField, Min(0f)] private float floatDistance = 6f;
    [SerializeField, Min(0.1f)] private float floatDuration = 1.3f;

    private readonly ActivityCardDrag[] offered = new ActivityCardDrag[2];
    private readonly Vector2[] homes = new Vector2[2];
    private readonly Image[] glows = new Image[2];
    private Action completed;

    private void Awake()
    {
        for (int i = 0; i < choiceCards.Length && i < 2; i++)
            if (choiceCards[i] != null)
            {
                homes[i] = choiceCards[i].anchoredPosition;
                Transform glow = choiceCards[i].parent.Find("Brilho " + (i + 1));
                if (glow != null) glows[i] = glow.GetComponent<Image>();
            }
        for (int i = 0; i < choiceButtons.Length && i < 2; i++)
        {
            int index = i;
            choiceButtons[i].onClick.AddListener(() => Choose(index));
        }
        Hide();
    }

    private void OnDestroy()
    {
        foreach (RectTransform card in choiceCards)
            if (card != null) card.DOKill();
        foreach (Image glow in glows)
            if (glow != null) glow.DOKill();
    }

    public void Begin(Action onCompleted)
    {
        completed = onCompleted;
        if (hand == null || !hand.HasRoom || rewards == null || rewards.Length < 2)
        {
            completed = null;
            onCompleted?.Invoke();
            return;
        }
        int first = UnityEngine.Random.Range(0, rewards.Length);
        int second = UnityEngine.Random.Range(0, rewards.Length - 1);
        if (second >= first) second++;
        offered[0] = rewards[first];
        offered[1] = rewards[second];
        for (int i = 0; i < 2; i++)
        {
            if (choiceNames[i] != null) choiceNames[i].text = offered[i].DisplayName;
            if (choiceArt[i] != null)
            {
                Image source = offered[i].GetComponent<Image>();
                choiceArt[i].sprite = source != null ? source.sprite : null;
                choiceArt[i].color = Color.white;
            }
            RectTransform card = choiceCards[i];
            if (card == null) continue;
            card.DOKill();
            card.anchoredPosition = homes[i];
            card.DOAnchorPosY(homes[i].y + floatDistance, floatDuration)
                .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            if (glows[i] != null)
                glows[i].DOFade(0.7f, floatDuration).SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
        }
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    private void Choose(int index)
    {
        if (completed == null || index < 0 || index >= 2 || offered[index] == null) return;
        if (!hand.AddCard(offered[index].Kind)) return;
        Hide();
        Action callback = completed;
        completed = null;
        callback?.Invoke();
    }

    private void Hide()
    {
        if (group == null) return;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        for (int i = 0; i < choiceCards.Length && i < 2; i++)
            if (choiceCards[i] != null)
            {
                choiceCards[i].DOKill();
                choiceCards[i].anchoredPosition = homes[i];
            }
        foreach (Image glow in glows)
            if (glow != null)
            {
                glow.DOKill();
                Color color = glow.color;
                glow.color = new Color(color.r, color.g, color.b, 0.38f);
            }
    }
}
