using DG.Tweening;
using UnityEngine;

// Fluxo de entrada e saída preservado para integração futura à jogabilidade.
public sealed class HouseInteriorTest : MonoBehaviour
{
    [SerializeField] private WorkerAssignment worker;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private Transform outside;
    [SerializeField] private Transform doorway;
    [Tooltip("Ponto para onde o personagem volta ao sair. Pode ser movido durante o Play Mode.")]
    [SerializeField] private Transform returnSpot;
    [SerializeField] private Transform activitySpot;
    [SerializeField, Range(0.25f, 1f)] private float indoorSpriteScale = 0.4f;
    [SerializeField, Min(0.1f)] private float walkingSpeed = 3f;

    private Sequence motion;
    private Vector3 outdoorSpriteScale;
    private bool inside;
    private bool moving;
    private bool reserved;

    public void ToggleHouse()
    {
        if (moving || worker == null || worker.SpriteVisual == null ||
            outside == null || doorway == null || returnSpot == null || activitySpot == null) return;
        if (inside) ExitHouse();
        else if (actions != null && !actions.IsBusy && dayCycle != null &&
                 dayCycle.CanAcceptActions && !worker.UsedToday) EnterHouse();
    }

    private void EnterHouse()
    {
        outdoorSpriteScale = worker.SpriteVisual.localScale;
        moving = reserved = true;
        worker.HousePreviewActive = true;
        worker.SetStatus("entrando em casa");
        actions.SetHousePreviewBusy(true);

        motion = DOTween.Sequence()
            .Append(worker.transform.DOMove(outside.position,
                TravelTime(worker.transform.position, outside.position)))
            .Append(worker.transform.DOMove(doorway.position, 0.38f))
            .Join(worker.SpriteVisual.DOScale(outdoorSpriteScale * indoorSpriteScale, 0.38f)
                .SetEase(Ease.InOutSine))
            .Append(worker.transform.DOMove(activitySpot.position, 0.5f))
            .Append(worker.SpriteVisual.DOPunchScale(
                outdoorSpriteScale * indoorSpriteScale * 0.08f, 0.3f, 4, 0.5f))
            .OnComplete(() =>
            {
                inside = true;
                moving = false;
                worker.SetStatus("dentro de casa");
            });
    }

    private void ExitHouse()
    {
        moving = true;
        worker.SetStatus("saindo de casa");
        motion = DOTween.Sequence()
            .Append(worker.transform.DOMove(doorway.position, 0.5f))
            .Append(worker.transform.DOMove(outside.position, 0.38f))
            .Append(worker.SpriteVisual.DOScale(outdoorSpriteScale, 0.3f)
                .SetEase(Ease.OutBack))
            .Append(worker.transform.DOMove(returnSpot.position,
                TravelTime(outside.position, returnSpot.position)))
            .OnComplete(() =>
            {
                inside = moving = false;
                ReleaseWorker();
            });
    }

    private float TravelTime(Vector3 from, Vector3 destination) => Mathf.Max(0.15f,
        Vector3.Distance(from, destination) / walkingSpeed);

    private void OnDisable()
    {
        motion?.Kill();
        if (!reserved) return;
        if (worker != null)
        {
            worker.transform.position = returnSpot != null ? returnSpot.position : worker.HomePosition;
            if (worker.SpriteVisual != null)
                worker.SpriteVisual.localScale = outdoorSpriteScale;
        }
        inside = moving = false;
        ReleaseWorker();
    }

    private void ReleaseWorker()
    {
        reserved = false;
        if (worker != null)
        {
            worker.HousePreviewActive = false;
            worker.SetStatus("livre");
        }
        if (actions != null) actions.SetHousePreviewBusy(false);
    }

}
