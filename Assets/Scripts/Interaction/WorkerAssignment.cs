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
    public bool UsedToday { get; private set; }
    public Vector3 HomePosition { get; private set; }
    public int ProductionReduction { get; private set; }
    public float MovementDurationMultiplier { get; private set; } = 1f;
    public WorkProgress Progress => workProgress;

    private int nextDayProductionReduction;
    private float nextDayMovementDurationMultiplier = 1f;
    private bool hovered;

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

    public void TryAssign(ActivityKind activity, string activityName)
    {
        if (actionResolver != null)
            actionResolver.TryAssign(this, activity, activityName);
    }

    public void SetStatus(string status)
    {
        if (statusText != null)
            statusText.text = workerName + ": " + status;
    }

    public void MarkUsed(string activityName)
    {
        UsedToday = true;
        Select();
        SetStatus(activityName);
    }

    public void BeginNextDay()
    {
        UsedToday = false;
        hovered = false;
        ProductionReduction = nextDayProductionReduction;
        MovementDurationMultiplier = nextDayMovementDurationMultiplier;
        nextDayProductionReduction = 0;
        nextDayMovementDurationMultiplier = 1f;
        SetStatus(ProductionReduction > 0 ? "produção reduzida" :
            MovementDurationMultiplier > 1f ? "movimento lento" : "livre");
        if (selectionMarker != null)
            selectionMarker.SetActive(false);
        if (Selected == this)
            Selected = null;
    }

    public void ScheduleNextDayCondition(int productionReduction, float movementDurationMultiplier)
    {
        nextDayProductionReduction = Mathf.Max(0, productionReduction);
        nextDayMovementDurationMultiplier = Mathf.Max(1f, movementDurationMultiplier);
    }
}
