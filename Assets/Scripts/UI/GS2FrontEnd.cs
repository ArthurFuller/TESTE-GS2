using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Navegação da cena de entrada; a transição preta permanece até a cena do jogo abrir.
public sealed class GS2FrontEnd : MonoBehaviour
{
    [SerializeField] private CanvasGroup root;
    [SerializeField] private CanvasGroup blackFade;
    [SerializeField] private GS2SceneFade sceneFade;
    [SerializeField] private RectTransform mainPage;
    [SerializeField] private RectTransform optionsPage;
    [SerializeField] private RectTransform leaderboardPage;
    [SerializeField] private CanvasGroup mainGroup;
    [SerializeField] private CanvasGroup optionsGroup;
    [SerializeField] private CanvasGroup leaderboardGroup;
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button optionsBackButton;
    [SerializeField] private Button leaderboardButton;
    [SerializeField] private Button leaderboardBackButton;
    [SerializeField] private Text statusText;
    [SerializeField, Min(0.1f)] private float slideSeconds = 0.4f;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 0.36f;
    [SerializeField, Range(0.8f, 1f)] private float enteringScale = 0.975f;
    private Sequence motion;
    private PageItems mainItems;
    private PageItems optionsItems;
    private bool locked;
    private enum Page { Main, Options, Leaderboard }
    private Page current;

    private sealed class PageItems
    {
        public RectTransform[] rects;
        public Vector2[] positions;
        public Vector3[] scales;
    }

    private void Awake()
    {
        Time.timeScale = 0f;
        if (root != null) { root.alpha = 0f; root.interactable = false; root.blocksRaycasts = true; }
        SetInitialPage(mainPage, mainGroup, true);
        SetInitialPage(optionsPage, optionsGroup, false);
        SetInitialPage(leaderboardPage, leaderboardGroup, false);
        mainItems = CaptureItems(mainPage);
        optionsItems = CaptureItems(optionsPage);
        if (mainPage != null) mainPage.localScale = Vector3.one * enteringScale;
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (exitButton != null) exitButton.onClick.AddListener(Exit);
        if (optionsBackButton != null) optionsBackButton.onClick.AddListener(Back);
        if (leaderboardButton != null) leaderboardButton.onClick.AddListener(OpenLeaderboard);
        if (leaderboardBackButton != null) leaderboardBackButton.onClick.AddListener(Back);
        current = Page.Main;
        locked = true;
        motion = DOTween.Sequence().SetUpdate(true);
        if (root != null) motion.Join(root.DOFade(1f, fadeSeconds));
        if (mainPage != null)
            motion.Join(mainPage.DOScale(1f, fadeSeconds).SetEase(Ease.OutCubic));
        motion.OnComplete(() =>
        {
            if (root != null) root.interactable = true;
            locked = false;
        });
    }

