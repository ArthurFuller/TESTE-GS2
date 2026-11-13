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
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite milkSprite;
    [SerializeField] private Sprite eggSprite;
    [SerializeField, Min(1)] private int maximumFlyingIcons = 64;
    [SerializeField, Min(0.1f)] private float flightDuration = 0.55f;
    [SerializeField, Min(0f)] private float iconInterval = 0.055f;

    private readonly int[] inFlight = new int[9];
    private Canvas canvas;
    private RectTransform canvasRect;
    private Texture2D circleTexture;
    private Sprite circleSprite;
    private readonly int[] displayed = new int[9];
    private readonly Tween[] counterTweens = new Tween[9];
    private readonly Tween[] counterPulses = new Tween[9];
    private readonly Color[] textColors = new Color[9];
    private readonly Transform[] counterRoots = new Transform[9];
    private readonly Vector3[] counterRootScales = new Vector3[9];
    private readonly Color[] iconColors = new Color[9];
    private readonly Material[] iconFlashMaterials = new Material[9];
    private readonly Material[] originalIconMaterials = new Material[9];
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private ResourceCounterHover counterHover;
    private TopBarAutoHide topBar;

    public bool HasFlyingResources
    {
        get
        {
            foreach (int amount in inFlight)
                if (amount > 0) return true;
            return false;
        }
    }

    private Camera UiCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
        ? canvas.worldCamera : null;

    private void Awake()
    {
        topBar = GetComponent<TopBarAutoHide>();
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null) canvasRect = canvas.transform as RectTransform;
    }

    private void OnEnable()
    {
        counterHover = GetComponent<ResourceCounterHover>();
        Shader flashShader = Resources.Load<Shader>("ResourceIconFlash");
        for (int i = 0; i < displayed.Length; i++)
        {
            counterPulses[i]?.Kill();
            ResourceKind kind = (ResourceKind)i;
            displayed[i] = Stored(kind);
            Text label = TargetFor(kind);
            Graphic icon = counterHover != null ? counterHover.IconFor(kind) : null;
            if (label != null) textColors[i] = label.color;
            if (label != null && icon != null &&
                label.rectTransform.parent == icon.rectTransform.parent)
            {
                counterRoots[i] = label.rectTransform.parent;
                counterRootScales[i] = counterRoots[i].localScale;
            }
            if (icon != null)
            {
                iconColors[i] = icon.color;
                if (flashShader != null)
                {
                    originalIconMaterials[i] = icon.material;
                    iconFlashMaterials[i] = new Material(flashShader)
                        { hideFlags = HideFlags.DontSave };
                    icon.material = iconFlashMaterials[i];
                }
            }
        }
        if (resources != null) resources.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (resources != null) resources.Changed -= Refresh;
        DOTween.Kill(this);
        for (int i = 0; i < displayed.Length; i++)
        {
            ResourceKind kind = (ResourceKind)i;
            Text label = TargetFor(kind);
            Graphic icon = counterHover != null ? counterHover.IconFor(kind) : null;
            if (label != null)
            {
                label.DOKill();
                label.color = textColors[i];
            }
            if (icon != null)
            {
                icon.DOKill();
                icon.color = iconColors[i];
            }
            if (iconFlashMaterials[i] != null)
            {
                if (icon != null) icon.material = originalIconMaterials[i];
                Destroy(iconFlashMaterials[i]);
                iconFlashMaterials[i] = null;
            }
            if (counterRoots[i] != null)
                counterRoots[i].localScale = counterRootScales[i];
        }
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
        Camera worldCamera = Camera.main;
        Vector2 screen = worldCamera != null ?
            (Vector2)worldCamera.WorldToScreenPoint(worldOrigin) :
            new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        DropOneAtScreen(batch, screen, null);
    }

    public void DropOneAtScreen(DropBatch batch, Vector2 screenOrigin, Vector2? screenLanding)
    {
        if (batch == null || batch.Released || canvasRect == null) return;
        Vector2 origin = CanvasPoint(screenOrigin);
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
        Vector2 landing = screenLanding.HasValue
            ? CanvasPoint(screenLanding.Value) + new Vector2(Mathf.Cos(angle) * 16f,
                Mathf.Sin(angle) * 9f)
            : origin + new Vector2(Mathf.Cos(angle) * 18f,
                -35f + Mathf.Sin(angle) * 7f);
        rect.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetId(this);
        rect.DOAnchorPos(landing, screenLanding.HasValue ? 0.4f : 0.28f)
            .SetEase(screenLanding.HasValue ? Ease.OutCubic : Ease.OutBounce).SetId(this);
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
        topBar?.RevealForResources();
        for (int i = 0; i < batch.Icons.Count; i++)
        {
            RectTransform rect = batch.Icons[i];
            if (rect == null)
            {
                CreditArrival(batch.Kind, 1);
                if (--pending == 0) onAllArrived?.Invoke();
                continue;
            }
            rect.DOKill();
            Sequence flight = DOTween.Sequence().SetId(this);
            flight.AppendInterval(i * iconInterval);
            flight.Append(FollowCounter(rect, target));
            flight.Join(rect.DOScale(0.4f, flightDuration).SetEase(Ease.InQuad));
            flight.OnComplete(() =>
            {
                CreditArrival(batch.Kind, 1);
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
        // A mudança no banco pode ter agendado a contagem antes de os ícones nascerem.
        counterTweens[(int)kind]?.Kill();
        counterTweens[(int)kind] = null;
        Refresh();
        if (topBar != null)
            pauseSeconds = Mathf.Max(pauseSeconds, topBar.PrepareResourceFeedback());
        Vector2 origin = CanvasPoint(screenOrigin);
        int count = Mathf.Min(amount, Mathf.Max(1, maximumFlyingIcons));
        int pending = count;
        for (int i = 0; i < count; i++)
            CreateIcon(kind, i, count, origin, target,
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

    private Tween FollowCounter(RectTransform icon, Text target)
    {
        Vector2 start = icon.anchoredPosition;
        float progress = 0f;
        return DOTween.To(() => progress, value =>
        {
            progress = value;
            if (icon == null || target == null || canvasRect == null) return;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(UiCamera,
                target.rectTransform.TransformPoint(target.rectTransform.rect.center));
            icon.anchoredPosition = Vector2.LerpUnclamped(start, CanvasPoint(screen), value);
        }, 1f, flightDuration).SetEase(Ease.InCubic);
    }

    private void CreateIcon(ResourceKind kind, int index, int count, Vector2 origin,
        Text target, int credited, float pauseSeconds,
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
        flight.Append(FollowCounter(rect, target))
            .Join(rect.DOScale(0.45f, flightDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                CreditArrival(kind, credited);
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
        ResourceKind.Coins => coinSprite,
        ResourceKind.Milk => milkSprite,
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

    private int Stored(ResourceKind kind) => resources == null ? 0 : kind switch
    {
        ResourceKind.Wood => resources.Wood,
        ResourceKind.Fish => resources.Fish,
        ResourceKind.Corn => resources.Corn,
        ResourceKind.Pumpkin => resources.Pumpkin,
        ResourceKind.Jerimum => resources.Jerimum,
        ResourceKind.Food => resources.Food,
        ResourceKind.Coins => resources.Coins,
        ResourceKind.Milk => resources.Milk,
        ResourceKind.Eggs => resources.Eggs,
        _ => 0
    };

    private int Shown(ResourceKind kind) => Mathf.Max(0, Stored(kind) - inFlight[(int)kind]);

    // The bank already owns the resource. Reveal each portion exactly when its icon lands.
    private void CreditArrival(ResourceKind kind, int amount)
    {
        int index = (int)kind;
        inFlight[index] = Mathf.Max(0, inFlight[index] - amount);
        int next = Mathf.Min(Shown(kind), displayed[index] + amount);
        if (next > displayed[index])
        {
            displayed[index] = next;
            DrawCounter(kind);
            // Frequent arrivals update the number individually without restarting every pulse.
            if (counterPulses[index] == null || !counterPulses[index].IsActive())
                FlashCounter(kind, 1);
        }
        Refresh();
    }

    private void StepCounter(ResourceKind kind)
    {
        int index = (int)kind;
        int target = Shown(kind);
        if (displayed[index] == target) { counterTweens[index] = null; return; }
        int direction = target > displayed[index] ? 1 : -1;
        displayed[index] += direction;
        DrawCounter(kind);
        FlashCounter(kind, direction);
        counterTweens[index] = DOVirtual.DelayedCall(0.075f, () => StepCounter(kind), false)
            .SetTarget(this);
    }

    private void FlashCounter(ResourceKind kind, int direction)
    {
        int index = (int)kind;
        Color flash = direction < 0 ? new Color(0.95f, 0.14f, 0.14f) : Color.white;
        Text label = TargetFor(kind);
        Graphic icon = counterHover != null ? counterHover.IconFor(kind) : null;
        counterPulses[index]?.Kill();
        Sequence pulse = DOTween.Sequence().SetTarget(this);
        counterPulses[index] = pulse;
        if (counterRoots[index] != null)
        {
            counterRoots[index].localScale = counterRootScales[index];
            pulse.Insert(0f, counterRoots[index]
                .DOScale(counterRootScales[index] * 1.08f, 0.09f)
                .SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad));
        }
        if (label != null)
        {
            label.DOKill();
            label.color = textColors[index];
            pulse.Insert(0f, label.DOColor(flash, 0.055f));
            pulse.Insert(0.055f, label.DOColor(textColors[index], 0.12f));
        }
        if (icon != null)
        {
            icon.DOKill();
            icon.color = iconColors[index];
            Material material = iconFlashMaterials[index];
            if (material != null)
            {
                material.SetColor(FlashColorId, flash);
                material.SetFloat(FlashId, 0f);
                pulse.Insert(0f, DOTween.To(() => material.GetFloat(FlashId),
                    value => material.SetFloat(FlashId, value), 1f, 0.055f));
                pulse.Insert(0.055f, DOTween.To(() => material.GetFloat(FlashId),
                    value => material.SetFloat(FlashId, value), 0f, 0.12f));
            }
            else
            {
                Color fallback = new Color(flash.r, flash.g, flash.b, iconColors[index].a);
                pulse.Insert(0f, icon.DOColor(fallback, 0.055f));
                pulse.Insert(0.055f, icon.DOColor(iconColors[index], 0.12f));
            }
        }
    }

    private void DrawCounter(ResourceKind kind)
    {
        Text label = TargetFor(kind);
        if (label == null) return;
        if (eggText == null && (kind == ResourceKind.Milk || kind == ResourceKind.Eggs))
            label.text = displayed[(int)ResourceKind.Milk] + "  Ovos: " +
                displayed[(int)ResourceKind.Eggs];
        else label.text = displayed[(int)kind].ToString();
    }

    private void Refresh()
    {
        if (resources == null) return;
        bool changed = false;
        for (int i = 0; i < displayed.Length; i++)
            if (displayed[i] != Shown((ResourceKind)i)) { changed = true; break; }

        // The bank updates immediately; only its visual counting waits for the HUD.
        float revealDelay = changed && topBar != null ? topBar.PrepareResourceFeedback() : 0f;
        float countDelay = Mathf.Max(0.075f, revealDelay + 0.05f);
        for (int i = 0; i < displayed.Length; i++)
        {
            ResourceKind kind = (ResourceKind)i;
            DrawCounter(kind);
            if (displayed[i] == Shown(kind)) continue;
            if (revealDelay > 0f) counterTweens[i]?.Kill();
            if (counterTweens[i] == null || !counterTweens[i].IsActive())
                counterTweens[i] = DOVirtual.DelayedCall(countDelay, () => StepCounter(kind), false)
                    .SetTarget(this);
        }
    }
}
