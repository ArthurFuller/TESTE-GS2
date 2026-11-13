using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FeedingMinigame : MonoBehaviour
{
    [Serializable]
    private struct WorkerVisual
    {
        public bool useCustomPosition;
        public Vector2 position;
        public Vector2 imageSize;
        [Min(0f)] public float hitboxMargin;
    }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panel;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Text foodCount;
    [SerializeField] private Text[] workerLabels;
    [SerializeField] private RectTransform[] workerTargets;
    [Header("Visual dos até cinco funcionários")]
    [SerializeField] private float workerRowY = -117.4f;
    [SerializeField, Min(1f)] private float maximumWorkerSpacing = 178f;
    [SerializeField] private WorkerVisual[] workerVisuals = new WorkerVisual[5];
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
    [SerializeField, Min(0f)] private float workerEntranceDuration = 0.38f;
    [SerializeField, Min(0f)] private float workerWaveDelay = 0.1f;
    [SerializeField, Min(0f)] private float innerFrameInset = 16f;
    [Header("Transicao entre telas")]
    [SerializeField, Min(0.05f)] private float transitionDuration = 0.62f;
    [SerializeField, Min(0f)] private float transitionDistance = 80f;

    private WorkerAssignment[] workers;
    private SaleItem?[] chosenFood;
    private Action<SaleItem?[]> completed;
    private Action cancelled;
    private bool open;
    private bool transitioning;
    private Image backdrop;
    private Color backdropColor;
    private RectTransform innerViewport;
    private RectTransform[] workerHitAreas = new RectTransform[5];
    private FeedingFoodIcon reservedIcon;
    private Tween backEntrance;
    private Sequence transition;
    private Vector3 panelSceneScale;
    private Vector2 panelHome;
#if UNITY_EDITOR
    private const string PreviewWorkerName = "Prévia trabalhador ";
#endif
    private static readonly SaleItem[] FoodKinds = { SaleItem.Food, SaleItem.Fish,
        SaleItem.Corn, SaleItem.Pumpkin, SaleItem.Jerimum, SaleItem.Milk, SaleItem.Eggs };

    private void Awake()
    {
#if UNITY_EDITOR
        if (Application.isPlaying && panel != null)
            for (int i = panel.childCount - 1; i >= 0; i--)
                if (panel.GetChild(i).name.StartsWith(PreviewWorkerName,
                    StringComparison.Ordinal))
                    Destroy(panel.GetChild(i).gameObject);
#endif
        if (group != null)
        {
            backdrop = group.GetComponent<Image>();
            if (backdrop != null) backdropColor = backdrop.color;
        }
        if (panel != null) panelSceneScale = panel.localScale;
        if (panel != null) panelHome = panel.anchoredPosition;
        EnsureInnerViewport();
        if (finishButton != null) finishButton.onClick.AddListener(Finish);
        if (backButton != null) backButton.onClick.AddListener(Cancel);
        Hide();
    }

    private void OnDestroy()
    {
        if (finishButton != null) finishButton.onClick.RemoveListener(Finish);
        if (backButton != null) backButton.onClick.RemoveListener(Cancel);
        backEntrance?.Kill();
        transition?.Kill();
        if (panel != null) panel.DOKill();
        foreach (RectTransform worker in workerTargets)
            if (worker != null) worker.DOKill();
        foreach (FeedingFoodIcon icon in foodIcons)
            if (icon != null) icon.StopAnimation();
    }

    public void Begin(WorkerAssignment[] dayWorkers, Action<SaleItem?[]> onCompleted,
        Action onCancelled = null)
    {
        transition?.Kill();
        // The scene may keep this panel disabled for editor layout work.
        if (panel != null) panel.gameObject.SetActive(true);
        workers = dayWorkers;
        // Os dois cards atuais já têm tamanho e posição definidos na cena.
        // O arranjo automático só é necessário para slots adicionais.
        if (workers.Length > workerTargets.Length)
            EnsureWorkerTargets(workers.Length);
        ApplyWorkerVisuals();
        reservedIcon = null;
        chosenFood = new SaleItem?[workers.Length];
        FeedingReaction sharedReaction = workerTargets.Length > 0 && workerTargets[0] != null ?
            workerTargets[0].GetComponentInChildren<FeedingReaction>(true) : null;
        for (int i = 0; i < workerTargets.Length; i++)
        {
            if (workerTargets[i] == null) continue;
            Animator character = workerTargets[i].GetComponent<Animator>();
            if (character != null && character.gameObject.activeInHierarchy)
                character.Play("Idle", 0, 0f);
            FeedingReaction reaction = workerTargets[i].GetComponentInChildren<FeedingReaction>(true);
            if (reaction != null)
            {
                reaction.CopyStyleFrom(sharedReaction);
                reaction.ResetVisual();
            }
        }
        completed = onCompleted;
        cancelled = onCancelled;
        open = true;
        backEntrance?.Kill();
        if (backButton != null)
        {
            backButton.gameObject.SetActive(false);
            backButton.transform.localScale = Vector3.one;
        }
        transitioning = true;
        if (backdrop != null)
        {
            backdrop.DOKill();
            backdrop.color = new Color(backdropColor.r, backdropColor.g, backdropColor.b, 0f);
            backdrop.DOFade(backdropColor.a, transitionDuration).SetEase(Ease.OutSine);
        }
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        panel.DOKill();
        RectTransform parent = panel.parent as RectTransform;
        float below = parent != null ? (parent.rect.height + panel.rect.height) * 0.5f +
            transitionDistance : panel.rect.height + transitionDistance;
        panel.anchoredPosition = panelHome + Vector2.down * below;
        panel.localScale = panelSceneScale;
        transition = DOTween.Sequence().SetTarget(this)
            .Append(panel.DOAnchorPos(panelHome, transitionDuration).SetEase(Ease.InOutSine));
        AnimateWorkers();
        Refresh(true, transitionDuration);
        int visibleFoods = 0;
        foreach (SaleItem kind in FoodKinds)
            if (Stock(kind) > 0) visibleFoods++;
        float waveEnd = transitionDuration + Mathf.Max(
            entranceDuration + Mathf.Max(0, visibleFoods - 1) * foodWaveDelay,
            workerEntranceDuration + Mathf.Max(0, workers.Length - 1) * workerWaveDelay);
        transition.AppendInterval(waveEnd - transitionDuration)
            .OnComplete(() => transitioning = false);
        backEntrance = DOVirtual.DelayedCall(waveEnd, ShowBackButton).SetTarget(this);
    }

    private void EnsureInnerViewport()
    {
        if (panel == null || innerViewport != null) return;
        var area = new GameObject("Área interna da alimentação", typeof(RectTransform),
            typeof(RectMask2D));
        innerViewport = (RectTransform)area.transform;
        innerViewport.SetParent(panel, false);
        innerViewport.anchorMin = Vector2.zero;
        innerViewport.anchorMax = Vector2.one;
        innerViewport.offsetMin = new Vector2(innerFrameInset, innerFrameInset);
        innerViewport.offsetMax = new Vector2(-innerFrameInset, -innerFrameInset);
        foreach (RectTransform worker in workerTargets)
            if (worker != null) worker.SetParent(innerViewport, false);
        foreach (FeedingFoodIcon icon in foodIcons)
            if (icon != null) icon.transform.SetParent(innerViewport, false);
    }

    private void AnimateWorkers()
    {
        if (innerViewport == null) return;
        for (int i = 0; i < workerTargets.Length && i < workers.Length; i++)
        {
            RectTransform worker = workerTargets[i];
            if (worker == null) continue;
            worker.DOKill();
            Vector2 home = worker.anchoredPosition;
            worker.anchoredPosition = new Vector2(home.x,
                innerViewport.rect.yMin - worker.rect.yMax - 8f);
            worker.DOAnchorPos(home, workerEntranceDuration)
                .SetDelay(transitionDuration + i * workerWaveDelay)
                .SetEase(Ease.OutBack);
        }
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
            RectTransform target = Instantiate(workerTargets[oldCount - 1],
                innerViewport != null ? innerViewport : panel);
            target.name = "Trabalhador " + (i + 1);
            workerTargets[i] = target;
            workerLabels[i] = target.GetComponentInChildren<Text>(true);
        }
    }

    private void ApplyWorkerVisuals()
    {
        int count = Mathf.Min(workers.Length, workerTargets.Length);
        if (workerHitAreas.Length < count) Array.Resize(ref workerHitAreas, count);
        float availableWidth = innerViewport != null ? innerViewport.rect.width : panel.rect.width;
        for (int i = 0; i < count; i++)
        {
            RectTransform target = workerTargets[i];
            if (target == null) continue;
            WorkerVisual visual = ApplyWorkerAppearance(target, i, count, availableWidth);
            Vector2 size = target.sizeDelta;
            Image characterImage = target.GetComponent<Image>();
            if (characterImage != null) characterImage.raycastTarget = false;
            RectTransform hit = target.Find("Alvo da alimentação") as RectTransform;
            if (hit == null)
            {
                hit = (RectTransform)new GameObject("Alvo da alimentação",
                    typeof(RectTransform)).transform;
                hit.SetParent(target, false);
            }
            hit.anchorMin = hit.anchorMax = new Vector2(0.5f, 0.5f);
            hit.anchoredPosition = Vector2.zero;
            hit.sizeDelta = size + Vector2.one * (2f * visual.hitboxMargin);
            workerHitAreas[i] = hit;
            if (workerLabels[i] == null) continue;
            float spacing = WorkerSpacing(count, availableWidth);
            workerLabels[i].rectTransform.sizeDelta = new Vector2(
                Mathf.Min(152f, Mathf.Max(60f, spacing - 8f)), 60f);
            workerLabels[i].fontSize = count >= 4 ? 12 : 14;
            workerLabels[i].resizeTextForBestFit = true;
            workerLabels[i].resizeTextMinSize = 11;
            workerLabels[i].resizeTextMaxSize = count >= 4 ? 12 : 14;
        }
    }

    private float WorkerSpacing(int count, float availableWidth) => count > 1 ?
        Mathf.Min(maximumWorkerSpacing, (availableWidth - 100f) / (count - 1)) : 0f;

    private WorkerVisual ApplyWorkerAppearance(RectTransform target, int index, int count,
        float availableWidth)
    {
        WorkerVisual visual = workerVisuals != null && index < workerVisuals.Length ?
            workerVisuals[index] : default;
        target.anchoredPosition = visual.useCustomPosition ? visual.position :
            new Vector2((index - (count - 1) * 0.5f) * WorkerSpacing(count, availableWidth),
                workerRowY);
        target.sizeDelta = visual.imageSize.x > 0f && visual.imageSize.y > 0f ?
            visual.imageSize : new Vector2(72f, 72f);
        return visual;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshWorkerPreview;
        UnityEditor.EditorApplication.delayCall += RefreshWorkerPreview;
    }

    private void OnDisable()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshWorkerPreview;
    }

    private void RefreshWorkerPreview()
    {
        if (this == null || panel == null || workerTargets == null || workerTargets.Length == 0)
            return;
        if (Application.isPlaying)
        {
            if (open && workers != null) ApplyWorkerVisuals();
            return;
        }

        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
            !gameObject.scene.IsValid()) return;
        const int count = 5;
        float availableWidth = panel.rect.width - 2f * innerFrameInset;
        RectTransform source = workerTargets[workerTargets.Length - 1];
        if (source == null) return;
        for (int i = 0; i < count; i++)
        {
            RectTransform target;
            if (i < workerTargets.Length)
                target = workerTargets[i];
            else
            {
                string previewName = PreviewWorkerName + (i + 1);
                target = panel.Find(previewName) as RectTransform;
                if (target == null)
                {
                    target = Instantiate(source, panel);
                    target.name = previewName;
                    target.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    Animator animator = target.GetComponent<Animator>();
                    if (animator != null) animator.enabled = false;
                    Text label = target.GetComponentInChildren<Text>(true);
                    if (label != null) label.text = "Trabalhador " + (i + 1);
                }
            }
            if (target == null) continue;
            target.gameObject.SetActive(true);
            WorkerVisual visual = ApplyWorkerAppearance(target, i, count, availableWidth);
            RectTransform hit = target.Find("Alvo da alimentação") as RectTransform;
            if (hit != null)
                hit.sizeDelta = target.sizeDelta + Vector2.one * (2f * visual.hitboxMargin);
        }
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    public bool Reserve(FeedingFoodIcon icon)
    {
        if (!open || transitioning || reservedIcon != null || Stock(icon.Kind) == 0) return false;
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
        if (!open || transitioning || reservedIcon != icon) return null;
        int target = -1;
        for (int i = 0; i < workerTargets.Length && i < chosenFood.Length; i++)
            if (CanFeed(i) && workerHitAreas[i] != null &&
                RectTransformUtility.RectangleContainsScreenPoint(workerHitAreas[i],
                    eventData.position, eventData.pressEventCamera) &&
                (innerViewport == null || RectTransformUtility.RectangleContainsScreenPoint(
                    innerViewport, eventData.position, eventData.pressEventCamera)))
            {
                target = i;
                break;
            }
        if (target < 0 || !Consume(icon.Kind)) return null;
        reservedIcon = null;
        chosenFood[target] = icon.Kind;
        Animator character = workerTargets[target].GetComponent<Animator>();
        if (character != null)
            character.Play(icon.Kind == SaleItem.Food ? "Boa" : "Ok", 0, 0f);
        FeedingReaction reaction = workerTargets[target].GetComponentInChildren<FeedingReaction>(true);
        if (reaction != null) reaction.Show(icon.Kind == SaleItem.Food);
        AudioManager.Play(SoundCue.FeedingAssigned);
        RectTransform worker = workerTargets[target];
        worker.DOKill();
        worker.DOPunchScale(worker.localScale * 0.12f, 0.3f, 1);
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

    private void Refresh(bool animate, float entranceDelay = 0f)
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
                animate, entranceDelay + visibleIndex * foodWaveDelay,
                animate ? entranceDuration : 0.18f);
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
        if (!open || transitioning) return;
        open = false;
        transitioning = true;
        group.blocksRaycasts = true;
        if (reservedIcon != null) CancelReservation(reservedIcon);
        foreach (FeedingFoodIcon icon in foodIcons)
            if (icon != null) icon.StopAnimation();
        AudioManager.Play(SoundCue.FeedingCompleted);
        Action<SaleItem?[]> callback = completed;
        completed = null;
        cancelled = null;
        SaleItem?[] result = chosenFood;
        transition?.Kill();
        RectTransform parent = panel.parent as RectTransform;
        float left = parent != null ? (parent.rect.width + panel.rect.width) * 0.5f +
            transitionDistance : panel.rect.width + transitionDistance;
        transition = DOTween.Sequence().SetTarget(this)
            .Append(panel.DOAnchorPosX(panelHome.x - left,
                transitionDuration).SetEase(Ease.InOutSine))
            .OnComplete(() =>
            {
                Hide();
                callback?.Invoke(result);
            });
        if (backdrop != null)
            backdrop.DOFade(0f, transitionDuration).SetEase(Ease.InOutCubic);
    }

    public void Cancel()
    {
        if (!open || transitioning) return;
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
        transitioning = false;
        if (backdrop != null)
        {
            backdrop.DOKill();
            backdrop.color = backdropColor;
        }
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
        if (panel != null)
        {
            panel.anchoredPosition = panelHome;
            panel.gameObject.SetActive(false);
        }
    }
}
