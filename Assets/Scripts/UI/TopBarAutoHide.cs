using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class TopBarAutoHide : MonoBehaviour
{
    [SerializeField] private RectTransform bar;
    [SerializeField] private RectTransform background;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private DayTimeline timeline;
    [SerializeField] private ActiveMissionsPanel missionsPanel;
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField] private Vector2 openPosition = Vector2.zero;
    [SerializeField] private Vector2 closedPosition = new Vector2(0f, 178f);
    [SerializeField, InspectorName("Posição ao visualizar um dia")]
    private Vector2 timelineDayPosition = new Vector2(0f, 90f);
    [SerializeField, Min(0.1f)] private float idleSeconds = 10f;
    [SerializeField, Min(0.01f)] private float slideDuration = 0.36f;
    [SerializeField, Min(0f)] private float waveDelay = 0.055f;
    [SerializeField, Min(0f)] private float widgetOffset = 24f;

    private RectTransform[] widgets;
    private Vector2[] widgetPositions;
    private Camera uiCamera;
    private ResourceHud resourceHud;
    private Sequence motion;
    private bool open;
    private bool dayActive;
    private bool wavePendingForDay = true;
    private bool waitForPointerExit;
    private bool timelineDayOpen;
    private bool closingAfterTimeline;
    private bool paused;
    private float lastHoverTime;

    private void Awake()
    {
        if (bar == null) bar = (RectTransform)transform;
        resourceHud = GetComponent<ResourceHud>();
        var ordered = new List<RectTransform>(bar.childCount);
        for (int i = 0; i < bar.childCount; i++)
        {
            RectTransform child = bar.GetChild(i) as RectTransform;
            if (child != null && child != background && child.gameObject.activeSelf)
                ordered.Add(child);
        }
        ordered.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
        widgets = ordered.ToArray();
        widgetPositions = new Vector2[widgets.Length];
        for (int i = 0; i < widgets.Length; i++)
            widgetPositions[i] = widgets[i].anchoredPosition;
        Canvas canvas = GetComponentInParent<Canvas>();
        uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
    }

    private void OnEnable()
    {
        if (dayCycle != null)
        {
            dayCycle.DayChanged += OpenForNewDay;
            dayCycle.DayClosing += CloseForDayEnd;
            dayCycle.DayClosingCancelled += OpenForNewDay;
        }
        wavePendingForDay = true;
        SetOpen(false, true);
    }

    private void OnDisable()
    {
        if (dayCycle != null)
        {
            dayCycle.DayChanged -= OpenForNewDay;
            dayCycle.DayClosing -= CloseForDayEnd;
            dayCycle.DayClosingCancelled -= OpenForNewDay;
        }
        motion?.Kill();
    }

    private void Update()
    {
        if (paused || !dayActive) return;

        // Dragging takes precedence over hover, open panels and resource flights.
        if (hand != null && hand.IsDraggingCard)
        {
            lastHoverTime = Time.unscaledTime;
            if (open) SetOpen(false, false);
            return;
        }
        if (closingAfterTimeline) return;

        // Target selection has priority over the HUD, including incoming resources.
        if (hand != null && hand.IsSelectionLocked)
        {
            if (open)
            {
                missionsPanel?.Close();
                CloseAfterTimeline();
            }
            return;
        }

        // Keep the destination visible for the entire flight, including while paused.
        if (resourceHud != null && resourceHud.HasFlyingResources)
        {
            RevealForResources();
            return;
        }

        if (Mouse.current == null) return;
        Vector2 pointer = Mouse.current.position.ReadValue();
        bool overBar = RectTransformUtility.RectangleContainsScreenPoint(bar, pointer, uiCamera);
        if (waitForPointerExit)
        {
            if (overBar) return;
            waitForPointerExit = false;
        }
        if ((timeline != null && timeline.IsOpen) ||
            (missionsPanel != null && missionsPanel.IsOpen))
        {
            lastHoverTime = Time.unscaledTime;
            if (!open) SetOpen(true, false);
            return;
        }
        if (overBar)
        {
            lastHoverTime = Time.unscaledTime;
            if (!open) SetOpen(true, false);
        }
        else if (open && Time.unscaledTime - lastHoverTime >= idleSeconds)
            SetOpen(false, false);
    }

    public void RevealForResources() => PrepareResourceFeedback();

    // The day counter is the only manual close target for the top bar.
    public void CloseFromDayCounter()
    {
        if (paused || !dayActive || !open || closingAfterTimeline ||
            (resourceHud != null && resourceHud.HasFlyingResources) ||
            (hand != null && (hand.IsDraggingCard || hand.IsSelectionLocked))) return;

        missionsPanel?.Close();
        waitForPointerExit = true;
        CloseAfterTimeline();
    }

    // Returns the time remaining before the counter area is fully visible.
    public float PrepareResourceFeedback()
    {
        if (paused || !dayActive || closingAfterTimeline || timelineDayOpen ||
            (timeline != null && timeline.IsOpen) ||
            (hand != null && (hand.IsSelectionLocked || hand.IsDraggingCard))) return 0f;

        lastHoverTime = Time.unscaledTime;
        if (!open) SetOpen(true, false);
        if (bar == null || (bar.anchoredPosition - openPosition).sqrMagnitude < 0.01f)
            return 0f;
        return motion != null && motion.IsActive()
            ? Mathf.Max(0f, slideDuration - motion.Elapsed()) : 0f;
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (paused) motion?.Pause();
        else
        {
            motion?.Play();
            lastHoverTime = Time.unscaledTime;
        }
    }

    private void OpenForNewDay()
    {
        wavePendingForDay = true;
        dayActive = true;
        waitForPointerExit = false;
        lastHoverTime = Time.unscaledTime;
        SetOpen(true, false);
    }

    private void CloseForDayEnd()
    {
        dayActive = false;
        CloseAfterTimeline();
    }

    public void PreviewCurrentPosition()
    {
        if (!Application.isPlaying) return;
        motion?.Complete();
        MoveBar(timelineDayOpen ? timelineDayPosition :
            open ? openPosition : closedPosition, true, null);
    }

    public void MoveToTimelineDay(System.Action onComplete)
    {
        timelineDayOpen = true;
        open = true;
        lastHoverTime = Time.unscaledTime;
        MoveBar(timelineDayPosition, false, onComplete);
    }

    public void ReturnFromTimelineDay(System.Action onComplete)
    {
        timelineDayOpen = false;
        lastHoverTime = Time.unscaledTime;
        MoveBar(openPosition, false, onComplete);
    }

    private void CloseAfterTimeline()
    {
        if (timeline != null && timeline.IsOpen)
        {
            closingAfterTimeline = true;
            timeline.ClosePanel(() =>
            {
                closingAfterTimeline = false;
                SetOpen(false, false);
            });
            return;
        }
        SetOpen(false, false);
    }

    private void SetOpen(bool show, bool immediate)
    {
        if (paused) return;
        bool playWave = show && wavePendingForDay && !immediate;
        if (open == show && !immediate && !playWave) return;
        open = show;
        timelineDayOpen = false;
        lastHoverTime = Time.unscaledTime;
        motion?.Kill();
        if (bar == null) return;

        Vector2 destination = show ? openPosition : closedPosition;
        float exitDistance = bar.rect.height +
            Mathf.Abs(closedPosition.y - openPosition.y) + widgetOffset;

        // During ordinary opening/closing, widgets remain fixed to their parent HUD.
        // Only the first opening of each day receives staggered entrance tweens.
        for (int i = 0; i < widgets.Length; i++)
            widgets[i].anchoredPosition = widgetPositions[i] +
                ((playWave || (immediate && !show && wavePendingForDay))
                    ? Vector2.up * exitDistance : Vector2.zero);

        MoveBar(destination, immediate, null);
        if (!playWave) return;

        wavePendingForDay = false;
        for (int i = 0; i < widgets.Length; i++)
        {
            if (!widgets[i].gameObject.activeSelf) continue;
            motion.Insert(slideDuration + i * waveDelay,
                widgets[i].DOAnchorPos(widgetPositions[i], slideDuration * 0.65f)
                    .SetEase(Ease.OutCubic));
        }
    }

    private void MoveBar(Vector2 destination, bool immediate, System.Action onComplete)
    {
        motion?.Kill();
        if (bar == null) return;
        bool separateArt = background != null && background.parent != bar;
        float artScale = separateArt && bar.rect.width > 0f
            ? background.rect.width / bar.rect.width : 1f;
        if (immediate)
        {
            bar.anchoredPosition = destination;
            if (separateArt) background.anchoredPosition = destination * artScale;
            timeline?.FollowHud(bar.anchoredPosition - openPosition);
            onComplete?.Invoke();
            return;
        }
        motion = DOTween.Sequence().SetUpdate(true);
        motion.Insert(0f, bar.DOAnchorPos(destination, slideDuration).SetEase(Ease.InOutCubic));
        if (separateArt)
            motion.Insert(0f, background.DOAnchorPos(destination * artScale, slideDuration)
                .SetEase(Ease.InOutCubic));
        motion.OnUpdate(() => timeline?.FollowHud(bar.anchoredPosition - openPosition));
        motion.OnComplete(() => onComplete?.Invoke());
    }
}
