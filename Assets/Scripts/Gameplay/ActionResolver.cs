using DG.Tweening;
using UnityEngine;

public sealed class ActionResolver : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private HarvestChoiceView harvestChoice;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private Transform forestDestination;
    [SerializeField] private Transform fishingDestination;
    [SerializeField, Min(0)] private int woodReward = 2;
    [SerializeField, Min(0)] private int fishingReward = 2;
    [SerializeField, Min(0.01f)] private float movementDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float woodWorkDuration = 1f;
    [SerializeField, Min(0.01f)] private float fishingWorkDuration = 1f;
    [SerializeField, Min(0.01f)] private float plowWorkDuration = 1f;
    [SerializeField] private Ease movementEase = Ease.InOutSine;

    private WorkerAssignment pendingPlotWorker;
    private ActivityKind pendingPlotAction;
    private CropSettings pendingHarvest;
    private bool awaitingHarvestChoice;
    private int busyWorkers;

    public bool IsBusy => busyWorkers > 0 || awaitingHarvestChoice;
    public int WoodGainedToday { get; private set; }
    public int FoodGainedToday { get; private set; }
    public int PlotsPlowedToday { get; private set; }
    public int CropsHarvestedToday { get; private set; }
    public int HarvestScoreGainedToday { get; private set; }

    public void TryAssign(WorkerAssignment worker, ActivityKind activity, string activityName)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (IsBusy)
        {
            feedback.ShowMessage("Aguarde a atividade atual terminar.");
            return;
        }
        if (activity == ActivityKind.BuySeeds)
        {
            if (merchant != null) merchant.RequestVisit(worker);
            return;
        }
        if (worker.UsedToday)
        {
            feedback.ShowMessage("Esse trabalhador já agiu hoje.");
            return;
        }

        if (pendingPlotWorker != null && !pendingPlotWorker.UsedToday)
            pendingPlotWorker.SetStatus("livre");
        pendingPlotWorker = null;

        if (activity == ActivityKind.Plow || activity == ActivityKind.PlantCorn ||
            activity == ActivityKind.PlantPumpkin || activity == ActivityKind.Harvest)
        {
            pendingPlotWorker = worker;
            pendingPlotAction = activity;
            worker.Select();
            worker.SetStatus("escolha um terreno");
            feedback.ShowMessage("Clique no terreno para " + activityName.ToLowerInvariant() + ".");
            return;
        }

        Transform destination = activity == ActivityKind.CollectWood ? forestDestination : fishingDestination;
        if (destination == null || resources == null)
        {
            feedback.ShowMessage("Destino ou recursos não configurados.");
            return;
        }

        worker.MarkUsed(activityName);
        feedback.ShowMessage(activityName + " em andamento.");
        float workDuration = activity == ActivityKind.CollectWood ? woodWorkDuration : fishingWorkDuration;
        MoveAndWork(worker, destination.position, workDuration, () =>
        {
            worker.SetStatus("ação concluída");
            if (activity == ActivityKind.CollectWood)
            {
                int gained = Mathf.Max(0, woodReward - worker.ProductionReduction);
                resources.AddWood(gained);
                WoodGainedToday += gained;
                feedback.ShowMessage("+" + gained + " madeira");
            }
            else
            {
                int gained = Mathf.Max(0, fishingReward - worker.ProductionReduction);
                resources.AddFood(gained);
                FoodGainedToday += gained;
                feedback.ShowMessage("+" + gained + " comida");
            }
        });
    }

    public void TryUsePlot(Plot plot)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (IsBusy)
        {
            feedback.ShowMessage("Aguarde a atividade atual terminar.");
            return;
        }
        if (pendingPlotWorker == null)
        {
            feedback.ShowMessage("Atribua primeiro um card de terreno a um trabalhador.");
            return;
        }

        if (pendingPlotAction == ActivityKind.Plow)
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
            return;
        }

        if (pendingPlotAction == ActivityKind.PlantCorn || pendingPlotAction == ActivityKind.PlantPumpkin)
        {
            CropType crop = pendingPlotAction == ActivityKind.PlantCorn ? CropType.Corn : CropType.Pumpkin;
            if (!plot.IsPlowed || !plot.IsEmpty)
            {
                feedback.ShowMessage("Plante em um terreno arado e vazio.");
                return;
            }
            if (resources == null || seedCatalog == null || !resources.TrySpendSeed(crop))
            {
                feedback.ShowMessage("Falta semente de " + seedCatalog.Get(crop).displayName + ".");
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
            return;
        }

        if (pendingPlotAction == ActivityKind.Harvest)
        {
            if (seedCatalog == null || harvestChoice == null || !plot.IsReady(dayCycle.CurrentDay, seedCatalog))
            {
                feedback.ShowMessage("A plantação ainda não está pronta.");
                return;
            }
            WorkerAssignment worker = TakePendingWorker();
            worker.MarkUsed("colhendo");
            MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
            {
                CropType crop = plot.Harvest();
                pendingHarvest = seedCatalog.Get(crop);
                awaitingHarvestChoice = true;
                CropsHarvestedToday++;
                worker.SetStatus("colheita concluída");
                harvestChoice.Show(pendingHarvest);
            });
        }
    }

    public void CompleteHarvest(bool asFood)
    {
        if (!awaitingHarvestChoice || pendingHarvest == null)
            return;
        if (asFood)
        {
            resources.AddFood(pendingHarvest.foodYield);
            FoodGainedToday += pendingHarvest.foodYield;
            feedback.ShowMessage("+" + pendingHarvest.foodYield + " comida da colheita");
        }
        else
        {
            resources.AddScore(pendingHarvest.scoreYield);
            HarvestScoreGainedToday += pendingHarvest.scoreYield;
            feedback.ShowMessage("+" + pendingHarvest.scoreYield + " pontos da colheita");
        }
        pendingHarvest = null;
        awaitingHarvestChoice = false;
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

    private WorkerAssignment TakePendingWorker()
    {
        WorkerAssignment worker = pendingPlotWorker;
        pendingPlotWorker = null;
        return worker;
    }

    public void ReturnWorkersHome()
    {
        pendingPlotWorker = null;
        foreach (WorkerAssignment worker in workers)
        {
            if (worker == null || Vector3.Distance(worker.transform.position, worker.HomePosition) < 0.01f)
                continue;
            MoveWorker(worker, worker.HomePosition, null);
        }
    }

    public void BeginNextDay()
    {
        pendingPlotWorker = null;
        WoodGainedToday = 0;
        FoodGainedToday = 0;
        PlotsPlowedToday = 0;
        CropsHarvestedToday = 0;
        HarvestScoreGainedToday = 0;
        foreach (WorkerAssignment worker in workers)
            if (worker != null) worker.BeginNextDay();
    }

    private void MoveWorker(WorkerAssignment worker, Vector3 destination, System.Action onArrived)
    {
        busyWorkers++;
        worker.transform.DOKill();
        worker.transform.DOMove(destination, movementDuration * worker.MovementDurationMultiplier).SetEase(movementEase).OnComplete(() =>
        {
            busyWorkers--;
            onArrived?.Invoke();
        });
    }

    private void MoveAndWork(WorkerAssignment worker, Vector3 destination, float workDuration, System.Action onFinished)
    {
        busyWorkers++;
        worker.SetStatus("caminhando");
        worker.transform.DOKill();
        worker.transform.DOMove(destination, movementDuration * worker.MovementDurationMultiplier).SetEase(movementEase).OnComplete(() =>
        {
            worker.SetStatus("trabalhando");
            if (worker.Progress != null)
                worker.Progress.Show();
            float progress = 0;
            DOTween.To(() => progress, value =>
            {
                progress = value;
                if (worker.Progress != null)
                    worker.Progress.SetProgress(value);
            }, 1f, workDuration).OnComplete(() =>
            {
                if (worker.Progress != null)
                    worker.Progress.Hide();
                busyWorkers--;
                onFinished?.Invoke();
            });
        });
    }
}
