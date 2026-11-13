using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WorkerAssignment : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private GameObject selectionMarker;
    [SerializeField] private Text statusText;
    [SerializeField] private string workerName = "Trabalhador";
    [SerializeField] private ActionResolver actionResolver;
    [SerializeField] private WorkProgress workProgress;
    [SerializeField] private ParticleSystem comadreDizzyPrefab;
    [Header("Pontos de ação")]
    [SerializeField] private Sprite actionPointSprite;
    [SerializeField, Min(0.01f)] private float actionPointSize = 0.16f;
    [SerializeField, Tooltip("Posição em relação ao funcionário: X lateral, Y altura, Z profundidade.")]
    private Vector3 actionPointOffset = new Vector3(0f, 1.65f, 0f);
    [SerializeField, Min(0f)] private float actionPointSpacing = 0.2f;
    [SerializeField, Range(0, 10), InspectorName("Prévia de pontos de ação"),
        Tooltip("Mostra esta quantidade de pontos para ajustar o visual. Zero usa as ações reais; a prévia não concede ações.")]
    private int previewActionPoints;
    [Header("Teste visual 2D")]
    [SerializeField] private Sprite testSprite;
    [SerializeField, Min(0.1f)] private float testSpriteHeight = 1.9f;
    [SerializeField] private Vector3 spriteOffset = new Vector3(0f, 0.8f, 0f);
    [Header("Animação do trabalhador")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] runFrames;
    [SerializeField, Min(1f)] private float idleFps = 8f;
    [SerializeField, Min(1f)] private float runFps = 12f;
    [Header("Percurso da casa (ajustável em Play Mode)")]
    [SerializeField] private Transform homeMarker;
    [SerializeField] private Transform outsideMarker;
    [SerializeField] private Transform doorwayMarker;
    [SerializeField, Range(0.05f, 1f)] private float indoorScale = 0.4f;

    public static WorkerAssignment Selected { get; private set; }
    public bool UsedToday => HousePreviewActive || IsUnavailableToday || actionsRemaining <= 0;
    public bool HousePreviewActive { get; set; }
    public Transform SpriteVisual => spriteVisual;
    public int ActionsRemaining => actionsRemaining;
    public ActivityKind? LastActivity { get; private set; }
    public bool IsUnavailableToday { get; private set; }
    public Vector3 HomePosition => homeMarker != null ? homeMarker.position : initialHomePosition;
    public Transform OutsideMarker => outsideMarker;
    public Transform DoorwayMarker => doorwayMarker;
    public float IndoorScale => indoorScale;
    public int ProductionReduction { get; private set; }
    public WorkProgress Progress => workProgress;
    public string DisplayName => workerName;
    public int AssignmentVersion => actionResolver != null ? actionResolver.AssignmentVersion : 0;

    private int nextDayProductionReduction;
    private bool hovered;
    private int absenceDaysRemaining;
    private int actionsRemaining = 1;
    private int nextDayExtraActions;
    private int coffeeActions;
    private ActivityKind? coffeeExcludedActivity;
    private readonly List<Transform> actionDots = new List<Transform>();
    private Transform actionDotsRoot;
    private ParticleSystem comadreDizzy;
    private Transform spriteVisual;
    private Camera spriteCamera;
    private Vector3 initialHomePosition;
    private Vector3 previousPosition;
    private float animationTime;
    private int animationFrame;
    private bool wasMoving;

    private void Awake()
    {
        if (homeMarker != null)
            transform.position = homeMarker.position;
        initialHomePosition = transform.position;
        previousPosition = transform.position;
        spriteCamera = Camera.main;
        SetupTestSprite();
        if (comadreDizzyPrefab != null)
        {
            comadreDizzy = Instantiate(comadreDizzyPrefab, transform, false);
            comadreDizzy.name = "Tontura - Comadre Fulozinha";
            comadreDizzy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        actionDotsRoot = new GameObject("Ações - " + workerName).transform;
        actionDotsRoot.SetParent(transform.parent, false);
        RefreshActionDots(false);
    }

    private void LateUpdate()
    {
        if (spriteVisual != null)
        {
            if (spriteCamera == null) spriteCamera = Camera.main;
            if (spriteCamera != null)
                spriteVisual.rotation = Quaternion.Euler(0f, spriteCamera.transform.eulerAngles.y, 0f);
            UpdateSpriteAnimation();
        }
        if (actionDotsRoot != null)
        {
            actionDotsRoot.position = transform.position + actionPointOffset;
            if (spriteCamera == null) spriteCamera = Camera.main;
            if (spriteCamera != null) actionDotsRoot.rotation = spriteCamera.transform.rotation;
        }
    }

    private void SetupTestSprite()
    {
        Sprite firstFrame = idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : testSprite;
        if (firstFrame == null) return;
        Transform body = transform.Find("Corpo");
        Transform head = transform.Find("Cabeça");
        if (body != null) body.gameObject.SetActive(false);
        if (head != null) head.gameObject.SetActive(false);

        spriteVisual = transform.Find("Visual 2D - teste");
        if (spriteVisual == null)
        {
            spriteVisual = new GameObject("Visual 2D - teste", typeof(SpriteRenderer)).transform;
            spriteVisual.SetParent(transform, false);
        }
        SpriteRenderer visual = spriteVisual.GetComponent<SpriteRenderer>();
        visual.sprite = firstFrame;
        visual.spriteSortPoint = SpriteSortPoint.Pivot;
        visual.sortingOrder = 100;
        RefreshSpriteAppearance();
    }

    public void RefreshSpriteAppearance()
    {
        if (spriteVisual == null) return;
        SpriteRenderer renderer = spriteVisual.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;
        spriteVisual.localPosition = spriteOffset;
        spriteVisual.localScale = Vector3.one * (testSpriteHeight / renderer.sprite.bounds.size.y);
    }

    private void UpdateSpriteAnimation()
    {
        Vector3 movement = transform.position - previousPosition;
        previousPosition = transform.position;
        bool moving = Time.deltaTime > 0f && movement.sqrMagnitude > 0.0025f * Time.deltaTime * Time.deltaTime;
        Sprite[] frames = moving ? runFrames : idleFrames;
        if (frames == null || frames.Length == 0) return;

        SpriteRenderer renderer = spriteVisual.GetComponent<SpriteRenderer>();
        if (moving && spriteCamera != null)
        {
            float horizontal = Vector3.Dot(movement, spriteCamera.transform.right);
            if (Mathf.Abs(horizontal) > 0.0001f)
                renderer.flipX = horizontal > 0f; // A arte original olha para a esquerda.
        }
        if (moving != wasMoving)
        {
            wasMoving = moving;
            animationTime = 0f;
            animationFrame = 0;
        }
        animationTime += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, moving ? runFps : idleFps);
        while (animationTime >= frameDuration)
        {
            animationTime -= frameDuration;
            animationFrame = (animationFrame + 1) % frames.Length;
        }
        renderer.sprite = frames[animationFrame];
    }

    private void OnDestroy()
    {
        if (actionDotsRoot != null) Destroy(actionDotsRoot.gameObject);
    }

    private void OnEnable()
    {
        if (selectionMarker != null)
            selectionMarker.SetActive(false);
    }

    private void OnDisable()
    {
        if (Selected == this)
            Selected = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TutorialHints.Once("workers", "Clique em um trabalhador para inspecioná-lo e depois use as cartas para escolher sua tarefa.");
        Select();
    }

    public void Select()
    {
        if (Selected != null && Selected.selectionMarker != null)
            Selected.UpdateMarker(false);

        Selected = this;
        UpdateMarker(true);
    }

    public void SetHovered(bool value)
    {
        hovered = value;
        UpdateMarker(Selected == this);
    }

    private void UpdateMarker(bool selected)
    {
        if (selectionMarker != null)
            selectionMarker.SetActive(selected || hovered);
    }

    public void TryAssign(ActivityKind activity, string activityName, ActivityCardDrag card = null)
    {
        if (actionResolver != null)
            actionResolver.TryAssign(this, activity, activityName, card);
    }

    public void SetStatus(string status)
    {
        if (statusText != null)
            statusText.text = workerName + ": " + status;
    }

    public bool CanAct(ActivityKind activity) => !HousePreviewActive && !IsUnavailableToday && actionsRemaining > 0 &&
        (actionsRemaining > coffeeActions || coffeeExcludedActivity != activity);

    public void MarkUsed(string activityName, ActivityKind activity)
    {
        bool spendingCoffeeAction = coffeeActions > 0 && actionsRemaining <= coffeeActions;
        actionsRemaining = Mathf.Max(0, actionsRemaining - 1);
        RefreshActionDots(true);
        if (spendingCoffeeAction)
        {
            coffeeActions--;
            if (coffeeActions > 0) coffeeExcludedActivity = activity;
        }
        else if (coffeeActions > 0) coffeeExcludedActivity = activity;
        if (coffeeActions == 0) coffeeExcludedActivity = null;
        LastActivity = activity;
        Select();
        SetStatus(activityName);
    }

    public void MarkRepeatedAction(string activityName, ActivityKind activity)
    {
        LastActivity = activity;
        Select();
        SetStatus(activityName);
    }

    public void GrantCoffeeAction()
    {
        actionsRemaining++;
        RefreshActionDots(true);
        coffeeActions++;
        coffeeExcludedActivity = LastActivity;
        LastActivity = ActivityKind.Coffee;
    }

    public void ReplaceCoffeeWithReplay()
    {
        // Replay cancels the pending coffee restriction and leaves two actions.
        actionsRemaining = 2;
        RefreshActionDots(true);
        coffeeActions = 0;
        coffeeExcludedActivity = null;
    }

    public void ScheduleExtraAction() => nextDayExtraActions++;

    public void ConfigureNewWorker(string name)
    {
        if (statusText != null)
        {
            float bottom = statusText.rectTransform.anchoredPosition.y;
            foreach (Text existing in statusText.transform.parent.GetComponentsInChildren<Text>(false))
                bottom = Mathf.Min(bottom, existing.rectTransform.anchoredPosition.y);
            statusText = Instantiate(statusText, statusText.transform.parent);
            statusText.name = "Status - " + name;
            RectTransform label = statusText.rectTransform;
            label.anchoredPosition = new Vector2(label.anchoredPosition.x,
                bottom - label.rect.height - 2f);
            RectTransform background = label.parent as RectTransform;
            if (background != null)
                background.sizeDelta = new Vector2(background.sizeDelta.x,
                    background.sizeDelta.y + label.rect.height + 2f);
        }
        workerName = name;
        if (actionDotsRoot != null) actionDotsRoot.name = "Ações - " + name;
        initialHomePosition = transform.position;
        SetStatus("livre");
    }

    public void SetHouseRoute(Transform home, Transform outside, Transform doorway)
    {
        homeMarker = home;
        outsideMarker = outside;
        doorwayMarker = doorway;
    }

    public void JoinDuringDay()
    {
        actionsRemaining = 0;
        RefreshActionDots(false);
        SetStatus("novo morador");
    }

    public void BeginNextDay()
    {
        IsUnavailableToday = absenceDaysRemaining > 0;
        if (IsUnavailableToday) absenceDaysRemaining--;
        if (comadreDizzy != null)
        {
            if (IsUnavailableToday) comadreDizzy.Play(true);
            else comadreDizzy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        actionsRemaining = IsUnavailableToday ? 0 : 1 + nextDayExtraActions;
        RefreshActionDots(true);
        nextDayExtraActions = 0;
        coffeeActions = 0;
        coffeeExcludedActivity = null;
        LastActivity = null;
        hovered = false;
        ProductionReduction = nextDayProductionReduction;
        nextDayProductionReduction = 0;
        SetStatus(IsUnavailableToday ? "ausente" : ProductionReduction > 0 ? "produção reduzida" :
            "livre");
        if (selectionMarker != null)
            selectionMarker.SetActive(false);
        if (Selected == this)
            Selected = null;
    }

    public void ScheduleAbsence(int days)
    {
        absenceDaysRemaining = Mathf.Max(absenceDaysRemaining, days);
    }

    public void ScheduleNextDayCondition(int productionReduction)
    {
        nextDayProductionReduction = Mathf.Max(0, productionReduction);
    }

    private void RefreshActionDots(bool animate)
    {
        if (actionDotsRoot == null) return;
        int visibleCount = previewActionPoints > 0 ? previewActionPoints : actionsRemaining;
        while (actionDots.Count > visibleCount)
        {
            Transform dot = actionDots[actionDots.Count - 1];
            actionDots.RemoveAt(actionDots.Count - 1);
            dot.DOKill();
            if (animate)
                DOTween.Sequence().Append(dot.DOPunchScale(dot.localScale * 0.35f, 0.13f))
                    .Append(dot.DOScale(Vector3.zero, 0.13f))
                    .OnComplete(() => { if (dot != null) Destroy(dot.gameObject); });
            else Destroy(dot.gameObject);
        }
        while (actionDots.Count < visibleCount)
        {
            GameObject icon = new GameObject("Ação " + (actionDots.Count + 1),
                typeof(SpriteRenderer));
            icon.transform.SetParent(actionDotsRoot, false);
            SpriteRenderer visual = icon.GetComponent<SpriteRenderer>();
            visual.sprite = actionPointSprite;
            visual.sortingOrder = 110;
            Transform dot = icon.transform;
            actionDots.Add(dot);
            float spriteHeight = actionPointSprite != null ?
                actionPointSprite.bounds.size.y : 1f;
            Vector3 size = Vector3.one * (actionPointSize / Mathf.Max(0.01f, spriteHeight));
            dot.localScale = animate ? Vector3.zero : size;
            if (animate)
                DOTween.Sequence().Append(dot.DOScale(size, 0.17f).SetEase(Ease.OutBack))
                    .Append(dot.DOPunchScale(size * 0.35f, 0.2f));
        }
        for (int i = 0; i < actionDots.Count; i++)
            actionDots[i].localPosition = new Vector3(
                (i - (actionDots.Count - 1) * 0.5f) * actionPointSpacing, 0f, 0f);
    }

    public void RefreshActionPointAppearance()
    {
        if (actionDotsRoot == null) return;
        RefreshActionDots(false);
        actionDotsRoot.position = transform.position + actionPointOffset;
        float spriteHeight = actionPointSprite != null ?
            actionPointSprite.bounds.size.y : 1f;
        Vector3 scale = Vector3.one * (actionPointSize / Mathf.Max(0.01f, spriteHeight));
        for (int i = 0; i < actionDots.Count; i++)
        {
            Transform dot = actionDots[i];
            if (dot == null) continue;
            dot.DOKill();
            dot.localScale = scale;
            dot.localPosition = new Vector3(
                (i - (actionDots.Count - 1) * 0.5f) * actionPointSpacing, 0f, 0f);
            SpriteRenderer renderer = dot.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.sprite = actionPointSprite;
        }
    }
}
