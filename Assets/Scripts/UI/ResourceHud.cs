using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum ResourceKind { Wood, Fish, Corn, Pumpkin, Jerimum, Food, Coins, Milk, Eggs }

public sealed class ResourceHud : MonoBehaviour
{
    public sealed class DropBatch
    {
        internal readonly ResourceKind Kind;
        internal readonly List<RectTransform> Icons = new();
        internal bool Released;
        internal DropBatch(ResourceKind kind) { Kind = kind; }
    }

    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text woodText;
    [SerializeField] private Text fishText;
    [SerializeField] private Text cassavaText;
    [SerializeField] private Text pumpkinText;
    [SerializeField] private Text cornText;
    [SerializeField] private Text foodText;
    [SerializeField] private Text coinText;
    [SerializeField] private Text animalText;
    [SerializeField] private Text eggText;
    [Header("Ícones em voo")]
    [SerializeField] private Sprite woodSprite;
    [SerializeField] private Sprite fishSprite;
    [SerializeField] private Sprite cornSprite;
    [SerializeField] private Sprite pumpkinSprite;
    [SerializeField] private Sprite jerimumSprite;
    [SerializeField] private Sprite foodSprite;
    [SerializeField] private Sprite eggSprite;
    [SerializeField, Min(1)] private int maximumFlyingIcons = 64;
    [SerializeField, Min(0.1f)] private float flightDuration = 0.55f;
    [SerializeField, Min(0f)] private float iconInterval = 0.055f;

    private readonly int[] inFlight = new int[9];
    private Canvas canvas;
    private RectTransform canvasRect;
    private Texture2D circleTexture;
    private Sprite circleSprite;
    private int observedCoins;
    private int coinLossRemaining;
    private Tween coinLossTween;
    private Tween coinFlashTween;
    private Color normalCoinColor;

