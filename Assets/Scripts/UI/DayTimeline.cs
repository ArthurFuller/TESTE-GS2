using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public sealed class DayTimeline : MonoBehaviour
{
    [SerializeField] private DayCycle days;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private DayEventController dayEvents;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private RectTransform topBar;
    [SerializeField] private RectTransform missionsButton;
    [SerializeField] private Font font;
    [SerializeField, Min(1)] private int totalDays = 12;
    [Header("Padrões para elementos novos")]
    [SerializeField, Min(170f)] private float panelWidth = 380f;
    [SerializeField, Min(44f)] private float daysHeight = 96f;
    [SerializeField, Min(120f)] private float cardsHeight = 160f;
    [SerializeField, Min(30f)] private float topBarHeight = 78f;
    [SerializeField, Min(24f)] private float dayButtonWidth = 34f;
    [SerializeField, Min(20f)] private float dayButtonHeight = 22f;
    [SerializeField, Min(1f)] private float dayLineHeight = 3f;
    [Header("Cartas geradas no histórico")]
    [SerializeField, Min(45f)] private float cardWidth = 60f;
    [SerializeField, Min(65f)] private float cardHeight = 83f;
    [SerializeField, Min(0f)] private float cardGap = 10f;
    [SerializeField, Min(1f)] private float cardLineHeight = 3f;
    [SerializeField, Min(0f)] private float cardStartX = 18f;
    [SerializeField, Min(0f)] private float cardTopInset = 8f;
    [SerializeField] private float cardLineOffsetY;
    [Header("Animação")]
    [SerializeField, Min(0.05f)] private float slideDuration = 0.36f;
    [SerializeField, Min(0f)] private float waveDelay = 0.055f;

    private sealed class PlayedCard
    {
        public string Name;
        public Sprite Art;
    }

    private readonly Dictionary<int, List<PlayedCard>> history = new();
    private RectTransform panelClip;
    private RectTransform panel;
    private RectTransform trigger;
    private RectTransform dayRow;
    private RectTransform dayContent;
    private ScrollRect dayScroll;
    private RectTransform cardScroll;
    private RectTransform cardContent;
    private ScrollRect scroll;
    private Button triggerButton;
    private Button backButton;
    private Text title;
    private CanvasGroup panelGroup;
    private readonly List<RectTransform> dayNodes = new();
    private readonly List<RectTransform> dayLines = new();
    private readonly List<RectTransform> cardNodes = new();
    private readonly List<RectTransform> cardLines = new();
    private readonly Dictionary<RectTransform, float> dayHomeY = new();
    private Vector2 daysPanelSize;
    private Vector2 panelHomePosition;
    private bool open;
    private int viewedDay;

    private void Awake()
    {
        EnsureUI();
        if (Application.isPlaying) CloseImmediately();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall -= RefreshEditor;
            UnityEditor.EditorApplication.delayCall += RefreshEditor;
            return;
        }
#endif
        if (dayEvents == null) dayEvents = FindFirstObjectByType<DayEventController>();
        if (actions != null) actions.CardPlayed += Record;
        if (days != null) days.DayChanged += RefreshDays;
        EnsureUI();
        RefreshDays();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= RefreshEditor;
        UnityEditor.EditorApplication.delayCall -= RefreshInspector;
#endif
        if (actions != null) actions.CardPlayed -= Record;
        if (days != null) days.DayChanged -= RefreshDays;
        if (panel != null) panel.DOKill();
        DOTween.Kill(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshInspector;
        UnityEditor.EditorApplication.delayCall += RefreshInspector;
    }

    private void RefreshInspector()
    {
        if (this == null) return;
        if (!Application.isPlaying)
        {
            RefreshEditor();
            return;
        }
        if (open && viewedDay > 0 && cardScroll != null &&
            cardScroll.gameObject.activeInHierarchy)
            RenderCards(viewedDay, false);
    }

    private void RefreshEditor()
    {
        if (this == null || Application.isPlaying) return;
        EnsureUI();
        RefreshDays();
        if (panelGroup != null)
        {
            if (panelGroup.alpha <= 0f) panelGroup.alpha = 1f;
            panelGroup.interactable = panelGroup.blocksRaycasts = false;
        }
    }
#endif

    private void Record(int day, ActivityCardDrag card)
    {
        if (card == null) return;
        if (!history.TryGetValue(day, out List<PlayedCard> cards))
        {
            cards = new List<PlayedCard>();
            history.Add(day, cards);
        }
        Image image = card.GetComponent<Image>();
        cards.Add(new PlayedCard { Name = card.DisplayName,
            Art = image != null ? image.sprite : null });
        if (open && viewedDay == day) RenderCards(day, false);
    }

    private void Update()
    {
        if (Application.isPlaying && open && days != null && !days.CanAcceptActions)
            Close();
    }

    private void EnsureUI()
    {
        if (topBar == null || missionsButton == null) return;
        if (font == null)
        {
            Text reference = GetComponentInChildren<Text>(true);
            if (reference != null) font = reference.font;
        }
        if (font == null) return;
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        bool created = false;

        trigger = topBar.Find("Botão da linha do tempo") as RectTransform;
        if (trigger == null)
        {
            created = true;
            trigger = Box("Botão da linha do tempo", topBar,
                new Color(0.16f, 0.16f, 0.13f), true);
            Label("DIAS", trigger, 14, Color.white);
        }
        triggerButton = trigger.GetComponent<Button>();
        triggerButton.onClick.RemoveAllListeners();
        triggerButton.onClick.AddListener(Toggle);

        panelClip = transform.Find("Recorte da linha do tempo") as RectTransform;
        if (panelClip == null)
        {
            created = true;
            panelClip = Empty("Recorte da linha do tempo", transform);
        }
        if (panelClip.GetComponent<RectMask2D>() == null)
            panelClip.gameObject.AddComponent<RectMask2D>();
        if (panelClip.parent == topBar.parent &&
            panelClip.GetSiblingIndex() > topBar.GetSiblingIndex())
            panelClip.SetSiblingIndex(topBar.GetSiblingIndex());

        panel = panelClip.Find("Linha do tempo") as RectTransform;
        if (panel == null)
        {
            panel = transform.Find("Linha do tempo") as RectTransform;
            if (panel != null) panel.SetParent(panelClip, false);
        }
        if (panel == null)
        {
            created = true;
            panel = Box("Linha do tempo", panelClip,
                new Color(0.88f, 0.82f, 0.65f, 0.98f), false);
        }
        panelGroup = panel.GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        if (panel.GetComponent<RectMask2D>() == null)
            panel.gameObject.AddComponent<RectMask2D>();
        dayRow = panel.Find("Dias") as RectTransform;
        if (dayRow == null) { created = true; dayRow = Empty("Dias", panel); }
        Image dayImage = dayRow.GetComponent<Image>();
        if (dayImage == null)
        {
            dayImage = dayRow.gameObject.AddComponent<Image>();
            dayImage.color = new Color(1f, 1f, 1f, 0.01f);
        }
        if (dayRow.GetComponent<Mask>() == null)
            dayRow.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        dayScroll = dayRow.GetComponent<ScrollRect>();
        if (dayScroll == null)
        {
            dayScroll = dayRow.gameObject.AddComponent<ScrollRect>();
            dayScroll.horizontal = true;
            dayScroll.vertical = false;
            dayScroll.movementType = ScrollRect.MovementType.Clamped;
        }
        dayContent = dayRow.Find("Conteúdo dos dias") as RectTransform;
        if (dayContent == null)
        {
            created = true;
            dayContent = Empty("Conteúdo dos dias", dayRow);
            for (int i = dayRow.childCount - 1; i >= 0; i--)
            {
                Transform old = dayRow.GetChild(i);
                if (old != dayContent) old.SetParent(dayContent, false);
            }
        }
        if (dayScroll.viewport == null) dayScroll.viewport = dayRow;
        if (dayScroll.content == null) dayScroll.content = dayContent;
        cardScroll = panel.Find("Rolagem de cartas") as RectTransform;
        if (cardScroll == null)
        {
            created = true;
            cardScroll = Empty("Rolagem de cartas", panel);
        }
        scroll = cardScroll.GetComponent<ScrollRect>();
        if (scroll == null)
        {
            scroll = cardScroll.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
        }
        RectTransform viewport = cardScroll.Find("Janela") as RectTransform;
        if (viewport == null)
        {
            created = true;
            viewport = Box("Janela", cardScroll,
                new Color(1f, 1f, 1f, 0.01f), false);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }
        cardContent = viewport.Find("Cartas em ordem") as RectTransform;
        if (cardContent == null)
        {
            created = true;
            cardContent = Empty("Cartas em ordem", viewport);
        }
        if (scroll.viewport == null) scroll.viewport = viewport;
        if (scroll.content == null) scroll.content = cardContent;
        RectTransform titleRect = panel.Find("Título do dia") as RectTransform;
        if (titleRect == null) titleRect = Label("Título do dia", panel, 14,
            new Color(0.19f, 0.17f, 0.13f));
        title = titleRect.GetComponent<Text>();
        RectTransform back = panel.Find("Voltar aos dias") as RectTransform;
        if (back == null)
        {
            created = true;
            back = Box("Voltar aos dias", panel,
                new Color(0.22f, 0.2f, 0.16f), true);
            Label("< DIAS", back, 11, Color.white);
        }
        backButton = back.GetComponent<Button>();
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(BackToDays);
        if (created) Layout();
        if (!Application.isPlaying || daysPanelSize == Vector2.zero)
        {
            daysPanelSize = panel.sizeDelta;
            panelHomePosition = panel.anchoredPosition;
        }
    }

    private void Layout()
    {
        if (panel == null || trigger == null) return;
        trigger.anchorMin = trigger.anchorMax = new Vector2(0f, 1f);
        trigger.pivot = new Vector2(0f, 1f);
        trigger.sizeDelta = new Vector2(45f, missionsButton.rect.height);
        trigger.anchoredPosition = missionsButton.anchoredPosition +
            new Vector2(-trigger.rect.width - 8f, 0f);
        panelClip.anchorMin = panelClip.anchorMax = new Vector2(1f, 1f);
        panelClip.pivot = new Vector2(1f, 1f);
        panelClip.sizeDelta = new Vector2(panelWidth, cardsHeight);
        panelClip.anchoredPosition = new Vector2(-2f,
            -Mathf.Max(topBarHeight, topBar.rect.height));
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.sizeDelta = new Vector2(panelWidth, viewedDay > 0 ? cardsHeight : daysHeight);
        if (!Application.isPlaying) panel.anchoredPosition = Vector2.zero;
        Fill(dayRow, 0f, 0f);
        dayContent.anchorMin = dayContent.anchorMax = new Vector2(0f, 1f);
        dayContent.pivot = new Vector2(0f, 1f);
        cardScroll.anchorMin = new Vector2(0f, 1f);
        cardScroll.anchorMax = new Vector2(1f, 1f);
        cardScroll.pivot = new Vector2(0.5f, 1f);
        cardScroll.anchoredPosition = new Vector2(0f, -51f);
        cardScroll.sizeDelta = new Vector2(-20f, cardsHeight - 58f);
        RectTransform viewport = scroll.viewport;
        Fill(viewport, 0f, 0f);
        cardContent.anchorMin = cardContent.anchorMax = new Vector2(0f, 1f);
        cardContent.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(160f, 19f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -27f);
        RectTransform back = (RectTransform)backButton.transform;
        back.anchorMin = back.anchorMax = new Vector2(1f, 1f);
        back.pivot = new Vector2(1f, 1f);
        back.sizeDelta = new Vector2(68f, 22f);
        back.anchoredPosition = new Vector2(-7f, -26f);
    }

    private void RefreshDays()
    {
        if (dayRow == null) return;
        if (dayNodes.Count != totalDays || dayLines.Count != Mathf.Max(0, totalDays - 1))
        {
            dayNodes.Clear();
            dayLines.Clear();
            dayHomeY.Clear();
            float step = dayButtonWidth + 14f;
            bool createdDayChild = false;
            for (int i = 0; i < totalDays - 1; i++)
            {
                RectTransform line = dayContent.Find("Ligação do dia " + (i + 1)) as RectTransform;
                if (line == null)
                {
                    createdDayChild = true;
                    line = Box("Ligação do dia " + (i + 1), dayContent,
                        new Color(0.32f, 0.28f, 0.2f), false);
                    line.anchorMin = line.anchorMax = new Vector2(0f, 1f);
                    line.pivot = new Vector2(0f, 0.5f);
                    line.sizeDelta = new Vector2(step, dayLineHeight);
                    line.anchoredPosition = new Vector2(
                        12f + dayButtonWidth * 0.5f + i * step, -60f);
                }
                dayLines.Add(line);
            }
            for (int i = 1; i <= totalDays; i++)
            {
                int day = i;
                RectTransform node = dayContent.Find("Dia " + day) as RectTransform;
                if (node == null)
                {
                    createdDayChild = true;
                    node = Box("Dia " + day, dayContent,
                        new Color(0.28f, 0.25f, 0.19f), true);
                    Label(day.ToString(), node, 12, Color.white);
                    node.anchorMin = node.anchorMax = new Vector2(0f, 1f);
                    node.pivot = new Vector2(0.5f, 0.5f);
                    node.sizeDelta = new Vector2(dayButtonWidth, dayButtonHeight);
                    node.anchoredPosition = new Vector2(
                        12f + dayButtonWidth * 0.5f + (i - 1) * step, -60f);
                }
                Button button = node.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowDay(day));
                dayNodes.Add(node);
            }
            if (createdDayChild)
                dayContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                    Mathf.Max(dayContent.rect.width,
                        24f + dayButtonWidth + (totalDays - 1) * step));
        }
        foreach (RectTransform line in dayLines)
            if (!Application.isPlaying || !dayHomeY.ContainsKey(line))
                dayHomeY[line] = line.anchoredPosition.y;
        foreach (RectTransform node in dayNodes)
            if (!Application.isPlaying || !dayHomeY.ContainsKey(node))
                dayHomeY[node] = node.anchoredPosition.y;
        for (int i = 0; i < dayNodes.Count; i++)
        {
            RectTransform node = dayNodes[i];
            if (!Application.isPlaying) continue;
            Button button = node.GetComponent<Button>();
            button.interactable = days == null || i + 1 <= days.CurrentDay;
        }
    }

    private void PlaceDaysAbove()
    {
        foreach (RectTransform node in dayNodes)
            node.anchoredPosition = new Vector2(node.anchoredPosition.x,
                dayHomeY[node] + dayRow.rect.height);
        foreach (RectTransform line in dayLines)
            line.anchoredPosition = new Vector2(line.anchoredPosition.x,
                dayHomeY[line] + dayRow.rect.height);
    }

    private void AnimateDays(bool entering)
    {
        Ease ease = entering ? Ease.OutCubic : Ease.InCubic;
        for (int i = 0; i < dayNodes.Count; i++)
        {
            RectTransform node = dayNodes[i];
            node.DOKill();
            float destination = dayHomeY[node] + (entering ? 0f : dayRow.rect.height);
            node.DOAnchorPosY(destination, slideDuration * 0.65f)
                .SetDelay(i * waveDelay).SetEase(ease);
        }
        for (int i = 0; i < dayLines.Count; i++)
        {
            RectTransform line = dayLines[i];
            line.DOKill();
            float destination = dayHomeY[line] + (entering ? 0f : dayRow.rect.height);
            line.DOAnchorPosY(destination, slideDuration * 0.65f)
                .SetDelay((i + 0.5f) * waveDelay).SetEase(ease);
        }
    }

    public void Toggle()
    {
        if (!Application.isPlaying) return;
        if (open) Close();
        else Open();
    }

    public void ClosePanel()
    {
        if (open) Close();
    }

    private void Open()
    {
        if (days != null && !days.CanAcceptActions) return;
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (merchant != null && merchant.IsOpen) merchant.HideShop();
        EnsureUI();
        open = true;
        viewedDay = 0;
        dayRow.gameObject.SetActive(true);
        cardScroll.gameObject.SetActive(false);
        scroll.enabled = true;
        backButton.gameObject.SetActive(false);
        backButton.interactable = true;
        title.text = "LINHA DO TEMPO";
        RefreshDays();
        dayScroll.horizontalNormalizedPosition = 0f;
        PlaceDaysAbove();
        panel.DOKill();
        panel.sizeDelta = daysPanelSize;
        panel.anchoredPosition = panelHomePosition + Vector2.up * daysPanelSize.y;
        panelGroup.alpha = 1f;
        panelGroup.interactable = panelGroup.blocksRaycasts = true;
        panel.DOAnchorPosY(panelHomePosition.y, slideDuration).SetEase(Ease.OutCubic)
            .OnComplete(() => { if (open && viewedDay == 0) AnimateDays(true); });
    }

    private void Close()
    {
        open = false;
        DOTween.Kill(this);
        panel.DOKill();
        scroll.enabled = true;
        panelGroup.interactable = panelGroup.blocksRaycasts = false;
        panel.DOAnchorPosY(panelHomePosition.y + panel.rect.height, slideDuration)
            .SetEase(Ease.InCubic).OnComplete(() => panelGroup.alpha = 0f);
    }

    private void CloseImmediately()
    {
        if (panel == null) return;
        open = false;
        viewedDay = 0;
        panelGroup.alpha = 0f;
        panelGroup.interactable = panelGroup.blocksRaycasts = false;
        panel.anchoredPosition = panelHomePosition + Vector2.up * daysPanelSize.y;
        cardScroll.gameObject.SetActive(false);
        scroll.enabled = true;
        backButton.gameObject.SetActive(false);
    }

    private void ShowDay(int day)
    {
        if (days != null && day > days.CurrentDay) return;
        viewedDay = day;
        title.text = "DIA " + day;
        foreach (RectTransform node in dayNodes)
            node.GetComponent<Button>().interactable = false;
        AnimateDays(false);
        float end = Mathf.Max(0, dayNodes.Count - 1) * waveDelay +
            slideDuration * 0.65f;
        DOVirtual.DelayedCall(end, () =>
        {
            if (!open || viewedDay != day) return;
            dayRow.gameObject.SetActive(false);
            backButton.gameObject.SetActive(true);
            backButton.interactable = false;
            cardScroll.gameObject.SetActive(false);
            Clear(cardContent);
            panel.DOSizeDelta(new Vector2(daysPanelSize.x, panelClip.rect.height), slideDuration)
                .SetEase(Ease.OutCubic).OnComplete(() =>
                {
                    if (!open || viewedDay != day) return;
                    cardScroll.gameObject.SetActive(true);
                    RenderCards(day, true);
                    backButton.interactable = true;
                });
        }).SetTarget(this);
    }

    private void BackToDays()
    {
        if (viewedDay == 0 || !cardScroll.gameObject.activeSelf) return;
        viewedDay = 0;
        backButton.interactable = false;
        scroll.enabled = false;
        AnimateCardsOut();
        float end = Mathf.Max(0, cardNodes.Count - 1) * waveDelay +
            slideDuration * 0.65f;
        DOVirtual.DelayedCall(end, () =>
        {
            if (!open || viewedDay != 0) return;
            cardScroll.gameObject.SetActive(false);
            scroll.enabled = true;
            backButton.gameObject.SetActive(false);
            title.text = "LINHA DO TEMPO";
            panel.DOKill();
            panel.DOSizeDelta(daysPanelSize, slideDuration)
                .SetEase(Ease.OutCubic).OnComplete(() =>
                {
                    if (!open || viewedDay != 0) return;
                    dayRow.gameObject.SetActive(true);
                    RefreshDays();
                    PlaceDaysAbove();
                    AnimateDays(true);
                    backButton.interactable = true;
                });
        }).SetTarget(this);
    }

    private void AnimateCardsOut()
    {
        float exitY = cardHeight + cardTopInset + 4f;
        float duration = slideDuration * 0.65f;
        for (int i = 0; i < cardNodes.Count; i++)
        {
            RectTransform card = cardNodes[i];
            if (card == null) continue;
            card.DOKill();
            card.DOAnchorPosY(exitY, duration).SetDelay(i * waveDelay)
                .SetEase(Ease.InCubic);
        }
        for (int i = 0; i < cardLines.Count; i++)
        {
            RectTransform line = cardLines[i];
            if (line == null) continue;
            line.DOKill();
            line.DOAnchorPosY(exitY, duration).SetDelay((i + 0.5f) * waveDelay)
                .SetEase(Ease.InCubic);
        }
    }

    private void RenderCards(int day, bool animate)
    {
        Clear(cardContent);
        cardNodes.Clear();
        cardLines.Clear();
        var cards = new List<PlayedCard>();
        if (history.TryGetValue(day, out List<PlayedCard> played)) cards.AddRange(played);
        if (dayEvents != null && dayEvents.TryGetEvent(day, out string eventName,
                out Sprite eventArt))
            cards.Insert(0, new PlayedCard { Name = eventName, Art = eventArt });
        int count = cards.Count;
        float stride = cardWidth + cardGap;
        float width = Mathf.Max(scroll.viewport.rect.width,
            count * stride + cardStartX * 2f);
        cardContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        scroll.horizontalNormalizedPosition = 0f;
        if (count == 0)
        {
            RectTransform empty = Label("Nenhuma carta usada neste dia", cardContent,
                11, new Color(0.25f, 0.22f, 0.17f));
            empty.anchorMin = empty.anchorMax = new Vector2(0f, 1f);
            empty.pivot = new Vector2(0f, 1f);
            empty.anchoredPosition = new Vector2(18f, -40f);
            empty.sizeDelta = new Vector2(width - 36f, 40f);
            cardNodes.Add(empty);
            return;
        }
        float cardY = -cardTopInset;
        float lineY = cardY - cardHeight * 0.5f + cardLineOffsetY;
        float entryY = cardHeight + cardTopInset + 4f;
        for (int i = 0; i < count - 1; i++)
        {
            RectTransform line = Box("Ligação da carta " + (i + 1), cardContent,
                new Color(0.32f, 0.28f, 0.2f), false);
            line.anchorMin = line.anchorMax = new Vector2(0f, 1f);
            line.pivot = new Vector2(0f, 0.5f);
            line.sizeDelta = new Vector2(stride, cardLineHeight);
            line.anchoredPosition = new Vector2(cardStartX + cardWidth * 0.5f + i * stride,
                animate ? entryY : lineY);
            cardLines.Add(line);
            if (animate)
                line.DOAnchorPosY(lineY, slideDuration)
                    .SetDelay((i + 0.5f) * waveDelay).SetEase(Ease.OutCubic);
        }
        for (int i = 0; i < count; i++)
        {
            PlayedCard data = cards[i];
            RectTransform card = Box("Carta " + (i + 1), cardContent,
                Color.white, false);
            card.anchorMin = card.anchorMax = new Vector2(0f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.sizeDelta = new Vector2(cardWidth, cardHeight);
            float x = cardStartX + cardWidth * 0.5f + i * stride;
            card.anchoredPosition = new Vector2(x, animate ? entryY : cardY);
            cardNodes.Add(card);
            Image art = card.GetComponent<Image>();
            art.sprite = data.Art;
            art.preserveAspect = true;
            if (data.Art == null)
            {
                art.color = new Color(0.92f, 0.88f, 0.76f);
                Label(data.Name, card, 9, new Color(0.55f, 0.1f, 0.1f));
            }
            if (animate)
                card.DOAnchorPosY(cardY, slideDuration).SetDelay(i * waveDelay)
                    .SetEase(Ease.OutBack);
        }
    }

    private static RectTransform Empty(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform));
        node.layer = 5;
        RectTransform rect = (RectTransform)node.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static RectTransform Box(string name, Transform parent, Color color, bool button)
    {
        RectTransform rect = Empty(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (button) rect.gameObject.AddComponent<Button>().targetGraphic = image;
        return rect;
    }

    private RectTransform Label(string value, Transform parent, int size, Color color)
    {
        RectTransform rect = Empty(value, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Text label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.text = value;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        return rect;
    }

    private static void Fill(RectTransform rect, float x, float y)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(x, y);
        rect.offsetMax = new Vector2(-x, -y);
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.transform.DOKill();
            child.SetActive(false);
            child.transform.SetParent(null, false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}