    private void OnDestroy()
    {
        motion?.Kill();
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        if (optionsButton != null) optionsButton.onClick.RemoveListener(OpenOptions);
        if (exitButton != null) exitButton.onClick.RemoveListener(Exit);
        if (optionsBackButton != null) optionsBackButton.onClick.RemoveListener(Back);
        if (leaderboardButton != null) leaderboardButton.onClick.RemoveListener(OpenLeaderboard);
        if (leaderboardBackButton != null) leaderboardBackButton.onClick.RemoveListener(Back);
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    public void Play()
    {
        if (locked || root == null || sceneFade == null) return;
        locked = true;
        root.interactable = false;
        root.blocksRaycasts = false;
        motion?.Kill();
        sceneFade.EnterGameplay("PrototypeLayout");
    }

    public void OpenOptions() => SwitchTo(Page.Options);
    public void OpenLeaderboard() => SwitchTo(Page.Leaderboard);
    public void Back() => SwitchTo(current == Page.Leaderboard ? Page.Options : Page.Main);

    private void SwitchTo(Page destination)
    {
        if (locked || current == destination) return;
        RectTransform from = PageRect(current);
        RectTransform to = PageRect(destination);
        CanvasGroup fromGroup = PageGroup(current);
        CanvasGroup toGroup = PageGroup(destination);
        if (from == null || to == null || fromGroup == null || toGroup == null) return;
        locked = true;
        Canvas.ForceUpdateCanvases();
        float distance = SlideDistance(from, to);
        bool back = destination == Page.Main ||
            (current == Page.Leaderboard && destination == Page.Options);
        float entryX = back ? -distance : distance;
        to.gameObject.SetActive(true);
        bool wave = destination == Page.Main || destination == Page.Options;
        to.anchoredPosition = wave ? Vector2.zero : new Vector2(entryX, 0f);
        to.localScale = wave ? Vector3.one : Vector3.one * enteringScale;
        toGroup.alpha = wave ? 1f : 0f;
        toGroup.interactable = toGroup.blocksRaycasts = false;
        fromGroup.interactable = fromGroup.blocksRaycasts = false;
        motion?.Kill();
        motion = DOTween.Sequence().SetUpdate(true);
        motion.Join(from.DOAnchorPosX(-entryX, slideSeconds).SetEase(Ease.InOutCubic));
        if (wave)
            AnimateWave(destination == Page.Main ? mainItems : optionsItems, entryX, motion);
        else
        {
            motion.Join(to.DOAnchorPosX(0f, slideSeconds).SetEase(Ease.OutCubic));
            motion.Join(to.DOScale(1f, slideSeconds).SetEase(Ease.OutCubic));
            motion.Join(toGroup.DOFade(1f, slideSeconds * 0.85f));
        }
        motion.OnComplete(() =>
            {
                from.gameObject.SetActive(false);
                toGroup.interactable = toGroup.blocksRaycasts = true;
                current = destination;
                locked = false;
            });
    }

    private RectTransform PageRect(Page page) => page == Page.Main ? mainPage :
        page == Page.Options ? optionsPage : leaderboardPage;

    private CanvasGroup PageGroup(Page page) => page == Page.Main ? mainGroup :
        page == Page.Options ? optionsGroup : leaderboardGroup;

    private void SetInitialPage(RectTransform page, CanvasGroup group, bool active)
    {
        if (page == null) return;
        page.anchoredPosition = Vector2.zero;
        page.localScale = Vector3.one;
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = group.blocksRaycasts = active;
        }
        page.gameObject.SetActive(active);
    }

    private float SlideDistance(RectTransform from, RectTransform to)
    {
        RectTransform viewport = root.transform as RectTransform;
        float viewportWidth = viewport != null ? viewport.rect.width : 0f;
        return Mathf.Max(1100f, (viewportWidth + Mathf.Max(from.rect.width, to.rect.width)) * 0.5f + 32f);
    }

    private PageItems CaptureItems(RectTransform page)
    {
        if (page == null) return null;
        var items = new PageItems
        {
            rects = new RectTransform[page.childCount],
            positions = new Vector2[page.childCount],
            scales = new Vector3[page.childCount]
        };
        for (int i = 0; i < items.rects.Length; i++)
            items.rects[i] = page.GetChild(i) as RectTransform;
        System.Array.Sort(items.rects, (a, b) =>
            b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        for (int i = 0; i < items.rects.Length; i++)
        {
            items.positions[i] = items.rects[i].anchoredPosition;
            items.scales[i] = items.rects[i].localScale;
        }
        return items;
    }

    private void AnimateWave(PageItems items, float entryX, Sequence sequence)
    {
        if (items == null) return;
        for (int i = 0; i < items.rects.Length; i++)
        {
            RectTransform item = items.rects[i];
            item.anchoredPosition = items.positions[i] + Vector2.right * entryX;
            item.localScale = items.scales[i];
            float delay = i * 0.035f;
            sequence.Insert(delay, item.DOAnchorPos(items.positions[i], slideSeconds)
                .SetEase(Ease.OutCubic));
        }
    }

    public void Exit()
    {
        if (locked || root == null || blackFade == null) return;
        locked = true;
        root.interactable = false;
        root.blocksRaycasts = false;
        motion?.Kill();
        blackFade.blocksRaycasts = true;
        blackFade.DOKill();
        motion = DOTween.Sequence().SetUpdate(true)
            .Append(blackFade.DOFade(1f, fadeSeconds))
            .OnComplete(() =>
            {
#if UNITY_EDITOR || UNITY_WEBGL
#if UNITY_WEBGL && !UNITY_EDITOR
                if (statusText != null) statusText.text = "Para sair, feche a aba do navegador.";
#else
                if (statusText != null) statusText.text = "SAIR funciona na versão publicada.";
#endif
                blackFade.DOFade(0f, fadeSeconds).SetUpdate(true).OnComplete(() =>
                {
                    blackFade.blocksRaycasts = false;
                    root.interactable = root.blocksRaycasts = true;
                    locked = false;
                });
#else
                Application.Quit();
#endif
            });
    }
}
