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
    [SerializeField, Min(0.1f)] private float markerCrossingDuration = 2f;
    [SerializeField, Min(0f)] private float edgePadding = 8f;
    [SerializeField, Min(2)] private int rounds = 3;
    [SerializeField, Min(0f)] private float targetWidthReduction = 10f;
    [SerializeField, Min(0f)] private float minimumTargetSeparation = 45f;

    private Action<int> completed;
    private RectTransform bar;
    private string activityTitle;
    private int round;
    private int hits;
    private float difficultyMultiplier;
    private float initialTargetWidth;
    private float previousTargetCenter;
    private float roundStartedAt;

    public bool CanStart() => ResolveReferences();

    private void Awake()
    {
        ResolveReferences();
        Hide();
    }

    public void Begin(ActivityKind activity, Action<int> onCompleted, int fishingDifficulty = 0)
    {
        if (!ResolveReferences())
        {
            Debug.LogError("Minigame sem marcador, faixa verde ou painel configurado.", this);
            onCompleted?.Invoke(-1);
            return;
        }

        completed = onCompleted;
        activityTitle = activity == ActivityKind.Fish ? "PESCA" : "MADEIRA";
        round = 0;
        hits = 0;
        difficultyMultiplier = activity == ActivityKind.Fish ?
            1f + Mathf.Max(0, fishingDifficulty) * 0.4f : 1f;
        previousTargetCenter = float.NaN;
        if (group != null)
        {
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        StartRound();
    }

    private bool ResolveReferences()
    {
        if (group == null || marker == null || marker.parent is not RectTransform parent) return false;
        bar = parent;
        if (greenTarget == null) greenTarget = bar.Find("Zona perfeita") as RectTransform;
        if (greenTarget == null) return false;
        if (initialTargetWidth <= 0f) initialTargetWidth = greenTarget.rect.width;
        return initialTargetWidth > 0f;
    }

    public void Stop()
    {
        if (completed == null || Time.unscaledTime - roundStartedAt < 0.15f) return;

        Bounds markerBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, marker);
        Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(bar, greenTarget);
        if (markerBounds.min.x >= targetBounds.min.x && markerBounds.max.x <= targetBounds.max.x)
            hits++;

        round++;
        if (round < rounds)
        {
            StartRound();
            return;
        }

        int rating = hits == rounds ? 2 : hits >= rounds - 1 ? 1 : 0;
        Action<int> callback = completed;
        completed = null;
        Hide();
        callback(rating);
    }

    private void StartRound()
    {
        float markerWidth = marker.rect.width;
        float targetWidth = Mathf.Max(markerWidth * 3f,
            (initialTargetWidth - round * targetWidthReduction) / difficultyMultiplier);
        greenTarget.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

        float targetLimit = Mathf.Max(0f, bar.rect.width * 0.5f - targetWidth * 0.5f - edgePadding);
        float targetCenter = UnityEngine.Random.Range(-targetLimit, targetLimit);
        for (int attempt = 0; attempt < 12 &&
             !float.IsNaN(previousTargetCenter) &&
             Mathf.Abs(targetCenter - previousTargetCenter) < minimumTargetSeparation; attempt++)
            targetCenter = UnityEngine.Random.Range(-targetLimit, targetLimit);
        if (!float.IsNaN(previousTargetCenter) &&
            Mathf.Abs(targetCenter - previousTargetCenter) < minimumTargetSeparation)
            targetCenter = previousTargetCenter >= 0f ? -targetLimit : targetLimit;
        previousTargetCenter = targetCenter;
        greenTarget.anchoredPosition = new Vector2(
            targetCenter + (greenTarget.pivot.x - 0.5f) * targetWidth,
            greenTarget.anchoredPosition.y);

        float travel = Mathf.Max(0f, bar.rect.width - markerWidth - edgePadding * 2f);
        float pivotOffset = (marker.pivot.x - 0.5f) * markerWidth;
        marker.DOKill();
        marker.anchoredPosition = new Vector2(-travel * 0.5f + pivotOffset, marker.anchoredPosition.y);
        marker.DOAnchorPosX(travel * 0.5f + pivotOffset, markerCrossingDuration)
            .SetEase(Ease.InOutCubic).SetLoops(-1, LoopType.Yoyo);

        roundStartedAt = Time.unscaledTime;
        //if (title != null)
        //    title.text = activityTitle + ": ACERTE O VERDE\n" + (round + 1) + "/" + rounds +
        //        " · ACERTOS: " + hits;
    }

    private void Hide()
    {
        if (marker != null) marker.DOKill();
        if (group != null)
        {
            group.alpha = 0;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }
}
