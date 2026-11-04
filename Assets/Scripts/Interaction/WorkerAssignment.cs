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
    public float MovementDurationMultiplier { get; private set; } = 1f;
    public WorkProgress Progress => workProgress;
    public string DisplayName => workerName;

    private int nextDayProductionReduction;
    private float nextDayMovementDurationMultiplier = 1f;
    private bool hovered;
    private int absenceDaysRemaining;
    private int actionsRemaining = 1;
    private int nextDayExtraActions;
    private int coffeeActions;
    private ActivityKind? coffeeExcludedActivity;

    private void Awake()
    {
        HomePosition = transform.position;
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
        coffeeActions++;
        coffeeExcludedActivity = LastActivity;
    }

    public void ReplaceCoffeeWithReplay()
    {
        // Replay cancels the pending coffee restriction and leaves two actions.
        actionsRemaining = 2;
        coffeeActions = 0;
        coffeeExcludedActivity = null;
    }

    public void ScheduleExtraAction() => nextDayExtraActions++;

    public void ConfigureNewWorker(string name)
    {
        if (statusText != null)
        {
            statusText = Instantiate(statusText, statusText.transform.parent);
            statusText.name = "Status - " + name;
            RectTransform label = statusText.rectTransform;
            label.anchoredPosition = new Vector2(label.anchoredPosition.x,
                label.anchoredPosition.y - label.rect.height - 2f);
            RectTransform background = label.parent as RectTransform;
            if (background != null)
                background.sizeDelta = new Vector2(background.sizeDelta.x,
                    background.sizeDelta.y + label.rect.height + 2f);
        }
        workerName = name;
        HomePosition = transform.position;
        SetStatus("livre");
    }

    public void JoinDuringDay()
    {
        actionsRemaining = 0;
        SetStatus("novo morador");
    }

    public void BeginNextDay()
    {
        IsUnavailableToday = absenceDaysRemaining > 0;
        if (IsUnavailableToday) absenceDaysRemaining--;
        actionsRemaining = IsUnavailableToday ? 0 : 1 + nextDayExtraActions;
        nextDayExtraActions = 0;
        coffeeActions = 0;
        coffeeExcludedActivity = null;
        LastActivity = null;
        hovered = false;
        ProductionReduction = nextDayProductionReduction;
        MovementDurationMultiplier = nextDayMovementDurationMultiplier;
        nextDayProductionReduction = 0;
        nextDayMovementDurationMultiplier = 1f;
        SetStatus(IsUnavailableToday ? "ausente" : ProductionReduction > 0 ? "produção reduzida" :
            MovementDurationMultiplier > 1f ? "movimento lento" : "livre");
        if (selectionMarker != null)
            selectionMarker.SetActive(false);
        if (Selected == this)
            Selected = null;
    }

    public void ScheduleAbsence(int days)
    {
        absenceDaysRemaining = Mathf.Max(absenceDaysRemaining, days);
    }

    public void ScheduleNextDayCondition(int productionReduction, float movementDurationMultiplier)
    {
        nextDayProductionReduction = Mathf.Max(0, productionReduction);
        nextDayMovementDurationMultiplier = Mathf.Max(1f, movementDurationMultiplier);
    }
}
