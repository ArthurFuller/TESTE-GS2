using UnityEngine;
using UnityEngine.EventSystems;

public sealed class HouseSite : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private ActionResolver actions;
    [SerializeField] private Transform houseTemplate;
    [SerializeField] private WorkerAssignment workerTemplate;
    [SerializeField] private Vector3 workerOffset = new Vector3(0.25f, 0f, -1.65f);
    [SerializeField, Min(0.1f)] private float builderClearance = 0.8f;

    public bool IsBuilt { get; private set; }

    public Vector3 ConstructionPosition
    {
        get
        {
            Collider lot = GetComponent<Collider>();
            float distance = (lot != null ? lot.bounds.extents.z : 0.8f) + builderClearance;
            return transform.position + Vector3.back * distance;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (actions != null) actions.TryBuildHouse(this);
    }

    public WorkerAssignment Build()
    {
        if (IsBuilt || houseTemplate == null || workerTemplate == null) return null;
        IsBuilt = true;
        Transform house = Instantiate(houseTemplate, houseTemplate.parent);
        house.name = "Casinha 3";
        Transform templateLot = houseTemplate.Find("Lote da casinha");
        if (templateLot != null) house.position += transform.position - templateLot.position;
        WorkerAssignment worker = Instantiate(workerTemplate, workerTemplate.transform.parent);
        worker.name = "Trabalhador ilustrativo C";
        worker.transform.position = transform.position + workerOffset;
        worker.ConfigureNewWorker("Trabalhador C");
        worker.JoinDuringDay();
        Renderer surface = GetComponent<Renderer>();
        if (surface != null) surface.enabled = false;
        Collider target = GetComponent<Collider>();
        if (target != null) target.enabled = false;
        return worker;
    }
}
