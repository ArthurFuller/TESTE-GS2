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
    [SerializeField] private Sprite preparedFoodSprite;
    [SerializeField] private Sprite fishSprite;
    [SerializeField] private Sprite cornSprite;
    [SerializeField] private Sprite cassavaSprite;
    [SerializeField] private Sprite jerimumSprite;
    [SerializeField] private Sprite milkSprite;
    [SerializeField] private Sprite eggSprite;
    [SerializeField] private Button finishButton;
    [SerializeField] private Button backButton;
    [SerializeField, Min(0f)] private float entranceDuration = 0.45f;
    [SerializeField, Min(0f)] private float foodWaveDelay = 0.07f;

    private WorkerAssignment[] workers;
    private SaleItem?[] chosenFood;
    private Action<SaleItem?[]> completed;
    private Action cancelled;
    private bool open;
    private FeedingFoodIcon reservedIcon;
    private Tween backEntrance;
    private static readonly SaleItem[] FoodKinds = { SaleItem.Food, SaleItem.Fish,
        SaleItem.Corn, SaleItem.Pumpkin, SaleItem.Jerimum, SaleItem.Milk, SaleItem.Eggs };

    private void Awake()
    {
        if (finishButton != null) finishButton.onClick.AddListener(Finish);
        if (backButton != null) backButton.onClick.AddListener(Cancel);
        Hide();
    }

    private void OnDestroy()
    {
        if (finishButton != null) finishButton.onClick.RemoveListener(Finish);
        if (backButton != null) backButton.onClick.RemoveListener(Cancel);
        backEntrance?.Kill();
        if (panel != null) panel.DOKill();
        foreach (FeedingFoodIcon icon in foodIcons)
            if (icon != null) icon.StopAnimation();
    }

    public void Begin(WorkerAssignment[] dayWorkers, Action<SaleItem?[]> onCompleted,
        Action onCancelled = null)
    {
        workers = dayWorkers;
        EnsureWorkerTargets(workers.Length);
        LayoutWorkerTargets();
        reservedIcon = null;
        chosenFood = new SaleItem?[workers.Length];
        completed = onCompleted;
        cancelled = onCancelled;
        open = true;
        backEntrance?.Kill();
        if (backButton != null)
        {
            backButton.gameObject.SetActive(false);
            backButton.transform.localScale = Vector3.one;
        }
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        panel.DOKill();
        panel.localScale = Vector3.one * 0.92f;
        panel.DOScale(1f, entranceDuration).SetEase(Ease.OutBack);
        Refresh(true);
        int visibleFoods = 0;
        foreach (SaleItem kind in FoodKinds)
            if (Stock(kind) > 0) visibleFoods++;
        float waveEnd = entranceDuration + Mathf.Max(0, visibleFoods - 1) * foodWaveDelay;
        backEntrance = DOVirtual.DelayedCall(waveEnd, ShowBackButton).SetTarget(this);
    }

    private void ShowBackButton()
    {
        if (!open || backButton == null) return;
        RectTransform rect = (RectTransform)backButton.transform;
        rect.DOKill();
        rect.localScale = Vector3.zero;
        backButton.gameObject.SetActive(true);
        DOTween.Sequence()
            .Append(rect.DOScale(1.1f, 0.26f).SetEase(Ease.OutCubic))
            .Append(rect.DOScale(1f, 0.18f).SetEase(Ease.InOutSine))
            .SetTarget(rect);
    }

    private void EnsureWorkerTargets(int count)
    {
        if (count <= workerTargets.Length || workerTargets.Length == 0) return;
        int oldCount = workerTargets.Length;
        Array.Resize(ref workerTargets, count);
        Array.Resize(ref workerLabels, count);
        for (int i = oldCount; i < count; i++)
        {
            RectTransform target = Instantiate(workerTargets[oldCount - 1], panel);
            target.name = "Trabalhador " + (i + 1);
            workerTargets[i] = target;
            workerLabels[i] = target.GetComponentInChildren<Text>(true);
        }
    }

    private void LayoutWorkerTargets()
    {
        int count = Mathf.Min(workers.Length, workerTargets.Length);
        float width = count >= 4 ? 126f : 168f;
        float spacing = count >= 4 ? 136f : 178f;
        for (int i = 0; i < count; i++)
        {
            RectTransform target = workerTargets[i];
            if (target == null) continue;
            target.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * spacing, -80f);
            target.sizeDelta = new Vector2(width, 72f);
            if (workerLabels[i] == null) continue;
            workerLabels[i].rectTransform.sizeDelta = new Vector2(width - 16f, 60f);
            workerLabels[i].fontSize = count >= 4 ? 12 : 14;
            workerLabels[i].resizeTextForBestFit = true;
            workerLabels[i].resizeTextMinSize = 11;
            workerLabels[i].resizeTextMaxSize = count >= 4 ? 12 : 14;
        }
    }

    public bool Reserve(FeedingFoodIcon icon)
    {
        if (!open || reservedIcon != null || Stock(icon.Kind) == 0) return false;
        reservedIcon = icon;
        icon.SetQuantity(Stock(icon.Kind) - 1);
        return true;
    }

    public void CancelReservation(FeedingFoodIcon icon)
    {
        if (reservedIcon != icon) return;
        reservedIcon = null;
        icon.SetQuantity(Stock(icon.Kind));
    }

    public RectTransform FeedFromIcon(FeedingFoodIcon icon, PointerEventData eventData)
    {
        if (!open || reservedIcon != icon) return null;
        int target = -1;
        for (int i = 0; i < workerTargets.Length && i < chosenFood.Length; i++)
            if (CanFeed(i) && RectTransformUtility.RectangleContainsScreenPoint(
                    workerTargets[i], eventData.position, eventData.pressEventCamera))
            {
                target = i;
                break;
            }
        if (target < 0 || !Consume(icon.Kind)) return null;
        reservedIcon = null;
        chosenFood[target] = icon.Kind;
        RectTransform worker = workerTargets[target];
        worker.DOKill();
        worker.localScale = Vector3.one;
        worker.DOPunchScale(Vector3.one * 0.12f, 0.3f, 1);
        float restY = worker.anchoredPosition.y;
        worker.DOAnchorPosY(restY + 5f, 0.16f).SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.OutSine);
        Refresh(false);
        return worker;
    }

    private bool CanFeed(int index) => index < workers.Length && workers[index] != null &&
        !workers[index].IsUnavailableToday && !chosenFood[index].HasValue;

    private int Stock(SaleItem kind) => kind switch
    {
        SaleItem.Food => resources.Food,
        SaleItem.Fish => resources.Fish,
        SaleItem.Corn => resources.Corn,
        SaleItem.Pumpkin => resources.Pumpkin,
        SaleItem.Jerimum => resources.Jerimum,
        SaleItem.Milk => resources.Milk,
        SaleItem.Eggs => resources.Eggs,
        _ => 0
    };

    private bool Consume(SaleItem kind) => kind switch
    {
        SaleItem.Food => resources.TryDeliverFood(1),
        SaleItem.Fish => resources.TryDeliverFish(1),
        SaleItem.Corn => resources.TryDeliverCrop(CropType.Corn, 1),
        SaleItem.Pumpkin => resources.TryDeliverCrop(CropType.Pumpkin, 1),
        SaleItem.Jerimum => resources.TryDeliverCrop(CropType.Jerimum, 1),
        SaleItem.Milk => resources.TryDeliverAnimalProduct(SaleItem.Milk),
        SaleItem.Eggs => resources.TryDeliverAnimalProduct(SaleItem.Eggs),
        _ => false
    };

    private void Refresh(bool animate)
    {
        if (foodCount != null) foodCount.text = "Escolha um alimento para cada trabalhador";
        RefreshLabels();
        int visibleCount = 0;
        for (int i = 0; i < FoodKinds.Length; i++)
            if (Stock(FoodKinds[i]) > 0) visibleCount++;
        int visibleIndex = 0;
        for (int i = 0; i < foodIcons.Length; i++)
        {
            FeedingFoodIcon icon = foodIcons[i];
            if (icon == null) continue;
            bool visible = i < FoodKinds.Length && Stock(FoodKinds[i]) > 0;
            icon.gameObject.SetActive(visible);
            if (!visible) continue;
            SaleItem kind = FoodKinds[i];
            icon.Configure(this, kind, SpriteFor(kind),
                foodCount != null ? foodCount.font : null);
            icon.SetQuantity(Stock(kind) - (reservedIcon == icon ? 1 : 0));
            icon.SetHome(new Vector2((visibleIndex - (visibleCount - 1) * 0.5f) * 73f, 54f),
                animate, visibleIndex * foodWaveDelay, animate ? entranceDuration : 0.18f);
            visibleIndex++;
        }
    }

    private Sprite SpriteFor(SaleItem kind) => kind switch
    {
        SaleItem.Food => preparedFoodSprite,
        SaleItem.Fish => fishSprite,
        SaleItem.Corn => cornSprite,
        SaleItem.Pumpkin => cassavaSprite,
        SaleItem.Jerimum => jerimumSprite,
        SaleItem.Milk => milkSprite,
        SaleItem.Eggs => eggSprite,
        _ => null
    };

    private void RefreshLabels()
    {
        for (int i = 0; i < workerLabels.Length && i < chosenFood.Length; i++)
        {
            if (workerLabels[i] == null) continue;
            WorkerAssignment worker = workers[i];
            workerLabels[i].text = worker == null ? "" : worker.DisplayName + "\n" +
                (worker.IsUnavailableToday ? "ausente" : !chosenFood[i].HasValue ? "sem alimento" :
                 chosenFood[i].Value == SaleItem.Food ? "Pronta: 2 ações amanhã" :
                 chosenFood[i].Value == SaleItem.Fish ? "Peixe: 1 ação amanhã" :
                 chosenFood[i].Value == SaleItem.Corn ? "Milho: 1 ação amanhã" :
                 chosenFood[i].Value == SaleItem.Pumpkin ? "Macaxeira: 1 ação amanhã" :
                 chosenFood[i].Value == SaleItem.Jerimum ? "Jerimum: 1 ação amanhã" :
                 chosenFood[i].Value == SaleItem.Milk ? "Leite: 1 ação amanhã" :
                 "Ovos: 1 ação amanhã");
        }
    }

    public void Finish()
    {
        if (!open) return;
        open = false;
        if (reservedIcon != null) CancelReservation(reservedIcon);
        foreach (FeedingFoodIcon icon in foodIcons)
            if (icon != null) icon.StopAnimation();
        Hide();
        Action<SaleItem?[]> callback = completed;
        completed = null;
        cancelled = null;
        callback?.Invoke(chosenFood);
    }

    public void Cancel()
    {
        if (!open) return;
        open = false;
        if (reservedIcon != null) CancelReservation(reservedIcon);
        foreach (SaleItem? food in chosenFood)
        {
            if (!food.HasValue) continue;
            switch (food.Value)
            {
                case SaleItem.Food: resources.AddFood(1); break;
                case SaleItem.Fish: resources.AddFish(1); break;
                case SaleItem.Corn: resources.AddCrop(CropType.Corn, 1); break;
                case SaleItem.Pumpkin: resources.AddCrop(CropType.Pumpkin, 1); break;
                case SaleItem.Jerimum: resources.AddCrop(CropType.Jerimum, 1); break;
                case SaleItem.Milk: resources.AddAnimalProducts(1, 0); break;
                case SaleItem.Eggs: resources.AddAnimalProducts(0, 1); break;
            }
        }
        foreach (FeedingFoodIcon icon in foodIcons)
            if (icon != null) icon.StopAnimation();
        Hide();
        completed = null;
        Action callback = cancelled;
        cancelled = null;
        callback?.Invoke();
    }

    private void Hide()
    {
        backEntrance?.Kill();
        if (backButton != null)
        {
            backButton.transform.DOKill();
            backButton.transform.localScale = Vector3.one;
            backButton.gameObject.SetActive(false);
        }
        if (group == null) return;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
