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
    [SerializeField] private RectTransform[] choiceCards;
    [SerializeField, Min(0f)] private float floatDistance = 6f;
    [SerializeField, Min(0.1f)] private float floatDuration = 1.3f;

    private readonly ActivityCardDrag[] offered = new ActivityCardDrag[2];
    private readonly Vector2[] homes = new Vector2[2];
    private readonly Image[] glows = new Image[2];
    private readonly Button[] rerollButtons = new Button[2];
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
        for (int i = 0; i < choiceCards.Length && i < 2; i++)
            CreateRerollButton(i);
        Hide();
    }

    private void CreateRerollButton(int index)
    {
        if (choiceCards[index] == null) return;
        Transform existing = choiceCards[index].parent.Find("Rerrolar carta " + (index + 1));
        if (existing != null)
        {
            Button sceneButton = existing.GetComponent<Button>();
            if (sceneButton != null)
            {
                int choiceIndex = index;
                sceneButton.onClick.AddListener(() => Reroll(choiceIndex));
                rerollButtons[index] = sceneButton;
                sceneButton.gameObject.SetActive(false);
                return;
            }
        }
        var root = new GameObject("Rerrolar carta " + (index + 1), typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(choiceCards[index].parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(140f, 30f);
        rect.anchoredPosition = choiceCards[index].anchoredPosition + new Vector2(0f, -122f);
        Image background = root.GetComponent<Image>();
        background.color = new Color(0.76f, 0.66f, 0.43f);
        Button button = root.GetComponent<Button>();
        button.targetGraphic = background;
        int choiceIndex = index;
        button.onClick.AddListener(() => Reroll(choiceIndex));
        rerollButtons[index] = button;

        var caption = new GameObject("Texto", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text));
        RectTransform labelRect = (RectTransform)caption.transform;
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        Text label = caption.GetComponent<Text>();
        label.font = choiceNames[index] != null ? choiceNames[index].font :
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 17;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.16f, 0.12f, 0.08f);
        label.raycastTarget = false;
        label.text = "RERROLAR (1)";
        root.SetActive(false);
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
        SetChoiceName(index);
        if (choiceArt[index] == null) return;
        Image source = offered[index].GetComponent<Image>();
        choiceArt[index].sprite = source != null ? source.sprite : null;
        choiceArt[index].color = Color.white;
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

    private void SetChoiceName(int index)
    {
        if (choiceNames[index] == null) return;
        Text label = choiceNames[index];
        Text cardTitle = null;
        foreach (Text text in offered[index].GetComponentsInChildren<Text>(true))
            if (text.gameObject.activeSelf)
            {
                cardTitle = text;
                break;
            }

        (cardTitle != null ? new NameStyle(cardTitle) : defaultNames[index]).Apply(label);
        label.text = cardTitle != null ? cardTitle.text : offered[index].DisplayName;
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
