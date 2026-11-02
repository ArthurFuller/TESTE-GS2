using DG.Tweening;
using UnityEngine;

public sealed class ActionResolver : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DayEventController dayEvents;
    [SerializeField] private GatheringMinigame gatheringGame;
    [SerializeField] private FoodPreparationPanel foodPreparationPanel;
    [SerializeField] private InventoryTrading inventoryTrading;
    [SerializeField] private Transform foodPreparationDestination;
    [SerializeField, Min(0.01f)] private float foodPreparationDuration = 8f;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private Transform forestDestination;
    [SerializeField] private Transform fishingDestination;
    [SerializeField, Min(0)] private int woodReward = 2;
    [SerializeField, Min(0.01f)] private float movementDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float plowWorkDuration = 1f;
    [SerializeField] private Ease movementEase = Ease.InOutSine;

    private WorkerAssignment pendingPlotWorker;
    private ActivityKind pendingPlotAction;
    private int busyWorkers;
    private WorkerAssignment pendingFoodWorker;
    private WorkerAssignment gatheringWorker;

    public bool IsBusy => busyWorkers > 0 || pendingFoodWorker != null;
    public int WoodGainedToday { get; private set; }
    public int FoodGainedToday { get; private set; }
    public int FishGainedToday { get; private set; }
    public int CropsStoredToday { get; private set; }
    public int PlotsPlowedToday { get; private set; }
    public int CropsHarvestedToday { get; private set; }

    public void TryAssign(WorkerAssignment worker, ActivityKind activity, string activityName)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (pendingFoodWorker != null)
        {
            feedback.ShowMessage("Confirme ou cancele o preparo de comida primeiro.");
            return;
        }
        if (activity == ActivityKind.BuySeeds)
        {
            if (merchant != null) merchant.RequestVisit(worker);
            return;
        }
        if (worker.IsUnavailableToday)
        {
            feedback.ShowMessage("Esse trabalhador está ausente hoje.");
            return;
        }
        if (worker.UsedToday)
        {
            feedback.ShowMessage("Esse trabalhador já agiu hoje.");
            return;
        }
        if ((activity == ActivityKind.CollectWood || activity == ActivityKind.Fish) &&
            gatheringGame != null && gatheringWorker != null)
        {
            feedback.ShowMessage("Aguarde a atividade de coleta em andamento.");
            return;
        }

        if (activity == ActivityKind.PrepareFood)
        {
            AssignFoodPreparation(worker);
            return;
        }

        ClearPendingPlot();

        if (activity == ActivityKind.Plow || activity == ActivityKind.PlantCorn ||
            activity == ActivityKind.PlantPumpkin || activity == ActivityKind.Harvest)
        {
            SelectPlot(worker, activity, activityName);
            return;
        }

        StartGathering(worker, activity, activityName);
    }

    private void AssignFoodPreparation(WorkerAssignment worker)
    {
        if (foodPreparationPanel == null || inventoryTrading == null || foodPreparationDestination == null)
        {
            feedback.ShowMessage("Preparação de comida não configurada.");
            return;
        }
        ClearPendingPlot();
        pendingFoodWorker = worker;
        worker.Select();
        worker.SetStatus("indo à bancada");
        MoveWorker(worker, foodPreparationDestination.position, () =>
        {
            if (pendingFoodWorker != worker) return;
            worker.SetStatus("escolha os ingredientes");
            foodPreparationPanel.Show();
        });
    }

    private void ClearPendingPlot()
    {
        if (pendingPlotWorker != null && !pendingPlotWorker.UsedToday)
            pendingPlotWorker.SetStatus("livre");
        pendingPlotWorker = null;
    }

    private void SelectPlot(WorkerAssignment worker, ActivityKind activity, string activityName)
    {
        pendingPlotWorker = worker;
        pendingPlotAction = activity;
        worker.Select();
        worker.SetStatus("escolha um terreno");
        feedback.ShowMessage("Clique no terreno para " + activityName.ToLowerInvariant() + ".");
    }

    private void StartGathering(WorkerAssignment worker, ActivityKind activity, string activityName)
    {
        Transform destination = activity == ActivityKind.CollectWood ? forestDestination : fishingDestination;
        if (destination == null || resources == null)
        {
            feedback.ShowMessage("Destino ou recursos não configurados.");
            return;
        }
        if (gatheringGame == null || !gatheringGame.CanStart())
        {
            feedback.ShowMessage("Minigame de coleta não configurado.");
            return;
        }

        gatheringWorker = worker;
        worker.MarkUsed(activityName);
        feedback.ShowMessage(activityName + " em andamento.");
        MoveToGatheringGame(worker, destination.position, activity,
            rating => CompleteGathering(worker, activity, rating));
    }

    private void CompleteGathering(WorkerAssignment worker, ActivityKind activity, int rating)
    {
        if (rating < 0)
        {
            worker.SetStatus("ação cancelada");
            feedback.ShowMessage("Minigame de coleta indisponível.");
            return;
        }
        worker.SetStatus("ação concluída");
        if (activity == ActivityKind.CollectWood)
        {
            int gained = Mathf.Max(0, woodReward + rating - 1 - worker.ProductionReduction);
            resources.AddWood(gained);
            if (dayEvents != null) dayEvents.RecordWood(gained);
            WoodGainedToday += gained;
            feedback.ShowMessage("+" + gained + " madeira");
        }
        else
        {
            int gained = 2 + rating;
            resources.AddFish(gained);
            FishGainedToday += gained;
            feedback.ShowMessage("+" + gained + " peixe(s) guardados");
        }
    }

    public void TryUsePlot(Plot plot)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (pendingFoodWorker != null)
        {
            feedback.ShowMessage("Confirme ou cancele o preparo de comida primeiro.");
            return;
        }
        if (pendingPlotWorker == null)
        {
            feedback.ShowMessage("Atribua primeiro um card de terreno a um trabalhador.");
            return;
        }

        switch (pendingPlotAction)
        {
            case ActivityKind.Plow: PlowPlot(plot); break;
            case ActivityKind.PlantCorn: PlantPlot(plot, CropType.Corn); break;
            case ActivityKind.PlantPumpkin: PlantPlot(plot, CropType.Pumpkin); break;
            case ActivityKind.Harvest: HarvestPlot(plot); break;
        }
    }

    private void PlowPlot(Plot plot)
    {
        if (plot.IsPlowed)
        {
            feedback.ShowMessage("Esse terreno já está arado.");
            return;
        }
        WorkerAssignment worker = TakePendingWorker();
        worker.MarkUsed("arando");
        MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
        {
            plot.Plow();
            PlotsPlowedToday++;
            worker.SetStatus("terreno arado");
            feedback.ShowMessage("Terreno preparado para plantio.");
        });
    }

    private void PlantPlot(Plot plot, CropType crop)
    {
        if (!plot.IsPlowed || !plot.IsEmpty)
        {
            feedback.ShowMessage("Plante em um terreno arado e vazio.");
            return;
        }
        if (resources == null || seedCatalog == null || !resources.TrySpendSeed(crop))
        {
            feedback.ShowMessage("Falta semente de " + (seedCatalog != null ? seedCatalog.Get(crop).displayName : crop.ToString()) + ".");
            return;
        }
        WorkerAssignment worker = TakePendingWorker();
        worker.MarkUsed("plantando");
        MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
        {
            plot.Plant(crop, dayCycle.CurrentDay);
            worker.SetStatus("plantio concluído");
            feedback.ShowMessage(seedCatalog.Get(crop).displayName + " plantado.");
        });
    }

    private void HarvestPlot(Plot plot)
    {
        if (seedCatalog == null || resources == null || !plot.IsReady(dayCycle.CurrentDay, seedCatalog))
        {
            feedback.ShowMessage("A plantação ainda não está pronta.");
            return;
        }
        WorkerAssignment worker = TakePendingWorker();
        worker.MarkUsed("colhendo");
        MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
        {
            CropType crop = plot.Harvest();
            CropSettings settings = seedCatalog.Get(crop);
            int gained = Mathf.Max(0, settings.foodYield - worker.ProductionReduction -
                (dayEvents != null ? dayEvents.HarvestReduction : 0));
            resources.AddCrop(crop, gained);
            CropsStoredToday += gained;
            CropsHarvestedToday++;
            worker.SetStatus("colheita concluída");
            feedback.ShowMessage("+" + gained + " " + settings.displayName + " guardado(s)");
        });
    }

    public bool ConfirmFoodPreparation(int fish, int corn, int pumpkin)
    {
        if (pendingFoodWorker == null || pendingFoodWorker.UsedToday || inventoryTrading == null ||
            resources == null || foodPreparationDestination == null) return false;
        if (!inventoryTrading.TryPrepare(fish, corn, pumpkin, out int preparedFood))
        {
            feedback.ShowMessage("Selecione itens disponíveis para preparar comida.");
            return false;
        }
        WorkerAssignment worker = pendingFoodWorker;
        pendingFoodWorker = null;
        worker.MarkUsed("preparando comida");
        WorkAtStation(worker, foodPreparationDuration, () =>
        {
            resources.AddFood(preparedFood);
            FoodGainedToday += preparedFood;
            worker.SetStatus("comida preparada");
            feedback.ShowMessage("+" + preparedFood + " comida preparada");
        });
        return true;
    }

    public void CancelFoodPreparation()
    {
        if (pendingFoodWorker != null && !pendingFoodWorker.UsedToday) pendingFoodWorker.SetStatus("livre");
        pendingFoodWorker = null;
    }

    public void TravelToMerchant(WorkerAssignment worker, Vector3 destination, System.Action onArrived)
    {
        worker.SetStatus("indo à barraca");
        MoveWorker(worker, destination, () =>
        {
            worker.SetStatus("na barraca");
            onArrived?.Invoke();
        });
    }

    public bool IsWorkerStoppedNear(WorkerAssignment worker, Vector3 destination, float radius)
    {
        return worker != null &&
            Vector3.Distance(worker.transform.position, destination) <= radius &&
            !DOTween.IsTweening(worker.transform);
    }

    private WorkerAssignment TakePendingWorker()
    {
        WorkerAssignment worker = pendingPlotWorker;
        pendingPlotWorker = null;
        pendingFoodWorker = null;
        return worker;
    }

    public void ReturnWorkersHome()
    {
        pendingPlotWorker = null;
        foreach (WorkerAssignment worker in workers)
        {
            if (worker == null || Vector3.Distance(worker.transform.position, worker.HomePosition) < 0.01f)
                continue;
            MoveWorker(worker, worker.HomePosition, null, false);
        }
    }

    public void BeginNextDay()
    {
        pendingPlotWorker = null;
        pendingFoodWorker = null;
        WoodGainedToday = 0;
        FoodGainedToday = 0;
        FishGainedToday = 0;
        CropsStoredToday = 0;
        PlotsPlowedToday = 0;
        CropsHarvestedToday = 0;
        foreach (WorkerAssignment worker in workers)
            if (worker != null) worker.BeginNextDay();
    }

    private void MoveWorker(WorkerAssignment worker, Vector3 destination, System.Action onArrived,
        bool countAsBusy = true)
    {
        if (countAsBusy) busyWorkers++;
        worker.transform.DOKill();
        worker.transform.DOMove(destination, movementDuration * worker.MovementDurationMultiplier).SetEase(movementEase).OnComplete(() =>
        {
            if (countAsBusy) busyWorkers--;
            onArrived?.Invoke();
        });
    }

    private void MoveAndWork(WorkerAssignment worker, Vector3 destination, float workDuration, System.Action onFinished)
    {
        worker.SetStatus("caminhando");
        MoveWorker(worker, destination, () => WorkAtStation(worker, workDuration, onFinished));
    }

    private void WorkAtStation(WorkerAssignment worker, float workDuration, System.Action onFinished)
    {
        busyWorkers++;
        worker.SetStatus("trabalhando");
        if (worker.Progress != null) worker.Progress.Show();
        float progress = 0;
        DOTween.To(() => progress, value =>
        {
            progress = value;
            if (worker.Progress != null) worker.Progress.SetProgress(value);
        }, 1f, workDuration).OnComplete(() =>
        {
            if (worker.Progress != null) worker.Progress.Hide();
            busyWorkers--;
            onFinished?.Invoke();
        });
    }

    private void MoveToGatheringGame(WorkerAssignment worker, Vector3 destination, ActivityKind activity,
        System.Action<int> onFinished)
    {
        worker.SetStatus("caminhando");
        MoveWorker(worker, destination, () =>
        {
            busyWorkers++;
            worker.SetStatus("atividade interativa");
            gatheringGame.Begin(activity, rating =>
            {
                busyWorkers--;
                if (gatheringWorker == worker) gatheringWorker = null;
                onFinished(rating);
            }, activity == ActivityKind.Fish && dayEvents != null ? dayEvents.FishingReduction : 0);
        });
    }
}
