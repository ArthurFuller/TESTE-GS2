using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FeedingMinigame : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panel;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text foodCount;
    [SerializeField] private Text[] workerLabels;
    [SerializeField] private RectTransform[] workerTargets;
    [SerializeField] private FeedingFoodIcon[] foodIcons;
    [SerializeField] private Button finishButton;
    [SerializeField, Min(0f)] private float entranceDuration = 0.45f;
    [SerializeField, Min(0f)] private float foodWaveDelay = 0.07f;

    private WorkerAssignment[] workers;
    private int[] portions;
    private int foodPerWorker;
    private Action<int[]> completed;
    private bool open;

    private void Awake()
    {
        if (finishButton != null) finishButton.onClick.AddListener(Finish);
        Hide();
    }

    private void OnDestroy()
    {
        if (finishButton != null) finishButton.onClick.RemoveListener(Finish);
        if (panel != null) panel.DOKill();
        foreach (FeedingFoodIcon icon in foodIcons)
            if (icon != null) icon.StopAnimation();
    }

    public void Begin(WorkerAssignment[] dayWorkers, int requiredFood, Action<int[]> onCompleted)
    {
        workers = dayWorkers;
        foodPerWorker = Mathf.Max(1, requiredFood);
        portions = new int[workers.Length];
        completed = onCompleted;
        open = true;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        panel.DOKill();
        panel.localScale = Vector3.one * 0.92f;
        panel.DOScale(1f, entranceDuration).SetEase(Ease.OutBack);
        Refresh(true);
    }

    public void FeedFromIcon(FeedingFoodIcon icon, PointerEventData eventData)
    {
        if (!open || resources.Food == 0) return;
        int target = -1;
        if (eventData.dragging)
        {
            for (int i = 0; i < workerTargets.Length && i < portions.Length; i++)
                if (CanFeed(i) && RectTransformUtility.RectangleContainsScreenPoint(
                        workerTargets[i], eventData.position, eventData.pressEventCamera))
                {
                    target = i;
                    break;
                }
        }
        else
        {
            int smallest = int.MaxValue;
            for (int i = 0; i < portions.Length; i++)
                if (CanFeed(i) && portions[i] < smallest)
                {
                    target = i;
                    smallest = portions[i];
                }
        }
        if (target < 0) return;
        resources.ConsumeFood(1);
        portions[target]++;
        RectTransform worker = workerTargets[target];
        worker.DOKill();
        worker.localScale = Vector3.one;
        worker.DOPunchScale(Vector3.one * 0.12f, 0.3f, 1);
        float restY = worker.anchoredPosition.y;
        worker.DOAnchorPosY(restY + 5f, 0.16f).SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.OutSine);
        icon.PlayPlop(worker, () => Refresh(false));
        RefreshLabels();
    }

    private bool CanFeed(int index) => index < workers.Length && workers[index] != null &&
        !workers[index].IsUnavailableToday && portions[index] < foodPerWorker;

    private void Refresh(bool animate)
    {
        if (foodCount != null) foodCount.text = "Comida disponível: " + resources.Food;
        RefreshLabels();
        int visible = Mathf.Min(resources.Food, foodIcons.Length);
        for (int i = 0; i < foodIcons.Length; i++)
        {
            FeedingFoodIcon icon = foodIcons[i];
            if (icon == null) continue;
            icon.Configure(this);
            icon.gameObject.SetActive(i < visible);
            if (i < visible) icon.ResetPosition(animate, i * foodWaveDelay, entranceDuration);
        }
    }

    private void RefreshLabels()
    {
        for (int i = 0; i < workerLabels.Length && i < portions.Length; i++)
        {
            if (workerLabels[i] == null) continue;
            WorkerAssignment worker = workers[i];
            workerLabels[i].text = worker == null ? "" : worker.DisplayName + "  " +
                (worker.IsUnavailableToday ? "ausente" : portions[i] + "/" + foodPerWorker);
        }
    }

    public void Finish()
    {
        if (!open) return;
        open = false;
        Hide();
        Action<int[]> callback = completed;
        completed = null;
        callback?.Invoke(portions);
    }

    private void Hide()
    {
        if (group == null) return;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
