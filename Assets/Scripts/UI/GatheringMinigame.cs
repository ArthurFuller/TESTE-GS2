using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GatheringMinigame : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform marker;
    [SerializeField] private RectTransform greenTarget;
    [SerializeField] private Text title;
    [SerializeField] private Button actionButton;
    [SerializeField] private RectTransform topHud;
    [SerializeField] private RectTransform finishDayButton;
    [Header("Coleta de madeira")]
    [SerializeField, Min(0.1f)] private float markerCrossingDuration = 2f;
    [SerializeField, Range(0.3f, 1f)] private float speedMultiplierPerHit = 0.7f;
    [SerializeField, Min(0.2f)] private float minimumCrossingDuration = 0.55f;
    [SerializeField, Min(0f)] private float edgePadding = 8f;
    [SerializeField, Min(2)] private int rounds = 3;
    [SerializeField, Min(0f)] private float targetWidthReduction = 10f;
    [SerializeField, Min(0f)] private float minimumTargetSeparation = 45f;
    [SerializeField, Min(0f)] private float nearTargetSpeedBoost = 1.1f;
    [Header("Pesca")]
    [SerializeField, Range(0f, 1f)] private float initialFishingProgress = 0.3f;
    [SerializeField, Range(0.01f, 0.5f)] private float fishingClickGain = 0.085f;
    [SerializeField, Min(0.01f)] private float fishingDrainPerSecond = 0.16f;
    [SerializeField, Min(1f)] private float fishingPlusDifficulty = 1.35f;
    [SerializeField, Min(140f)] private float fishingPanelWidth = 190f;
    [SerializeField, Min(0f)] private float fishingVerticalGap = 45f;
    [SerializeField, Min(0f)] private float hitPunchStrength = 0.08f;
    [SerializeField, Min(0f)] private float missShakeStrength = 14f;
    [SerializeField, Min(0.05f)] private float fishDropPunchDuration = 0.23f;
    [SerializeField, Min(0f)] private float fishDropPunchGap = 0.04f;
    [SerializeField, Min(0f)] private float fishDropPunchStrength = 0.13f;

    private struct Layout
    {
        public Vector2 Position, Size, Min, Max, Pivot;
        public Layout(RectTransform rect)
        {
            Position = rect.anchoredPosition;
            Size = rect.sizeDelta;
            Min = rect.anchorMin;
            Max = rect.anchorMax;
            Pivot = rect.pivot;
        }
        public void Apply(RectTransform rect)
        {
            rect.anchorMin = Min;
            rect.anchorMax = Max;
            rect.pivot = Pivot;
            rect.sizeDelta = Size;
            rect.anchoredPosition = Position;
            rect.localScale = Vector3.one;
        }
    }

    private Action<int> completed;
    private Action woodHit;
    private Action<bool> fishingTensionChanged;
    private Action fishDropped;
    private RectTransform bar, panel, buttonRect, titleRect;
    private Text buttonText;
    private Layout panelLayout, barLayout, markerLayout, targetLayout, buttonLayout, titleLayout;
    private bool layoutCaptured, fishing, fishingPlus, resolving;
    private bool fishingTensionStarted;
    private int round, hits;
    private float difficultyMultiplier, initialTargetWidth, previousTargetCenter;
    private float markerX, markerLimit, markerPivotOffset, markerDirection;
    private float fishingProgress, fishingElapsed;
    private Vector2 lastParentSize;
    private readonly Vector3[] layoutCorners = new Vector3[4];

    public bool CanStart() => ResolveReferences();

    private void Awake()
    {
        ResolveReferences();
        Hide();
    }

    public void Begin(ActivityKind activity, Action<int> onCompleted, int fishingDifficulty = 0,
        Action onWoodHit = null, Action<bool> onFishingTension = null,
        Action onFishDropped = null)
    {
        AudioManager.StopFishingPull();
        if (!ResolveReferences())
        {
            Debug.LogError("Minigame sem marcador, faixa verde ou painel configurado.", this);
            onCompleted?.Invoke(-1);
            return;
        }
        RestoreLayout();
        completed = onCompleted;
        woodHit = onWoodHit;
        fishingTensionChanged = onFishingTension;
        fishDropped = onFishDropped;
        fishing = activity == ActivityKind.Fish || activity == ActivityKind.FishPlus;
        fishingPlus = activity == ActivityKind.FishPlus;
        resolving = false;
        fishingTensionStarted = false;
        round = hits = 0;
        difficultyMultiplier = fishing ? 1f + Mathf.Max(0, fishingDifficulty) * 0.4f : 1f;
        previousTargetCenter = float.NaN;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        if (fishing) StartFishing();
        else StartWoodRound();
    }

    private bool ResolveReferences()
    {
        if (group == null || marker == null || marker.parent is not RectTransform parent) return false;
        bar = parent;
        if (greenTarget == null) greenTarget = bar.Find("Zona perfeita") as RectTransform;
        if (greenTarget == null) return false;
        if (actionButton == null) actionButton = group.GetComponentInChildren<Button>(true);
        if (actionButton == null) return false;
        if (layoutCaptured) return true;
        panel = group.GetComponent<RectTransform>();
        buttonRect = actionButton.GetComponent<RectTransform>();
        if (title != null) titleRect = title.GetComponent<RectTransform>();
        buttonText = actionButton.GetComponentInChildren<Text>(true);
        if (panel == null || buttonRect == null) return false;
        panelLayout = new Layout(panel);
        barLayout = new Layout(bar);
        markerLayout = new Layout(marker);
        targetLayout = new Layout(greenTarget);
        buttonLayout = new Layout(buttonRect);
        if (titleRect != null) titleLayout = new Layout(titleRect);
        initialTargetWidth = greenTarget.rect.width;
        layoutCaptured = initialTargetWidth > 0f;
        return layoutCaptured;
    }

    private void RestoreLayout()
    {
        if (!layoutCaptured) return;
        panel.DOKill();
        bar.DOKill();
        marker.DOKill();
        greenTarget.DOKill();
        buttonRect.DOKill();
        panelLayout.Apply(panel);
        barLayout.Apply(bar);
        markerLayout.Apply(marker);
        targetLayout.Apply(greenTarget);
        buttonLayout.Apply(buttonRect);
        if (titleRect != null) titleLayout.Apply(titleRect);
        if (buttonText != null) buttonText.text = "PARAR";
    }

    private void Update()
    {
        if (completed == null || resolving) return;
        if (fishing)
        {
            RectTransform parent = panel.parent as RectTransform;
            if (parent != null && parent.rect.size != lastParentSize) LayoutFishingPanel();
            fishingElapsed += Time.deltaTime;
            float struggle = 1f + 0.25f * Mathf.Sin(fishingElapsed * 8f);
            float multiplier = difficultyMultiplier * (fishingPlus ? fishingPlusDifficulty : 1f);
            fishingProgress = Mathf.Max(0f, fishingProgress -
                fishingDrainPerSecond * multiplier * struggle * Time.deltaTime);
            UpdateFishingFill();
            CheckFishingTension();
            if (fishingProgress <= 0f) FinishWithFeedback(0);
            return;
        }
        float proximity = 1f - Mathf.Clamp01(
            Mathf.Abs(markerX - previousTargetCenter) / Mathf.Max(1f, greenTarget.rect.width * 2.5f));
        float crossing = Mathf.Max(minimumCrossingDuration,
            markerCrossingDuration * Mathf.Pow(speedMultiplierPerHit, hits));
        float speed = markerLimit * 2f / crossing * (1f + nearTargetSpeedBoost * proximity);
        markerX += markerDirection * speed * Time.deltaTime;
        if (markerX >= markerLimit) { markerX = markerLimit; markerDirection = -1f; }
        else if (markerX <= -markerLimit) { markerX = -markerLimit; markerDirection = 1f; }
        marker.anchoredPosition = new Vector2(markerX + markerPivotOffset, marker.anchoredPosition.y);
    }

    // O botão da cena usa a mesma ação nos dois minigames.
    public void Stop()
    {
        if (completed == null || resolving) return;
        if (fishing)
        {
            if (Time.timeScale <= 0f) return;
            AudioManager.KeepFishingPullAlive();
            fishingProgress = Mathf.Min(1f, fishingProgress + fishingClickGain /
                (fishingPlus ? fishingPlusDifficulty : 1f));
            UpdateFishingFill();
            CheckFishingTension();
            buttonRect.DOKill();
            buttonRect.localScale = Vector3.one;
            buttonRect.DOPunchScale(Vector3.one * 0.07f, 0.16f, 4);
            if (fishingProgress >= 1f) FinishWithFeedback(1);
            return;
        }
        Bounds markerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, marker);
        Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, greenTarget);
        bool hit = markerBounds.center.x >= targetBounds.min.x &&
            markerBounds.center.x <= targetBounds.max.x;
        if (hit)
        {
            hits++;
            AudioManager.Play(SoundCue.WoodHit);
            woodHit?.Invoke();
            bar.DOKill();
            bar.anchoredPosition = barLayout.Position;
            bar.localScale = Vector3.one;
            bar.DOPunchScale(Vector3.one * hitPunchStrength, 0.2f, 4);
        }
        else
        {
            AudioManager.Play(SoundCue.WoodMiss);
            bar.DOKill();
            bar.anchoredPosition = barLayout.Position;
            bar.DOShakeAnchorPos(0.22f, missShakeStrength, 12, 90f, false, true);
        }
        round++;
        if (round < rounds) StartWoodRound();
        else FinishWithFeedback(hits == rounds ? 2 : hits >= rounds - 1 ? 1 : 0);
    }

    private void FinishWithFeedback(int rating)
    {
        AudioManager.StopFishingPull();
        resolving = true;
        fishingTensionStarted = false;
        fishingTensionChanged?.Invoke(false);
        if (!fishing)
        {
            DOVirtual.DelayedCall(0.23f, () => Complete(rating)).SetTarget(this);
            return;
        }
        int fishCount = fishingPlus ? (rating > 0 ? 4 : 2) : (rating > 0 ? 2 : 1);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        panel.DOKill();
        panel.localScale = Vector3.one;
        if (rating == 0)
            sequence.Append(bar.DOShakeAnchorPos(0.2f, missShakeStrength, 10, 90f, false, true));
        for (int i = 0; i < fishCount; i++)
        {
            sequence.Append(panel.DOPunchScale(Vector3.one * fishDropPunchStrength,
                fishDropPunchDuration, 2));
            sequence.InsertCallback(sequence.Duration() - fishDropPunchDuration * 0.5f,
                () => fishDropped?.Invoke());
            if (i < fishCount - 1) sequence.AppendInterval(fishDropPunchGap);
        }
        sequence.AppendInterval(0.2f);
        sequence.OnComplete(() => Complete(rating));
    }

    private void CheckFishingTension()
    {
        bool active = fishingProgress >= 2f / 3f;
        if (active == fishingTensionStarted) return;
        fishingTensionStarted = active;
        fishingTensionChanged?.Invoke(active);
    }

    private void StartWoodRound()
    {
        float markerWidth = marker.rect.width;
        float targetWidth = Mathf.Max(markerWidth * 3f, initialTargetWidth - round * targetWidthReduction);
        greenTarget.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        float targetLimit = Mathf.Max(0f, bar.rect.width * 0.5f - targetWidth * 0.5f - edgePadding);
        float targetCenter = UnityEngine.Random.Range(-targetLimit, targetLimit);
        for (int attempt = 0; attempt < 12 && !float.IsNaN(previousTargetCenter) &&
            Mathf.Abs(targetCenter - previousTargetCenter) < minimumTargetSeparation; attempt++)
            targetCenter = UnityEngine.Random.Range(-targetLimit, targetLimit);
        if (!float.IsNaN(previousTargetCenter) &&
            Mathf.Abs(targetCenter - previousTargetCenter) < minimumTargetSeparation)
            targetCenter = previousTargetCenter >= 0f ? -targetLimit : targetLimit;
        previousTargetCenter = targetCenter;
        greenTarget.anchoredPosition = new Vector2(
            targetCenter + (greenTarget.pivot.x - 0.5f) * targetWidth, greenTarget.anchoredPosition.y);
        markerLimit = Mathf.Max(0f, (bar.rect.width - markerWidth - edgePadding * 2f) * 0.5f);
        markerPivotOffset = (marker.pivot.x - 0.5f) * markerWidth;
        markerX = -markerLimit;
        markerDirection = 1f;
        marker.anchoredPosition = new Vector2(markerX + markerPivotOffset, marker.anchoredPosition.y);
        if (title != null)
            title.text = "MADEIRA: ACERTE O VERDE\n" + (round + 1) + "/" + rounds + " · ACERTOS: " + hits;
    }

    private void StartFishing()
    {
        LayoutFishingPanel();
        greenTarget.anchorMin = new Vector2(0f, 0f);
        greenTarget.anchorMax = new Vector2(1f, 0f);
        greenTarget.pivot = new Vector2(0f, 0f);
        greenTarget.anchoredPosition = Vector2.zero;
        marker.anchorMin = new Vector2(0f, 1f);
        marker.anchorMax = new Vector2(1f, 1f);
        marker.pivot = new Vector2(0f, 1f);
        marker.anchoredPosition = Vector2.zero;
        if (buttonText != null) buttonText.text = "PUXAR";
        fishingProgress = initialFishingProgress;
        fishingElapsed = 0f;
        UpdateFishingFill();
        if (title != null)
            title.text = fishingPlus ? "PESCA PLUS\n2 OU 4 PEIXES" : "PESCA\n1 OU 2 PEIXES";
    }

    private void LayoutFishingPanel()
    {
        RectTransform parent = panel.parent as RectTransform;
        if (parent == null) return;
        lastParentSize = parent.rect.size;
        float top = parent.rect.yMax - 120f;
        float bottom = parent.rect.yMin + 120f;
        float right = parent.rect.xMax - 29f;
        if (topHud != null)
        {
            topHud.GetWorldCorners(layoutCorners);
            top = parent.InverseTransformPoint(layoutCorners[0]).y - fishingVerticalGap;
        }
        if (finishDayButton != null)
        {
            finishDayButton.GetWorldCorners(layoutCorners);
            bottom = parent.InverseTransformPoint(layoutCorners[1]).y + fishingVerticalGap;
            right = parent.InverseTransformPoint(layoutCorners[2]).x;
        }
        float height = Mathf.Max(220f, top - bottom);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(fishingPanelWidth, height);
        panel.anchoredPosition = new Vector2(right - parent.rect.xMax - fishingPanelWidth * 0.5f,
            top - parent.rect.yMax - height * 0.5f);
        if (titleRect != null)
        {
            titleRect.sizeDelta = new Vector2(fishingPanelWidth - 20f, titleRect.sizeDelta.y);
            titleRect.anchoredPosition = new Vector2(10f, -12f);
        }
        bar.anchorMin = bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0f, 1f);
        bar.sizeDelta = new Vector2(42f, height - 140f);
        bar.anchoredPosition = new Vector2((fishingPanelWidth - 42f) * 0.5f, -64f);
        buttonRect.anchoredPosition = new Vector2((fishingPanelWidth - buttonRect.rect.width) * 0.5f,
            -height + 55f);
        UpdateFishingFill();
    }

    private void UpdateFishingFill()
    {
        float height = bar.rect.height;
        greenTarget.sizeDelta = new Vector2(0f, height * fishingProgress);
        marker.sizeDelta = new Vector2(0f, height * (1f - fishingProgress));
    }

    private void Complete(int rating)
    {
        if (fishing)
            AudioManager.Play(rating > 0 ? SoundCue.FishingCaught : SoundCue.FishingFailed);
        Action<int> callback = completed;
        completed = null;
        woodHit = null;
        fishingTensionChanged = null;
        fishDropped = null;
        Hide();
        callback?.Invoke(rating);
    }

    private void Hide()
    {
        AudioManager.StopFishingPull();
        if (group == null) return;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        RestoreLayout();
    }
}
