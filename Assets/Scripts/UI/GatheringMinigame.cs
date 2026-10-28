using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class GatheringMinigame : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform marker;
    [SerializeField] private Text title;
    [Header("Parâmetros provisórios")]
    [SerializeField, Min(0.1f)] private float markerCrossingDuration = 1.25f;
    [SerializeField, Min(1f)] private float markerTravel = 190f;
    [SerializeField, Range(0f, 1f)] private float perfectZone = 0.2f;
    [SerializeField, Range(0f, 1f)] private float successZone = 0.65f;
    private Action<int> completed;
    private float currentPerfectZone;
    private float currentSuccessZone;

    private void Awake() { Hide(); }

    public void Begin(ActivityKind activity, Action<int> onCompleted, int fishingDifficulty = 0)
    {
        completed = onCompleted;
        float difficulty = activity == ActivityKind.Fish ? 1f + Mathf.Max(0, fishingDifficulty) * 0.4f : 1f;
        currentPerfectZone = perfectZone / difficulty;
        currentSuccessZone = successZone / difficulty;
        if (title != null) title.text = activity == ActivityKind.Fish ? "PESCARIA: PARE NO CENTRO" :
            "COLETA DE MADEIRA: PARE NO CENTRO";
        if (group != null)
        {
            group.alpha = 1;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        if (marker != null)
        {
            marker.DOKill();
            marker.anchoredPosition = new Vector2(-markerTravel * 0.5f, marker.anchoredPosition.y);
            marker.DOAnchorPosX(markerTravel * 0.5f, markerCrossingDuration)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Yoyo);
        }
    }

    public void Stop()
    {
        if (completed == null) return;
        float distance = marker == null ? 1f : Mathf.Abs(marker.anchoredPosition.x) / (markerTravel * 0.5f);
        int rating = distance <= currentPerfectZone ? 2 : distance <= currentSuccessZone ? 1 : 0;
        var callback = completed;
        completed = null;
        Hide();
        callback(rating);
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
