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

    public static WorkerAssignment Selected { get; private set; }
    public bool UsedToday => IsUnavailableToday || actionsRemaining <= 0;
    public int ActionsRemaining => actionsRemaining;
    public ActivityKind? LastActivity { get; private set; }
    public bool IsUnavailableToday { get; private set; }
    public Vector3 HomePosition { get; private set; }
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

    private void Awake()
    {
        HomePosition = transform.position;
        actionDotsRoot = new GameObject("Ações - " + workerName).transform;
        actionDotsRoot.SetParent(transform.parent, false);
        RefreshActionDots(false);
    }

    private void LateUpdate()
    {
        if (actionDotsRoot != null)
            actionDotsRoot.position = transform.position + Vector3.up * 1.65f;
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

    public bool CanAct(ActivityKind activity) => !IsUnavailableToday && actionsRemaining > 0 &&
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
        HomePosition = transform.position;
        SetStatus("livre");
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
        while (actionDots.Count > actionsRemaining)
        {
            Transform dot = actionDots[actionDots.Count - 1];
            actionDots.RemoveAt(actionDots.Count - 1);
            dot.DOKill();
            if (animate)
                DOTween.Sequence().Append(dot.DOPunchScale(Vector3.one * 0.08f, 0.13f))
                    .Append(dot.DOScale(Vector3.zero, 0.13f))
                    .OnComplete(() => { if (dot != null) Destroy(dot.gameObject); });
            else Destroy(dot.gameObject);
        }
        while (actionDots.Count < actionsRemaining)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Ação " + (actionDots.Count + 1);
            sphere.transform.SetParent(actionDotsRoot, false);
            Collider hitbox = sphere.GetComponent<Collider>();
            if (hitbox != null) Destroy(hitbox);
            Transform dot = sphere.transform;
            actionDots.Add(dot);
            dot.localScale = animate ? Vector3.zero : Vector3.one * 0.13f;
            if (animate)
                DOTween.Sequence().Append(dot.DOScale(0.13f, 0.17f).SetEase(Ease.OutBack))
                    .Append(dot.DOPunchScale(Vector3.one * 0.06f, 0.2f));
        }
        for (int i = 0; i < actionDots.Count; i++)
            actionDots[i].localPosition = new Vector3((i - (actionDots.Count - 1) * 0.5f) * 0.2f, 0f, 0f);
    }
}
