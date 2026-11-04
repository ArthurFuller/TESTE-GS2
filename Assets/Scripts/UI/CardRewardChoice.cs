using System;
using System.Collections.Generic;
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
    [SerializeField] private Sprite placeholderArt;
    [SerializeField] private RectTransform[] choiceCards;
    [SerializeField] private Button[] rerollButtons;
    [SerializeField, Min(0f)] private float floatDistance = 6f;
    [SerializeField, Min(0.1f)] private float floatDuration = 1.3f;

    private readonly ActivityCardDrag[] offered = new ActivityCardDrag[2];
    private readonly Vector2[] homes = new Vector2[2];
    private readonly Image[] glows = new Image[2];
    private readonly bool[] rerolled = new bool[2];
    private readonly List<ActivityCardDrag> available = new List<ActivityCardDrag>();
    private readonly NameStyle[] defaultNames = new NameStyle[2];
    private Action completed;
    private int currentDay;

    private struct NameStyle
    {
        public Vector2 Position;
        public Vector2 Size;
        public Font Font;
        public int FontSize;
        public FontStyle FontStyle;
        public Color Color;
        public TextAnchor Alignment;
        public bool BestFit;
        public int MinSize;
        public int MaxSize;

        public NameStyle(Text label)
        {
            RectTransform rect = label.rectTransform;
            Position = rect.anchoredPosition;
            Size = rect.sizeDelta;
            Font = label.font;
            FontSize = label.fontSize;
            FontStyle = label.fontStyle;
            Color = label.color;
            Alignment = label.alignment;
            BestFit = label.resizeTextForBestFit;
            MinSize = label.resizeTextMinSize;
            MaxSize = label.resizeTextMaxSize;
        }

        public void Apply(Text label)
        {
            RectTransform rect = label.rectTransform;
            rect.anchoredPosition = Position;
            rect.sizeDelta = Size;
            label.font = Font;
            label.fontSize = FontSize;
            label.fontStyle = FontStyle;
            label.color = Color;
            label.alignment = Alignment;
            label.resizeTextForBestFit = BestFit;
            label.resizeTextMinSize = MinSize;
            label.resizeTextMaxSize = MaxSize;
        }
    }

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
        for (int i = 0; i < choiceNames.Length && i < 2; i++)
            if (choiceNames[i] != null) defaultNames[i] = new NameStyle(choiceNames[i]);
        for (int i = 0; i < rerollButtons.Length && i < 2; i++)
        {
            if (rerollButtons[i] == null) continue;
            int index = i;
            rerollButtons[i].onClick.AddListener(() => Reroll(index));
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

    public void Begin(int day, Action onCompleted)
    {
        completed = onCompleted;
        currentDay = day;
        available.Clear();
        if (rewards != null)
            foreach (ActivityCardDrag reward in rewards)
                AddAvailable(reward);
        AddAvailable(hand != null ? hand.TemplateFor(ActivityKind.PlantCorn) : null);
        AddAvailable(hand != null ? hand.TemplateFor(ActivityKind.PlantPumpkin) : null);
        AddAvailable(hand != null ? hand.TemplateFor(ActivityKind.PlantJerimum) : null);
        AddAvailable(hand != null ? hand.TemplateFor(ActivityKind.PrepareFood) : null);
        if (hand == null || !hand.HasRoom || available.Count < 2)
        {
            completed = null;
            onCompleted?.Invoke();
            return;
        }
        if (day == 1)
        {
            offered[0] = hand.TemplateFor(ActivityKind.PlantPumpkin);
            offered[1] = hand.TemplateFor(ActivityKind.PlantJerimum);
        }
        else
        {
            int first = UnityEngine.Random.Range(0, available.Count);
            int second = UnityEngine.Random.Range(0, available.Count - 1);
            if (second >= first) second++;
            offered[0] = available[first];
            offered[1] = available[second];
        }
        if (offered[0] == null || offered[1] == null)
        {
            completed = null;
            onCompleted?.Invoke();
            return;
        }
        for (int i = 0; i < 2; i++)
        {
            rerolled[i] = false;
            ShowChoice(i);
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
        UpdateRerollButtons();
    }

    private void AddAvailable(ActivityCardDrag card)
    {
        if (card == null || !card.HasEffect || card.Kind == ActivityKind.BuildHouse ||
            card.Kind == ActivityKind.BuildFence) return;
        foreach (ActivityCardDrag existing in available)
            if (existing.Kind == card.Kind) return;
        available.Add(card);
    }

    private void ShowChoice(int index)
    {
        if (choiceArt[index] == null) return;
        Image source = offered[index].GetComponent<Image>();
        Sprite sprite = source != null ? source.sprite : null;
        bool placeholder = sprite == null || sprite == placeholderArt;
        choiceArt[index].sprite = sprite != null ? sprite : placeholderArt;
        choiceArt[index].color = Color.white;
        Text label = choiceNames[index];
        if (label == null) return;
        label.gameObject.SetActive(placeholder);
        if (!placeholder) return;
        defaultNames[index].Apply(label);
        label.text = offered[index].DisplayName;
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.rectTransform.sizeDelta = new Vector2(128f, 100f);
        label.fontSize = 17;
        label.color = new Color(0.72f, 0.1f, 0.09f);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 12;
        label.resizeTextMaxSize = 17;
    }

    private void Reroll(int index)
    {
        if (completed == null || currentDay <= 1 || rerolled[index]) return;
        var alternatives = new List<ActivityCardDrag>();
        foreach (ActivityCardDrag card in available)
            if (card.Kind != offered[0].Kind && card.Kind != offered[1].Kind)
                alternatives.Add(card);
        if (alternatives.Count == 0) return;
        offered[index] = alternatives[UnityEngine.Random.Range(0, alternatives.Count)];
        rerolled[index] = true;
        ShowChoice(index);
        if (choiceCards[index] != null)
            choiceCards[index].DOPunchScale(Vector3.one * 0.08f, 0.22f);
        UpdateRerollButtons();
    }

    private void UpdateRerollButtons()
    {
        for (int i = 0; i < rerollButtons.Length; i++)
        {
            Button button = rerollButtons[i];
            if (button == null) continue;
            button.gameObject.SetActive(currentDay > 1 && completed != null);
            bool hasAlternative = false;
            foreach (ActivityCardDrag card in available)
                if (card.Kind != offered[0].Kind && card.Kind != offered[1].Kind)
                {
                    hasAlternative = true;
                    break;
                }
            button.interactable = !rerolled[i] && hasAlternative;
        }
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
        foreach (Button button in rerollButtons)
            if (button != null) button.gameObject.SetActive(false);
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
