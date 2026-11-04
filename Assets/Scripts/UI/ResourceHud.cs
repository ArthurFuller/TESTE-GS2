using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum ResourceKind { Wood, Fish, Corn, Pumpkin, Jerimum, Food, Coins, Milk, Eggs }

public sealed class ResourceHud : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text woodText;
    [SerializeField] private Text fishText;
    [SerializeField] private Text cassavaText;
    [SerializeField] private Text pumpkinText;
    [SerializeField] private Text cornText;
    [SerializeField] private Text foodText;
    [SerializeField] private Text coinText;
    [SerializeField] private Text animalText;
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
        if (resources != null) resources.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
        DOTween.Kill(this);
        System.Array.Clear(inFlight, 0, inFlight.Length);
    }

    private void OnDestroy()
    {
        if (circleSprite != null) Destroy(circleSprite);
        if (circleTexture != null) Destroy(circleTexture);
    }

    public void FlyFromWorld(ResourceKind kind, int amount, Vector3 origin)
    {
        Camera worldCamera = Camera.main;
        Vector2 screen = worldCamera != null ? (Vector2)worldCamera.WorldToScreenPoint(origin) :
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        FlyFromScreen(kind, amount, screen);
    }

    public void FlyFromRect(ResourceKind kind, int amount, RectTransform origin)
    {
        if (origin != null)
            FlyFromScreen(kind, amount, RectTransformUtility.WorldToScreenPoint(UiCamera,
                origin.TransformPoint(origin.rect.center)));
    }

    public void FlyFromScreen(ResourceKind kind, int amount, Vector2 screenOrigin)
    {
        if (amount <= 0 || canvasRect == null || !isActiveAndEnabled) return;
        Text target = TargetFor(kind);
        if (target == null) return;
        inFlight[(int)kind] += amount;
        Refresh();
        Vector2 origin = CanvasPoint(screenOrigin);
        Vector3 targetCenter = target.rectTransform.rect.center;
        if (kind == ResourceKind.Milk || kind == ResourceKind.Eggs)
            targetCenter.x += target.rectTransform.rect.width *
                (kind == ResourceKind.Milk ? -0.25f : 0.25f);
        Vector2 destination = CanvasPoint(RectTransformUtility.WorldToScreenPoint(UiCamera,
            target.rectTransform.TransformPoint(targetCenter)));
        int count = Mathf.Min(amount, Mathf.Max(1, maximumFlyingIcons));
        for (int i = 0; i < count; i++)
            CreateIcon(kind, i, count, origin, destination, target,
                amount / count + (i < amount % count ? 1 : 0));
    }

    private Vector2 CanvasPoint(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, UiCamera,
            out Vector2 point);
        return point;
    }

    private void CreateIcon(ResourceKind kind, int index, int count, Vector2 origin,
        Vector2 destination, Text target, int credited)
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
        flight.Append(rect.DOAnchorPos(destination, flightDuration).SetEase(Ease.InCubic))
            .Join(rect.DOScale(0.45f, flightDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                inFlight[(int)kind] = Mathf.Max(0, inFlight[(int)kind] - credited);
                Refresh();
                target.rectTransform.DOKill();
                target.rectTransform.localScale = Vector3.one;
                target.rectTransform.DOPunchScale(Vector3.one * 0.12f, 0.2f, 2);
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
        ResourceKind.Pumpkin => pumpkinText,
        ResourceKind.Jerimum => cassavaText,
        ResourceKind.Food => foodText,
        ResourceKind.Coins => coinText,
        _ => animalText
    };

    private int Shown(ResourceKind kind, int stored) => Mathf.Max(0, stored - inFlight[(int)kind]);

    private void Refresh()
    {
        if (resources == null) return;
        if (woodText != null) woodText.text = Shown(ResourceKind.Wood, resources.Wood).ToString();
        if (fishText != null) fishText.text = Shown(ResourceKind.Fish, resources.Fish).ToString();
        if (cassavaText != null) cassavaText.text = Shown(ResourceKind.Jerimum, resources.Jerimum).ToString();
        if (pumpkinText != null) pumpkinText.text = Shown(ResourceKind.Pumpkin, resources.Pumpkin).ToString();
        if (cornText != null) cornText.text = Shown(ResourceKind.Corn, resources.Corn).ToString();
        if (foodText != null) foodText.text = "Comida: " + Shown(ResourceKind.Food, resources.Food);
        if (coinText != null) coinText.text = "Dinheiro: " + Shown(ResourceKind.Coins, resources.Coins);
        if (animalText != null) animalText.text = "Leite: " + Shown(ResourceKind.Milk, resources.Milk) +
            "     Ovos: " + Shown(ResourceKind.Eggs, resources.Eggs);
    }
}