    private Camera UiCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
        ? canvas.worldCamera : null;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null) canvasRect = canvas.transform as RectTransform;
    }

    private void OnEnable()
    {
        observedCoins = resources != null ? resources.Coins : 0;
        if (coinText != null) normalCoinColor = coinText.color;
        if (resources != null) resources.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
        DOTween.Kill(this);
        coinLossTween?.Kill();
        coinFlashTween?.Kill();
        if (coinText != null) coinText.color = normalCoinColor;
        coinLossRemaining = 0;
        System.Array.Clear(inFlight, 0, inFlight.Length);
    }

    private void OnDestroy()
    {
        if (circleSprite != null) Destroy(circleSprite);
        if (circleTexture != null) Destroy(circleTexture);
    }

    public void FlyFromWorld(ResourceKind kind, int amount, Vector3 origin,
        float pauseSeconds = 0f, System.Action onAllArrived = null)
    {
        Camera worldCamera = Camera.main;
        Vector2 screen = worldCamera != null ? (Vector2)worldCamera.WorldToScreenPoint(origin) :
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        FlyFromScreen(kind, amount, screen, pauseSeconds, onAllArrived);
    }

    public void FlyFromRect(ResourceKind kind, int amount, RectTransform origin)
    {
        if (origin != null)
            FlyFromScreen(kind, amount, RectTransformUtility.WorldToScreenPoint(UiCamera,
                origin.TransformPoint(origin.rect.center)));
    }

    public DropBatch CreateDropBatch(ResourceKind kind) => new DropBatch(kind);

    public void DropOne(DropBatch batch, Vector3 worldOrigin)
    {
        if (batch == null || batch.Released || canvasRect == null) return;
        Camera worldCamera = Camera.main;
        Vector2 screen = worldCamera != null ?
            (Vector2)worldCamera.WorldToScreenPoint(worldOrigin) :
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 origin = CanvasPoint(screen);
        int index = batch.Icons.Count;
        var icon = new GameObject("Recurso dropado - " + batch.Kind,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)icon.transform;
        rect.SetParent(canvasRect, false);
        rect.SetAsLastSibling();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(34f, 34f);
        rect.anchoredPosition = origin;
        rect.localScale = Vector3.zero;
        Image image = icon.GetComponent<Image>();
        image.sprite = SpriteFor(batch.Kind) ?? CircleSprite();
        image.preserveAspect = true;
        image.raycastTarget = false;
        batch.Icons.Add(rect);
        float angle = index * 2.39996f;
        Vector2 landing = origin + new Vector2(Mathf.Cos(angle) * 18f,
            -35f + Mathf.Sin(angle) * 7f);
        rect.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetId(this);
        rect.DOAnchorPos(landing, 0.28f).SetEase(Ease.OutBounce).SetId(this);
    }

    public void FlyDrops(DropBatch batch, System.Action onAllArrived = null)
    {
        if (batch == null || batch.Released || batch.Icons.Count == 0)
        {
            onAllArrived?.Invoke();
            return;
        }
        batch.Released = true;
        Text target = TargetFor(batch.Kind);
        if (target == null)
        {
            foreach (RectTransform icon in batch.Icons)
                if (icon != null) Destroy(icon.gameObject);
            onAllArrived?.Invoke();
            return;
        }
        int pending = batch.Icons.Count;
        inFlight[(int)batch.Kind] += pending;
        Refresh();
        Vector2 destination = CanvasPoint(RectTransformUtility.WorldToScreenPoint(UiCamera,
            target.rectTransform.TransformPoint(target.rectTransform.rect.center)));
        for (int i = 0; i < batch.Icons.Count; i++)
        {
            RectTransform rect = batch.Icons[i];
            if (rect == null)
            {
                if (--pending == 0) onAllArrived?.Invoke();
                continue;
            }
            rect.DOKill();
            Sequence flight = DOTween.Sequence().SetId(this);
            flight.AppendInterval(i * iconInterval);
            flight.Append(rect.DOAnchorPos(destination, flightDuration).SetEase(Ease.InCubic));
            flight.Join(rect.DOScale(0.4f, flightDuration).SetEase(Ease.InQuad));
            flight.OnComplete(() =>
            {
                inFlight[(int)batch.Kind] = Mathf.Max(0, inFlight[(int)batch.Kind] - 1);
                Refresh();
                if (--pending == 0) onAllArrived?.Invoke();
            });
            flight.OnKill(() => { if (rect != null) Destroy(rect.gameObject); });
        }
    }

    public void FlyFromScreen(ResourceKind kind, int amount, Vector2 screenOrigin,
        float pauseSeconds = 0f, System.Action onAllArrived = null)
    {
        if (amount <= 0 || canvasRect == null || !isActiveAndEnabled)
        {
            onAllArrived?.Invoke();
            return;
        }
        Text target = TargetFor(kind);
        if (target == null)
        {
            onAllArrived?.Invoke();
            return;
        }
        inFlight[(int)kind] += amount;
        Refresh();
        Vector2 origin = CanvasPoint(screenOrigin);
        Vector3 targetCenter = target.rectTransform.rect.center;
        Vector2 destination = CanvasPoint(RectTransformUtility.WorldToScreenPoint(UiCamera,
            target.rectTransform.TransformPoint(targetCenter)));
        int count = Mathf.Min(amount, Mathf.Max(1, maximumFlyingIcons));
        int pending = count;
        for (int i = 0; i < count; i++)
            CreateIcon(kind, i, count, origin, destination, target,
                amount / count + (i < amount % count ? 1 : 0), pauseSeconds, () =>
                {
                    if (--pending == 0) onAllArrived?.Invoke();
                });
    }

    private Vector2 CanvasPoint(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, UiCamera,
            out Vector2 point);
        return point;
    }

    private void CreateIcon(ResourceKind kind, int index, int count, Vector2 origin,
        Vector2 destination, Text target, int credited, float pauseSeconds,
        System.Action onArrived)
    {
        var icon = new GameObject("Recurso em voo - " + kind, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = (RectTransform)icon.transform;
        rect.SetParent(canvasRect, false);
        rect.SetAsLastSibling();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(32f, 32f);
        float angle = index * 2.39996f;
        float radius = 4f + 11f * Mathf.Sqrt((index + 0.5f) / count);
        rect.anchoredPosition = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        rect.localScale = Vector3.zero;
        Image image = icon.GetComponent<Image>();
        image.sprite = SpriteFor(kind) ?? CircleSprite();
        image.color = kind == ResourceKind.Coins ? new Color(1f, 0.74f, 0.16f) :
            kind == ResourceKind.Milk ? new Color(0.83f, 0.95f, 1f) : Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        Sequence flight = DOTween.Sequence().SetId(this);
        if (kind == ResourceKind.Coins)
            flight.Append(rect.DOScale(1f, 0.15f).SetEase(Ease.OutBack))
                .AppendInterval(0.08f + index * iconInterval);
        else
            flight.AppendInterval(index * iconInterval)
                .Append(rect.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
        if (pauseSeconds > 0f) flight.AppendInterval(pauseSeconds);
        flight.Append(rect.DOAnchorPos(destination, flightDuration).SetEase(Ease.InCubic))
            .Join(rect.DOScale(0.45f, flightDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                inFlight[(int)kind] = Mathf.Max(0, inFlight[(int)kind] - credited);
                Refresh();
                onArrived?.Invoke();
            })
            .OnKill(() => { if (icon != null) Destroy(icon); });
    }

    private Sprite SpriteFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Wood => woodSprite,
        ResourceKind.Fish => fishSprite,
        ResourceKind.Corn => cornSprite,
        ResourceKind.Pumpkin => pumpkinSprite,
        ResourceKind.Jerimum => jerimumSprite,
        ResourceKind.Food => foodSprite,
        ResourceKind.Eggs => eggSprite,
        _ => null
    };

    private Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        circleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        circleTexture.filterMode = FilterMode.Bilinear;
        var pixels = new Color[1024];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(16f - distance));
            }
        circleTexture.SetPixels(pixels);
        circleTexture.Apply();
        circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, 32, 32),
            new Vector2(0.5f, 0.5f), 32f);
        return circleSprite;
    }

    private Text TargetFor(ResourceKind kind) => kind switch
    {
        ResourceKind.Wood => woodText,
        ResourceKind.Fish => fishText,
        ResourceKind.Corn => cornText,
        ResourceKind.Pumpkin => cassavaText,
        ResourceKind.Jerimum => pumpkinText,
        ResourceKind.Food => foodText,
        ResourceKind.Coins => coinText,
        ResourceKind.Eggs => eggText != null ? eggText : animalText,
        _ => animalText
    };

    private int Shown(ResourceKind kind, int stored) => Mathf.Max(0, stored - inFlight[(int)kind]);

    private void StepCoinLoss()
    {
        if (coinLossRemaining <= 0)
        {
            coinFlashTween?.Kill();
            if (coinText != null) coinText.color = normalCoinColor;
            return;
        }
        coinLossRemaining--;
        Refresh();
        coinLossTween = DOVirtual.DelayedCall(0.075f, StepCoinLoss).SetTarget(this);
    }

    private void Refresh()
    {
        if (resources == null) return;
        if (resources.Coins < observedCoins && coinText != null)
        {
            coinLossRemaining += observedCoins - resources.Coins;
            if (coinLossTween == null || !coinLossTween.IsActive())
            {
                coinFlashTween?.Kill();
                coinText.color = normalCoinColor;
                coinFlashTween = coinText.DOColor(new Color(0.95f, 0.14f, 0.14f), 0.09f)
                    .SetLoops(-1, LoopType.Yoyo);
                coinLossTween = DOVirtual.DelayedCall(0.075f, StepCoinLoss).SetTarget(this);
            }
        }
        observedCoins = resources.Coins;
        if (woodText != null) woodText.text = Shown(ResourceKind.Wood, resources.Wood).ToString();
        if (fishText != null) fishText.text = Shown(ResourceKind.Fish, resources.Fish).ToString();
        if (cassavaText != null) cassavaText.text = Shown(ResourceKind.Pumpkin, resources.Pumpkin).ToString();
        if (pumpkinText != null) pumpkinText.text = Shown(ResourceKind.Jerimum, resources.Jerimum).ToString();
        if (cornText != null) cornText.text = Shown(ResourceKind.Corn, resources.Corn).ToString();
        if (foodText != null) foodText.text = "Comida: " + Shown(ResourceKind.Food, resources.Food);
        if (coinText != null) coinText.text = "Dinheiro: " +
            Mathf.Max(0, Shown(ResourceKind.Coins, resources.Coins) + coinLossRemaining);
        if (animalText != null) animalText.text = "Leite: " + Shown(ResourceKind.Milk, resources.Milk) +
            (eggText == null ? "     Ovos: " + Shown(ResourceKind.Eggs, resources.Eggs) : "");
        if (eggText != null) eggText.text = Shown(ResourceKind.Eggs, resources.Eggs).ToString();
    }
}
