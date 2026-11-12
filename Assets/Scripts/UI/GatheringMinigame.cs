using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GatheringMinigame : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform marker;
    [SerializeField] private RectTransform greenTarget;
    [Header("Arte dos minigames (objetos na cena)")]
    [SerializeField] private RectTransform woodArtRoot;
    [SerializeField] private RectTransform fishingArtRoot;
    [SerializeField] private Image fishingFill;
    [SerializeField] private Text title;
    [SerializeField] private Button woodActionButton;
    [SerializeField] private Button fishingActionButton;
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
    [SerializeField, Min(0f)] private float hitPunchStrength = 0.08f;
    [SerializeField, Min(0f)] private float missShakeStrength = 14f;
    [SerializeField, Min(0.05f)] private float fishDropPunchDuration = 0.23f;
    [SerializeField, Min(0f)] private float fishDropPunchGap = 0.04f;
    [SerializeField, Min(0f)] private float fishDropPunchStrength = 0.13f;

    private struct Layout
    {
        public Vector2 Position, Size, Min, Max, Pivot;
        public Vector3 Scale;
        public Layout(RectTransform rect)
        {
            Position = rect.anchoredPosition;
            Size = rect.sizeDelta;
            Min = rect.anchorMin;
            Max = rect.anchorMax;
            Pivot = rect.pivot;
            Scale = rect.localScale;
        }
        public void Apply(RectTransform rect)
        {
            rect.anchorMin = Min;
            rect.anchorMax = Max;
            rect.pivot = Pivot;
            rect.sizeDelta = Size;
            rect.anchoredPosition = Position;
            rect.localScale = Scale;
        }
    }

    private Action<int> completed;
    private Action woodHit;
    private Action<bool> fishingTensionChanged;
    private Action fishDropped;
    private RectTransform bar, panel, buttonRect, titleRect;
    private Image barImage;
    private Layout panelLayout, barLayout, markerLayout, targetLayout, titleLayout;
    private bool layoutCaptured, fishing, fishingPlus, resolving;
    private bool fishingTensionStarted;
    private int round, hits;
    private float difficultyMultiplier, initialTargetWidth, previousTargetCenter;
    private float markerX, markerLimit, markerPivotOffset, markerDirection;
    private float fishingProgress, fishingElapsed;

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
        buttonRect = (fishing ? fishingActionButton : woodActionButton).GetComponent<RectTransform>();
        RefreshArt();
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
        barImage = bar.GetComponent<Image>();
        if (greenTarget == null) greenTarget = bar.Find("Zona perfeita") as RectTransform;
        if (greenTarget == null) return false;
        if (woodActionButton == null || fishingActionButton == null) return false;
        if (layoutCaptured) return true;
        panel = group.GetComponent<RectTransform>();
        if (title != null) titleRect = title.GetComponent<RectTransform>();
        if (panel == null) return false;
        panelLayout = new Layout(panel);
        barLayout = new Layout(bar);
        markerLayout = new Layout(marker);
        targetLayout = new Layout(greenTarget);
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
        panelLayout.Apply(panel);
        barLayout.Apply(bar);
        markerLayout.Apply(marker);
        targetLayout.Apply(greenTarget);
        if (titleRect != null) titleLayout.Apply(titleRect);
    }

    private void RefreshArt()
    {
        if (title != null) title.gameObject.SetActive(true);
        if (woodArtRoot != null) woodArtRoot.gameObject.SetActive(!fishing);
        if (fishingArtRoot != null) fishingArtRoot.gameObject.SetActive(fishing);
        if (woodActionButton != fishingActionButton)
        {
            woodActionButton.gameObject.SetActive(!fishing);
            fishingActionButton.gameObject.SetActive(fishing);
        }
        if (barImage != null) barImage.enabled = false;
        marker.gameObject.SetActive(!fishing);
        greenTarget.gameObject.SetActive(!fishing);
    }

    private void Update()
    {
        if (completed == null || resolving) return;
        if (fishing)
        {
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
        float targetWidth = Mathf.Max(markerWidth * 1.75f, initialTargetWidth - round * targetWidthReduction);
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
        fishingProgress = initialFishingProgress;
        fishingElapsed = 0f;
        UpdateFishingFill();
        if (title != null)
            title.text = fishingPlus ? "PESCA PLUS\n2 OU 4 PEIXES" : "PESCA\n1 OU 2 PEIXES";
    }

    private void UpdateFishingFill()
    {
        if (fishingFill != null) fishingFill.fillAmount = fishingProgress;
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
