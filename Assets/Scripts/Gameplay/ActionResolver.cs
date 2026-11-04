using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActionResolver : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private DayCycle dayCycle;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DayEventController dayEvents;
    [SerializeField] private GatheringMinigame gatheringGame;
    [SerializeField] private CommunityCamera communityCamera;
    [SerializeField] private Button forestVisitButton;
    [SerializeField] private Text forestVisitLabel;
    [SerializeField] private Button fishingVisitButton;
    [SerializeField] private Text fishingVisitLabel;
    [SerializeField] private FoodPreparationPanel foodPreparationPanel;
    [SerializeField] private InventoryTrading inventoryTrading;
    [SerializeField] private Transform foodPreparationDestination;
    [SerializeField, Min(0.01f)] private float foodPreparationDuration = 8f;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private Transform forestDestination;
    [SerializeField] private Transform fishingDestination;
    [SerializeField] private Vector3 forestSecondOffset = new Vector3(0f, 0f, 1.2f);
    [SerializeField] private Vector3 fishingSecondOffset = new Vector3(0f, 0f, 1.2f);
    [SerializeField] private Transform villageExit;
    [SerializeField] private Transform fencePassage;
    [SerializeField, Min(0)] private int woodReward = 2;
    [SerializeField, Min(1)] private int gambleStake = 10;
    [SerializeField, Min(0.1f)] private float coinTossDuration = 0.65f;
    [SerializeField, Min(0.01f)] private float movementDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float outsideMovementDuration = 7f;
    [SerializeField, Min(0.01f)] private float plowWorkDuration = 1f;
    [SerializeField] private Ease movementEase = Ease.InOutSine;
    [SerializeField, Min(0f)] private float arrivalStillTime = 0.2f;

    private sealed class GatheringTask
    {
        public readonly ActivityKind Activity;
        public readonly Transform Destination;
        public readonly Button Button;
        public readonly Text Label;
        public readonly Vector3 Offset;
        public WorkerAssignment Worker;
        public ActivityKind SelectedActivity;
        public bool Arrived;
        public bool Started;
        public float StillTime;
        public Vector3 LastPosition;

        public Vector3 TargetPosition => Destination.position + Offset;

        public GatheringTask(ActivityKind activity, Transform destination, Button button, Text label,
            Vector3 offset)
        {
            Activity = activity;
            Destination = destination;
            Button = button;
            Label = label;
            Offset = offset;
        }
    }

    private WorkerAssignment pendingPlotWorker;
    private ActivityCardDrag pendingPlotCard;
    private ActivityKind pendingPlotAction;
    private int busyWorkers;
    private WorkerAssignment pendingFoodWorker;
    private ActivityCardDrag pendingFoodCard;
    private bool pendingFoodPlus;
    private GatheringTask forestTask;
    private GatheringTask fishingTask;
    private GatheringTask forestTaskSecond;
    private GatheringTask fishingTaskSecond;
    private GatheringTask activeMinigame;
    private ActivityKind? lastCardKind;
    private WorkerAssignment lastCardWorker;

    public bool IsBusy => busyWorkers > 0 || pendingFoodWorker != null ||
        forestTask?.Worker != null || fishingTask?.Worker != null ||
        forestTaskSecond?.Worker != null || fishingTaskSecond?.Worker != null;
    public bool GatheringNeedsPlayer =>
        (forestTask?.Worker != null && forestTask.Arrived && !forestTask.Started) ||
        (fishingTask?.Worker != null && fishingTask.Arrived && !fishingTask.Started) ||
        (forestTaskSecond?.Worker != null && forestTaskSecond.Arrived && !forestTaskSecond.Started) ||
        (fishingTaskSecond?.Worker != null && fishingTaskSecond.Arrived && !fishingTaskSecond.Started);
    public int WoodGainedToday { get; private set; }
    public int FoodGainedToday { get; private set; }
    public int FishGainedToday { get; private set; }
    public int CropsStoredToday { get; private set; }
    public int PlotsPlowedToday { get; private set; }
    public int CropsHarvestedToday { get; private set; }
    public event System.Action<ActivityCardDrag> CardUsed;

    private void Start()
    {
        forestTask = new GatheringTask(ActivityKind.CollectWood, forestDestination,
            forestVisitButton, forestVisitLabel, Vector3.zero);
        forestTaskSecond = new GatheringTask(ActivityKind.CollectWood, forestDestination,
            forestVisitButton, forestVisitLabel, forestSecondOffset);
        fishingTask = new GatheringTask(ActivityKind.Fish, fishingDestination,
            fishingVisitButton, fishingVisitLabel, Vector3.zero);
        fishingTaskSecond = new GatheringTask(ActivityKind.Fish, fishingDestination,
            fishingVisitButton, fishingVisitLabel, fishingSecondOffset);
        if (forestVisitButton != null)
        {
            forestVisitButton.onClick.AddListener(VisitForestWorker);
            forestVisitButton.gameObject.SetActive(false);
        }
        if (fishingVisitButton != null)
        {
            fishingVisitButton.onClick.AddListener(VisitFishingWorker);
            fishingVisitButton.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (forestVisitButton != null)
            forestVisitButton.onClick.RemoveListener(VisitForestWorker);
        if (fishingVisitButton != null)
            fishingVisitButton.onClick.RemoveListener(VisitFishingWorker);
    }

    private void Update()
    {
        UpdateGathering(forestTask);
        UpdateGathering(fishingTask);
        UpdateGathering(forestTaskSecond);
        UpdateGathering(fishingTaskSecond);
    }

    public void TryAssign(WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card = null)
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
        bool replaying = activity == ActivityKind.Replay;
        if (replaying)
        {
            if (lastCardKind == null || !CanReplay(lastCardKind.Value))
            {
                feedback.ShowMessage("Replay exige uma última carta de ação válida.");
                return;
            }
            if (lastCardKind == ActivityKind.Coffee)
            {
                if (worker != lastCardWorker)
                {
                    feedback.ShowMessage("Use Replay no trabalhador que recebeu Cafézinho.");
                    return;
                }
                worker.ReplaceCoffeeWithReplay();
                worker.Select();
                worker.SetStatus("duas ações disponíveis");
                RecordCard(ActivityKind.Replay, worker, card);
                feedback.ShowMessage("Replay interrompeu o Cafézinho: duas ações disponíveis.");
                return;
            }
            activity = lastCardKind.Value;
        }
        if (activity == ActivityKind.Coffee)
        {
            worker.GrantCoffeeAction();
            worker.Select();
            worker.SetStatus("ação extra disponível");
            RecordCard(activity, worker, card);
            feedback.ShowMessage("Cafézinho: uma ação diferente disponível.");
            return;
        }
        if (!replaying && !worker.CanAct(activity))
        {
            feedback.ShowMessage(worker.UsedToday ? "Esse trabalhador já agiu hoje." :
                "Cafézinho exige uma ação diferente.");
            return;
        }
        if (activity == ActivityKind.Gamble)
        {
            if (resources == null || !resources.TrySpendCoins(gambleStake))
            {
                feedback.ShowMessage("Faltam moedas para a aposta.");
                return;
            }
            CommitAction(worker, activity, activityName, card);
            worker.SetStatus("lançando moeda");
            feedback.ShowMessage("Moeda no ar...");
            busyWorkers++;
            Sequence toss = DOTween.Sequence();
            Transform visual = card != null ? card.transform : null;
            Vector3 original = visual != null ? visual.localEulerAngles : Vector3.zero;
            if (card != null)
            {
                toss.Append(visual.DOLocalRotate(
                    new Vector3(original.x, original.y + 720f, original.z), coinTossDuration,
                    RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            }
            else toss.AppendInterval(coinTossDuration);
            toss.OnComplete(() =>
            {
                if (visual != null) visual.localEulerAngles = original;
                busyWorkers--;
                bool won = Random.value < 0.5f;
                if (won) resources.AddCoins(gambleStake * 2);
                worker.SetStatus(won ? "aposta vencida" : "aposta perdida");
                feedback.ShowMessage(won ? "Jogo do Bicho: +" + gambleStake + " moedas líquidas." :
                    "Jogo do Bicho: -" + gambleStake + " moedas.");
            });
            return;
        }
        if (activity == ActivityKind.Rest)
        {
            CommitAction(worker, activity, activityName, card);
            worker.ScheduleExtraAction();
            feedback.ShowMessage("Descanso: ação extra amanhã. Alimentação normal hoje.");
            return;
        }
        bool woodActivity = activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus;
        bool fishActivity = activity == ActivityKind.Fish || activity == ActivityKind.FishPlus;
        GatheringTask task = woodActivity ? FreeTask(forestTask, forestTaskSecond) :
            fishActivity ? FreeTask(fishingTask, fishingTaskSecond) : null;
        if ((woodActivity || fishActivity) && task == null)
        {
            feedback.ShowMessage("Os dois postos dessa área já estão ocupados.");
            return;
        }

        if (activity == ActivityKind.PrepareFood || activity == ActivityKind.FoodPlus)
        {
            AssignFoodPreparation(worker, activity, card);
            return;
        }

        ClearPendingPlot();

        if (activity == ActivityKind.Plow || activity == ActivityKind.PlantCorn ||
            activity == ActivityKind.PlantPumpkin || activity == ActivityKind.PlantJerimum ||
            activity == ActivityKind.Harvest ||
            activity == ActivityKind.WateringCan || activity == ActivityKind.Fertilizer ||
            activity == ActivityKind.BuildFence || activity == ActivityKind.BuildHouse)
        {
            SelectPlot(worker, activity, activityName, card);
            return;
        }

        if (task == null)
        {
            feedback.ShowMessage("Essa carta ainda precisa de um alvo válido.");
            return;
        }

        StartGathering(task, worker, activity, activityName, card);
    }

    private static GatheringTask FreeTask(GatheringTask first, GatheringTask second) =>
        first != null && first.Worker == null ? first :
        second != null && second.Worker == null ? second : null;

    private void RecordCard(ActivityKind activity, WorkerAssignment worker, ActivityCardDrag card)
    {
        lastCardKind = card != null ? card.Kind : activity;
        lastCardWorker = worker;
        CardUsed?.Invoke(card);
    }

    private static bool CanReplay(ActivityKind activity) => activity == ActivityKind.Plow ||
        activity == ActivityKind.PrepareFood || activity == ActivityKind.FoodPlus ||
        activity == ActivityKind.CollectWood ||
        activity == ActivityKind.WoodPlus || activity == ActivityKind.Fish ||
        activity == ActivityKind.FishPlus || activity == ActivityKind.PlantCorn ||
        activity == ActivityKind.PlantPumpkin || activity == ActivityKind.PlantJerimum ||
        activity == ActivityKind.Coffee;

    private void CommitAction(WorkerAssignment worker, ActivityKind activity, string name,
        ActivityCardDrag card)
    {
        if (card != null && card.Kind == ActivityKind.Replay)
            worker.MarkRepeatedAction(name, activity);
        else
            worker.MarkUsed(name, activity);
        RecordCard(activity, worker, card);
    }

    private void AssignFoodPreparation(WorkerAssignment worker, ActivityKind activity, ActivityCardDrag card)
    {
        if (foodPreparationPanel == null || inventoryTrading == null || foodPreparationDestination == null)
        {
            feedback.ShowMessage("Preparação de comida não configurada.");
            return;
        }
        ClearPendingPlot();
        pendingFoodWorker = worker;
        pendingFoodCard = card;
        pendingFoodPlus = activity == ActivityKind.FoodPlus;
        worker.Select();
        worker.SetStatus("indo à bancada");
        MoveWorker(worker, foodPreparationDestination.position, () =>
        {
            if (pendingFoodWorker != worker) return;
            worker.SetStatus("escolha os ingredientes");
            foodPreparationPanel.Show(pendingFoodPlus);
        });
    }

    private void ClearPendingPlot()
    {
        if (pendingPlotWorker != null && !pendingPlotWorker.UsedToday)
            pendingPlotWorker.SetStatus("livre");
        pendingPlotWorker = null;
        pendingPlotCard = null;
    }

    private void SelectPlot(WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card)
    {
        pendingPlotWorker = worker;
        pendingPlotCard = card;
        pendingPlotAction = activity;
        worker.Select();
        bool buildingHouse = activity == ActivityKind.BuildHouse;
        worker.SetStatus(buildingHouse ? "escolha um lote" : "escolha um terreno");
        feedback.ShowMessage("Clique no " + (buildingHouse ? "lote" : "terreno") + " para " +
            activityName.ToLowerInvariant() + ".");
    }

    private void StartGathering(GatheringTask task, WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card)
    {
        if (task == null || task.Destination == null || villageExit == null ||
            fencePassage == null || resources == null)
        {
            feedback.ShowMessage("Destino ou recursos não configurados.");
            return;
        }
        if (gatheringGame == null || !gatheringGame.CanStart() ||
            communityCamera == null || task.Button == null)
        {
            feedback.ShowMessage("Minigame de coleta não configurado.");
            return;
        }

        task.Worker = worker;
        task.Arrived = false;
        task.Started = false;
        task.StillTime = 0f;
        task.SelectedActivity = activity;
        CommitAction(worker, activity, activityName, card);
        feedback.ShowMessage(activityName + " em andamento. Use o botão para acompanhar.");
        UpdateVisitButton(task);
        MoveToGatheringArea(task);
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
        if (activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus)
        {
            int gained = woodReward + rating + (activity == ActivityKind.WoodPlus ? 1 : 0);
            gained = Mathf.Max(0, gained - worker.ProductionReduction);
            resources.AddWood(gained);
            if (dayEvents != null) dayEvents.RecordWood(gained);
            WoodGainedToday += gained;
            feedback.ShowMessage("+" + gained + " madeira");
        }
        else
        {
            int gained = 1 + Mathf.Min(1, rating) +
                (activity == ActivityKind.FishPlus ? 1 : 0);
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
            case ActivityKind.PlantJerimum: PlantPlot(plot, CropType.Jerimum); break;
            case ActivityKind.Harvest: HarvestPlot(plot); break;
            case ActivityKind.WateringCan:
            case ActivityKind.Fertilizer: AcceleratePlot(plot); break;
            case ActivityKind.BuildFence: FencePlot(plot); break;
        }
    }

    private void FencePlot(Plot plot)
    {
        if (plot.IsFenced)
        {
            feedback.ShowMessage("Esse terreno já está cercado.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.BuildFence, "construindo cerca", card);
        MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
        {
            plot.BuildFence();
            worker.SetStatus("cerca construída");
            feedback.ShowMessage("Terreno cercado.");
        });
    }

    public void TryBuildHouse(HouseSite site)
    {
        if (site == null || site.IsBuilt || pendingPlotWorker == null ||
            pendingPlotAction != ActivityKind.BuildHouse || dayCycle == null ||
            !dayCycle.CanAcceptActions) return;
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment builder = TakePendingWorker();
        CommitAction(builder, ActivityKind.BuildHouse, "construindo casa", card);
        MoveAndWork(builder, site.ConstructionPosition, plowWorkDuration, () =>
        {
            WorkerAssignment resident = site.Build();
            if (resident != null)
            {
                RegisterWorker(resident);
                dayCycle.RegisterWorker(resident);
                if (dayEvents != null) dayEvents.RegisterWorker(resident);
                feedback.ShowMessage("Casa construída. Um novo trabalhador chegou.");
            }
            builder.SetStatus("voltando para casa");
            MoveWorker(builder, builder.HomePosition, () => builder.SetStatus("em casa"));
        });
    }

    private void RegisterWorker(WorkerAssignment worker)
    {
        if (worker == null || System.Array.IndexOf(workers, worker) >= 0) return;
        var updated = new System.Collections.Generic.List<WorkerAssignment>(workers) { worker };
        workers = updated.ToArray();
    }

    private void AcceleratePlot(Plot plot)
    {
        if (plot.IsEmpty || plot.IsReady(dayCycle.CurrentDay, seedCatalog))
        {
            feedback.ShowMessage("Escolha uma plantação ainda em crescimento.");
            return;
        }
        ActivityKind activity = pendingPlotAction;
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, activity, activity.ToString(), card);
        MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
        {
            plot.AdvanceGrowth(1);
            plot.RefreshGrowth(dayCycle.CurrentDay, seedCatalog);
            worker.SetStatus("crescimento acelerado");
            feedback.ShowMessage("Plantação avançou um dia.");
        });
    }

    private void PlowPlot(Plot plot)
    {
        if (plot.IsPlowed)
        {
            feedback.ShowMessage("Esse terreno já está arado.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.Plow, "arando", card);
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
        if (seedCatalog == null)
        {
            feedback.ShowMessage("Culturas não configuradas.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, pendingPlotAction, "plantando", card);
        MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
        {
            plot.Plant(crop, dayCycle.CurrentDay, seedCatalog);
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
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.Harvest, "colhendo", card);
        MoveAndWork(worker, plot.transform.position, plowWorkDuration, () =>
        {
            int harvestYield = plot.HarvestYield(dayCycle.CurrentDay, seedCatalog);
            CropType crop = plot.Harvest();
            CropSettings settings = seedCatalog.Get(crop);
            int gained = Mathf.Max(0, harvestYield - worker.ProductionReduction -
                (dayEvents != null ? dayEvents.HarvestReduction : 0));
            resources.AddCrop(crop, gained);
            CropsStoredToday += gained;
            CropsHarvestedToday++;
            worker.SetStatus("colheita concluída");
            feedback.ShowMessage("+" + gained + " " + settings.displayName + " guardado(s)");
        });
    }

    public bool ConfirmFoodPreparation(int fish, int corn, int pumpkin, int jerimum)
    {
        if (pendingFoodWorker == null ||
            (pendingFoodWorker.UsedToday &&
             (pendingFoodCard == null || pendingFoodCard.Kind != ActivityKind.Replay)) ||
            inventoryTrading == null ||
            resources == null || foodPreparationDestination == null) return false;
        if (pendingFoodPlus && fish + corn + pumpkin + jerimum < 2)
        {
            feedback.ShowMessage("Comida+ exige pelo menos dois ingredientes.");
            return false;
        }
        if (!inventoryTrading.TryPrepare(fish, corn, pumpkin, jerimum, pendingFoodPlus, out int preparedFood))
        {
            feedback.ShowMessage("Selecione itens disponíveis para preparar comida.");
            return false;
        }
        WorkerAssignment worker = pendingFoodWorker;
        ActivityCardDrag card = pendingFoodCard;
        bool plus = pendingFoodPlus;
        pendingFoodWorker = null;
        pendingFoodCard = null;
        pendingFoodPlus = false;
        CommitAction(worker, plus ? ActivityKind.FoodPlus : ActivityKind.PrepareFood, "preparando comida", card);
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
        pendingFoodCard = null;
        pendingFoodPlus = false;
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
        pendingPlotCard = null;
        pendingFoodCard = null;
        pendingFoodWorker = null;
        return worker;
    }

    public void ReturnWorkersHome()
    {
        pendingPlotWorker = null;
        foreach (WorkerAssignment worker in workers)
        {
            if (worker == null || DOTween.IsTweening(worker.transform) ||
                Vector3.Distance(worker.transform.position, worker.HomePosition) < 0.01f)
                continue;
            if (fencePassage != null && villageExit != null &&
                worker.transform.position.x < fencePassage.position.x)
                MoveAlongRoute(worker, new[] { fencePassage.position, villageExit.position, worker.HomePosition }, null, false);
            else MoveWorker(worker, worker.HomePosition, null, false);
        }
    }

    public void BeginNextDay()
    {
        pendingPlotWorker = null;
        pendingPlotCard = null;
        pendingFoodWorker = null;
        pendingFoodCard = null;
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

    private void MoveToGatheringArea(GatheringTask task)
    {
        WorkerAssignment worker = task.Worker;
        worker.SetStatus("caminhando");
        MoveAlongRoute(worker, new[] { villageExit.position, fencePassage.position,
            task.TargetPosition }, () =>
        {
            task.Arrived = true;
            task.LastPosition = worker.transform.position;
            task.StillTime = 0f;
            worker.SetStatus("aguardando jogador");
            UpdateVisitButton(task);
        });
    }

    private void VisitForestWorker() => VisitGatheringWorker(WaitingTask(forestTask, forestTaskSecond));
    private void VisitFishingWorker() => VisitGatheringWorker(WaitingTask(fishingTask, fishingTaskSecond));

    private static GatheringTask WaitingTask(GatheringTask first, GatheringTask second)
    {
        if (first != null && first.Worker != null && first.Arrived && !first.Started) return first;
        if (second != null && second.Worker != null && second.Arrived && !second.Started) return second;
        return first != null && first.Worker != null && !first.Started ? first :
            second != null && second.Worker != null && !second.Started ? second : null;
    }

    private void VisitGatheringWorker(GatheringTask task)
    {
        if (task?.Worker != null && activeMinigame == null)
            communityCamera.Follow(task.Worker.transform);
    }

    private void UpdateGathering(GatheringTask task)
    {
        if (task?.Worker == null || task.Started) return;
        if (task.Arrived)
        {
            Vector3 position = task.Worker.transform.position;
            bool stopped = !DOTween.IsTweening(task.Worker.transform) &&
                Vector3.Distance(position, task.TargetPosition) < 0.03f &&
                (position - task.LastPosition).sqrMagnitude < 0.000001f;
            task.StillTime = stopped ? task.StillTime + Time.deltaTime : 0f;
            task.LastPosition = position;
        }
        UpdateVisitButton(task);
        if (task.Arrived && task.StillTime >= arrivalStillTime && activeMinigame == null &&
            communityCamera.IsFollowing(task.Worker.transform) && communityCamera.IsFollowReady)
            BeginGatheringGame(task);
    }

    private void UpdateVisitButton(GatheringTask task)
    {
        if (task?.Button == null) return;
        if (task == forestTaskSecond || task == fishingTaskSecond) return;
        GatheringTask waiting = WaitingTask(task,
            task == forestTask ? forestTaskSecond : fishingTaskSecond);
        bool visible = waiting != null && activeMinigame == null &&
            !communityCamera.IsFollowing(waiting.Worker.transform);
        if (task.Button.gameObject.activeSelf != visible)
            task.Button.gameObject.SetActive(visible);
        if (visible && task.Label != null)
        {
            string area = task.Activity == ActivityKind.CollectWood ? "FLORESTA" : "PESCA";
            string action = waiting.Arrived && waiting.StillTime >= arrivalStillTime ? "INICIAR" : "ACOMPANHAR";
            string label = area + " - " + action + "\n" + waiting.Worker.DisplayName;
            if (task.Label.text != label) task.Label.text = label;
        }
    }

    private void BeginGatheringGame(GatheringTask task)
    {
        WorkerAssignment worker = task.Worker;
        ActivityKind activity = task.Activity;
        task.Started = true;
        activeMinigame = task;
        communityCamera.SetFollowLocked(true);
        UpdateVisitButton(forestTask);
        UpdateVisitButton(fishingTask);
        busyWorkers++;
        worker.SetStatus("atividade interativa");
        gatheringGame.Begin(task.SelectedActivity, rating =>
        {
            busyWorkers--;
            task.Worker = null;
            task.Arrived = false;
            task.Started = false;
            activeMinigame = null;
            communityCamera.SetFollowLocked(false);
            communityCamera.ReturnToCommunity();
            CompleteGathering(worker, task.SelectedActivity, rating);
            SendGatheringWorkerHome(worker);
            UpdateVisitButton(forestTask);
            UpdateVisitButton(fishingTask);
        }, (activity == ActivityKind.Fish || activity == ActivityKind.FishPlus) &&
            dayEvents != null ? dayEvents.FishingReduction : 0);
    }

    private void SendGatheringWorkerHome(WorkerAssignment worker)
    {
        int returnDay = dayCycle.CurrentDay;
        worker.SetStatus("voltando para casa");
        MoveAlongRoute(worker, new[] { fencePassage.position, villageExit.position,
            worker.HomePosition }, () =>
        {
            if (dayCycle.CurrentDay == returnDay) worker.SetStatus("em casa");
        }, false);
    }

    private void MoveAlongRoute(WorkerAssignment worker, Vector3[] points, System.Action onArrived, bool countAsBusy = true)
    {
        worker.transform.DOKill();
        float distance = 0f;
        Vector3 from = worker.transform.position;
        foreach (Vector3 point in points)
        {
            distance += Vector3.Distance(from, point);
            from = point;
        }
        if (countAsBusy) busyWorkers++;
        Sequence route = DOTween.Sequence();
        from = worker.transform.position;
        foreach (Vector3 point in points)
        {
            float duration = outsideMovementDuration * worker.MovementDurationMultiplier *
                Vector3.Distance(from, point) / Mathf.Max(distance, 0.01f);
            route.Append(worker.transform.DOMove(point, Mathf.Max(duration, 0.01f)).SetEase(Ease.Linear));
            from = point;
        }
        route.OnComplete(() =>
        {
            if (countAsBusy) busyWorkers--;
            onArrived?.Invoke();
        });
    }
}
