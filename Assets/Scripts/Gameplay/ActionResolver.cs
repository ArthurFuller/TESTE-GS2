using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActionResolver : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
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
    [SerializeField, Min(0.01f)] private float foodPreparationDuration = 5f;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private Transform forestDestination;
    [SerializeField] private ForestTreeFall forestTrees;
    [SerializeField] private Transform fishingDestination;
    [SerializeField, Range(1, 6)] private int forestPositions = 4;
    [SerializeField, Min(0.1f)] private float forestSpacing = 0.55f;
    [SerializeField] private Vector3 forestLineOffset = Vector3.zero;
    [SerializeField, Range(1, 6)] private int fishingPositions = 4;
    [SerializeField, Min(0.1f)] private float fishingSpacing = 0.55f;
    [SerializeField] private Vector3 fishingShoreOffset = new Vector3(0.25f, 0f, 0f);
    [SerializeField] private Transform villageExit;
    [SerializeField] private Transform fencePassage;
    [SerializeField, Min(0)] private int woodReward = 2;
    [SerializeField, Min(1)] private int gambleStake = 10;
    [SerializeField, Min(0.1f)] private float coinTossDuration = 0.65f;
    [SerializeField, Min(0.1f)] private float movementSpeed = 4.2f;
    [SerializeField, Min(0.01f)] private float plowWorkDuration = 5f;
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
    private int replayAnimations;
    private WorkerAssignment pendingFoodWorker;
    private ActivityCardDrag pendingFoodCard;
    private bool pendingFoodPlus;
    private GatheringTask[] forestTasks;
    private GatheringTask[] fishingTasks;
    private GatheringTask activeMinigame;
    private readonly System.Collections.Generic.Dictionary<WorkerAssignment, Tween> movements = new();
    private readonly System.Collections.Generic.HashSet<WorkerAssignment> busyMovements = new();
    private readonly System.Collections.Generic.HashSet<WorkerAssignment> returningHome = new();
    private readonly System.Collections.Generic.HashSet<WorkerAssignment> working = new();
    [SerializeField] private Button cancelSelectionButton;

    public bool IsBusy => busyWorkers > 0 || replayAnimations > 0 || pendingFoodWorker != null ||
        pendingPlotWorker != null ||
        HasGatheringWorker(forestTasks) || HasGatheringWorker(fishingTasks);
    public bool HasPendingSelection => pendingPlotWorker != null;
    public event System.Action<bool> PendingSelectionChanged;
    public bool GatheringNeedsPlayer => NeedsGatheringPlayer(forestTasks) ||
        NeedsGatheringPlayer(fishingTasks);
    public int WoodGainedToday { get; private set; }
    public int FoodGainedToday { get; private set; }
    public int FishGainedToday { get; private set; }
    public int CropsStoredToday { get; private set; }
    public int PlotsPlowedToday { get; private set; }
    public int CropsHarvestedToday { get; private set; }
    public event System.Action<ActivityCardDrag> CardUsed;
    public event System.Action<int, ActivityCardDrag> CardPlayed;
    public int AssignmentVersion { get; private set; }

    private void Awake()
    {
        if (cancelSelectionButton == null)
            foreach (Button candidate in FindObjectsByType<Button>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (candidate.name == "Cancelar escolha de terreno")
                {
                    cancelSelectionButton = candidate;
                    break;
                }
        if (cancelSelectionButton == null) return;
        cancelSelectionButton.onClick.AddListener(CancelPendingSelection);
        cancelSelectionButton.gameObject.SetActive(false);
    }

    private void Start()
    {
        forestTasks = CreateGatheringPositions(ActivityKind.CollectWood, forestDestination,
            forestVisitButton, forestVisitLabel, forestPositions, forestSpacing, forestLineOffset);
        fishingTasks = CreateGatheringPositions(ActivityKind.Fish, fishingDestination,
            fishingVisitButton, fishingVisitLabel, fishingPositions, fishingSpacing, fishingShoreOffset);
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
        if (cancelSelectionButton != null)
            cancelSelectionButton.onClick.RemoveListener(CancelPendingSelection);
    }

    private void Update()
    {
        foreach (GatheringTask task in forestTasks) UpdateGathering(task);
        foreach (GatheringTask task in fishingTasks) UpdateGathering(task);
    }

    private static GatheringTask[] CreateGatheringPositions(ActivityKind activity, Transform destination,
        Button button, Text label, int count, float spacing, Vector3 lineOffset)
    {
        var positions = new GatheringTask[Mathf.Clamp(count, 1, 6)];
        for (int i = 0; i < positions.Length; i++)
            positions[i] = new GatheringTask(activity, destination, button, label,
                lineOffset + Vector3.forward * ((i - (positions.Length - 1) * 0.5f) * spacing));
        return positions;
    }

    private static bool HasGatheringWorker(GatheringTask[] tasks)
    {
        if (tasks == null) return false;
        foreach (GatheringTask task in tasks)
            if (task.Worker != null) return true;
        return false;
    }

    private static bool NeedsGatheringPlayer(GatheringTask[] tasks)
    {
        if (tasks == null) return false;
        foreach (GatheringTask task in tasks)
            if (task.Worker != null && task.Arrived && !task.Started) return true;
        return false;
    }

    private static bool HasAssignedWorker(GatheringTask[] tasks, WorkerAssignment worker)
    {
        if (tasks == null) return false;
        foreach (GatheringTask task in tasks)
            if (task.Worker == worker) return true;
        return false;
    }

    public void TryAssign(WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card = null)
    {
        TryAssignInternal(worker, activity, activityName, card, false);
    }

    private void TryAssignInternal(WorkerAssignment worker, ActivityKind activity,
        string activityName, ActivityCardDrag card, bool replayAnimationComplete)
    {
        if (dayCycle == null || !dayCycle.CanAcceptActions)
            return;
        if (replayAnimations > 0 && !replayAnimationComplete) return;
        if (pendingFoodWorker != null)
        {
            feedback.ShowMessage("Confirme ou cancele o preparo de comida primeiro.");
            return;
        }
        if (pendingPlotWorker != null)
        {
            feedback.ShowMessage("Escolha um terreno ou cancele a carta ativa primeiro.");
            return;
        }
        if (activity == ActivityKind.BuySeeds)
        {
            if (merchant != null) merchant.ToggleShop();
            return;
        }
        if (worker.IsUnavailableToday)
        {
            feedback.ShowMessage("Esse trabalhador está ausente hoje.");
            return;
        }
        if (working.Contains(worker) || busyMovements.Contains(worker) ||
            HasAssignedWorker(forestTasks, worker) || HasAssignedWorker(fishingTasks, worker))
        {
            feedback.ShowMessage("Aguarde esse trabalhador concluir a ação atual.");
            return;
        }
        bool replaying = activity == ActivityKind.Replay;
        if (replaying)
        {
            ActivityKind? repeated = worker.LastActivity;
            if (repeated == null || !CanReplay(repeated.Value))
            {
                feedback.ShowMessage("Replay exige uma ação anterior desse trabalhador.");
                card?.PlayRejectedReplay();
                return;
            }
            if (!replayAnimationComplete && card != null)
            {
                ActivityCardAvailability hand = card.transform.parent
                    .GetComponentInChildren<ActivityCardAvailability>(true);
                bool disposable = repeated == ActivityKind.BuildFence ||
                    repeated == ActivityKind.BuildHouse;
                if (disposable && (hand == null || !hand.HasDisposable(repeated.Value)))
                {
                    feedback.ShowMessage("Replay precisa da carta descartável na mão.");
                    card.PlayRejectedReplay();
                    return;
                }
                AssignmentVersion++;
                replayAnimations++;
                ActivityKind revealed = repeated.Value;
                card.PlayReplay(worker, revealed, () =>
                {
                    replayAnimations--;
                    TryAssignInternal(worker, ActivityKind.Replay, activityName, card, true);
                });
                if (disposable) hand.ConsumeReplayDisposable(revealed);
                return;
            }
            if (repeated == ActivityKind.Coffee)
            {
                worker.ReplaceCoffeeWithReplay();
                worker.Select();
                worker.SetStatus("duas ações disponíveis");
                RecordCard(ActivityKind.Replay, worker, card);
                feedback.ShowMessage("Replay interrompeu o Cafézinho: duas ações disponíveis.");
                return;
            }
            activity = repeated.Value;
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
            if (resources == null || resources.Coins < gambleStake)
            {
                feedback.ShowMessage("Faltam moedas para a aposta.");
                return;
            }
            CommitAction(worker, activity, activityName, card);
            worker.SetStatus("lançando moeda");
            feedback.ShowMessage("Moeda no ar...");
            busyWorkers++;
            Sequence toss = DOTween.Sequence();
            toss.AppendInterval(coinTossDuration);
            toss.OnComplete(() =>
            {
                busyWorkers--;
                bool won = Random.value < 0.5f;
                if (won)
                {
                    resources.AddCoins(gambleStake);
                    if (resourceHud != null) resourceHud.FlyFromWorld(ResourceKind.Coins,
                        gambleStake, worker.transform.position + Vector3.up);
                }
                else resources.TrySpendCoins(gambleStake);
                worker.SetStatus(won ? "aposta vencida" : "aposta perdida");
                feedback.ShowMessage(won ? "Jogo do Bicho: +" + gambleStake + " moedas líquidas." :
                    "Jogo do Bicho: -" + gambleStake + " moedas.");
                SendWorkerHome(worker);
            });
            return;
        }
        if (activity == ActivityKind.Rest)
        {
            CommitAction(worker, activity, activityName, card);
            worker.ScheduleExtraAction();
            feedback.ShowMessage("Descanso: ação extra amanhã. Alimentação normal hoje.");
            SendWorkerHome(worker);
            return;
        }
        bool woodActivity = activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus;
        bool fishActivity = activity == ActivityKind.Fish || activity == ActivityKind.FishPlus;
        GatheringTask task = woodActivity ? FreeTask(forestTasks) :
            fishActivity ? FreeTask(fishingTasks) : null;
        if ((woodActivity || fishActivity) && task == null)
        {
            feedback.ShowMessage("Todos os postos dessa área já estão ocupados.");
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
            activity == ActivityKind.BuildFence || activity == ActivityKind.BuildHouse ||
            activity == ActivityKind.PlaceCow || activity == ActivityKind.PlaceChicken ||
            activity == ActivityKind.FeedAnimals)
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

    private static GatheringTask FreeTask(GatheringTask[] tasks)
    {
        if (tasks == null) return null;
        foreach (GatheringTask task in tasks)
            if (task.Worker == null) return task;
        return null;
    }

    private void RecordCard(ActivityKind activity, WorkerAssignment worker, ActivityCardDrag card)
    {
        AssignmentVersion++;
        if (card != null && dayCycle != null)
            CardPlayed?.Invoke(dayCycle.CurrentDay, card);
        card?.PrepareDelivery(worker);
        CardUsed?.Invoke(card);
    }

    private static bool CanReplay(ActivityKind activity) =>
        activity != ActivityKind.Replay && activity != ActivityKind.BuySeeds;

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
        if (foodPreparationPanel == null || inventoryTrading == null)
        {
            feedback.ShowMessage("Preparação de comida não configurada.");
            return;
        }
        ClearPendingPlot();
        pendingFoodWorker = worker;
        pendingFoodCard = card;
        AssignmentVersion++;
        pendingFoodPlus = activity == ActivityKind.FoodPlus;
        worker.Select();
        worker.SetStatus("escolha os ingredientes");
        foodPreparationPanel.Show(pendingFoodPlus);
    }

    private void ClearPendingPlot()
    {
        bool hadSelection = pendingPlotWorker != null;
        Plot.ClearHighlights();
        HouseSite.ClearHighlights();
        pendingPlotCard?.ReturnFromSelection();
        if (pendingPlotWorker != null && !pendingPlotWorker.UsedToday)
            pendingPlotWorker.SetStatus("livre");
        pendingPlotWorker = null;
        pendingPlotCard = null;
        if (cancelSelectionButton != null) cancelSelectionButton.gameObject.SetActive(false);
        if (hadSelection) PendingSelectionChanged?.Invoke(false);
    }

    public void CancelPendingSelection()
    {
        if (pendingPlotWorker == null) return;
        ClearPendingPlot();
        feedback.ShowMessage("Escolha cancelada. A carta voltou para a mão.");
    }

    private void SelectPlot(WorkerAssignment worker, ActivityKind activity, string activityName,
        ActivityCardDrag card)
    {
        bool available = activity == ActivityKind.BuildHouse ? HouseSite.HasAvailable() :
            Plot.HasAvailable(activity, dayCycle.CurrentDay, seedCatalog);
        if (!available)
        {
            feedback.ShowMessage("Não há alvo disponível para essa carta.");
            card?.PlayRejectedReplay();
            return;
        }
        AssignmentVersion++;
        pendingPlotWorker = worker;
        pendingPlotCard = card;
        pendingPlotAction = activity;
        card?.FlyToSelection();
        if (cancelSelectionButton != null)
        {
            cancelSelectionButton.gameObject.SetActive(true);
            cancelSelectionButton.transform.SetAsLastSibling();
        }
        PendingSelectionChanged?.Invoke(true);
        Plot.ShowAvailable(activity, dayCycle.CurrentDay, seedCatalog);
        if (activity == ActivityKind.BuildHouse) HouseSite.ShowAvailable();
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

    private void CompleteGathering(WorkerAssignment worker, ActivityKind activity, int rating,
        System.Action onResourcesCollected, ResourceHud.DropBatch drops = null)
    {
        if (rating < 0)
        {
            worker.SetStatus("ação cancelada");
            feedback.ShowMessage("Minigame de coleta indisponível.");
            onResourcesCollected?.Invoke();
            return;
        }
        worker.SetStatus("ação concluída");
        if (activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus)
        {
            int gained = woodReward + rating + (activity == ActivityKind.WoodPlus ? 1 : 0);
            gained = Mathf.Max(0, gained - worker.ProductionReduction);
            resources.AddWood(gained);
            if (resourceHud != null && drops != null && drops.Icons.Count > 0)
                resourceHud.FlyDrops(drops, onResourcesCollected);
            else if (resourceHud != null) resourceHud.FlyFromWorld(ResourceKind.Wood,
                gained, worker.transform.position + Vector3.up, 0f, onResourcesCollected);
            else onResourcesCollected?.Invoke();
            if (dayEvents != null) dayEvents.RecordWood(gained);
            WoodGainedToday += gained;
            feedback.ShowMessage("+" + gained + " madeira");
        }
        else
        {
            int gained = activity == ActivityKind.FishPlus ?
                2 + (rating > 0 ? 2 : 0) : 1 + (rating > 0 ? 1 : 0);
            resources.AddFish(gained);
            if (resourceHud != null && drops != null && drops.Icons.Count > 0)
                resourceHud.FlyDrops(drops, onResourcesCollected);
            else if (resourceHud != null) resourceHud.FlyFromWorld(ResourceKind.Fish,
                gained, worker.transform.position + Vector3.up, 0f, onResourcesCollected);
            else onResourcesCollected?.Invoke();
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
            case ActivityKind.Fertilizer: ApplyCropImprovement(plot); break;
            case ActivityKind.BuildFence: FencePlot(plot); break;
            case ActivityKind.PlaceCow: PlaceAnimal(plot, AnimalKind.Cow); break;
            case ActivityKind.PlaceChicken: PlaceAnimal(plot, AnimalKind.Chicken); break;
            case ActivityKind.FeedAnimals: FeedAnimals(plot); break;
        }
    }

    private void FeedAnimals(Plot plot)
    {
        if (plot.Animal == AnimalKind.None)
        {
            feedback.ShowMessage("Escolha um cercado com animal.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.FeedAnimals, "distribuindo ração", card);
        MoveAndWork(worker, plot.WorkPosition, plowWorkDuration, () =>
        {
            if (dayCycle.TryRepeatAnimalProduction(out int milk, out int eggs))
                feedback.ShowMessage("Ração: +" + milk + " leite e +" + eggs + " ovos.");
            SendWorkerHome(worker);
        });
    }

    private void PlaceAnimal(Plot plot, AnimalKind animal)
    {
        if (!plot.IsFenced || !plot.IsEmpty || plot.Animal != AnimalKind.None)
        {
            feedback.ShowMessage("Escolha um cercado vazio para o animal.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, animal == AnimalKind.Cow ? ActivityKind.PlaceCow :
            ActivityKind.PlaceChicken, "levando animal ao cercado", card);
        MoveAndWork(worker, plot.WorkPosition, plowWorkDuration, () =>
        {
            plot.PlaceAnimal(animal);
            feedback.ShowMessage(animal == AnimalKind.Cow ? "Vaca no cercado." : "Galinha no cercado.");
            SendWorkerHome(worker);
        });
    }

    private void FencePlot(Plot plot)
    {
        if (!plot.CanUse(ActivityKind.BuildFence, dayCycle.CurrentDay, seedCatalog))
        {
            feedback.ShowMessage("A cerca exige um terreno vazio e não arado.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.BuildFence, "construindo cerca", card);
        MoveAndWork(worker, plot.WorkPosition, plowWorkDuration, () =>
        {
            plot.BuildFence();
            worker.SetStatus("cerca construída");
            feedback.ShowMessage("Terreno cercado.");
            SendWorkerHome(worker);
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
            if (site.Build(dayCycle.CurrentDay))
                feedback.ShowMessage("Casa construída. O novo trabalhador chegará amanhã.");
            SendWorkerHome(builder);
        });
    }

    public void RegisterWorker(WorkerAssignment worker)
    {
        if (worker == null || System.Array.IndexOf(workers, worker) >= 0) return;
        var updated = new System.Collections.Generic.List<WorkerAssignment>(workers) { worker };
        workers = updated.ToArray();
    }

    private void ApplyCropImprovement(Plot plot)
    {
        ActivityKind activity = pendingPlotAction;
        if (!plot.CanUse(activity, dayCycle.CurrentDay, seedCatalog))
        {
            feedback.ShowMessage(activity == ActivityKind.Fertilizer ?
                "Escolha uma plantação que ainda não recebeu adubo." :
                "Escolha uma plantação ainda em crescimento.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        var targets = plot.ConnectedPlotsFor(activity, dayCycle.CurrentDay, seedCatalog);
        CommitAction(worker, activity, activity.ToString(), card);
        ImproveNext(0);

        void ImproveNext(int index)
        {
            if (index >= targets.Count)
            {
                worker.SetStatus(activity == ActivityKind.Fertilizer ? "plantação adubada" :
                    "crescimento acelerado");
                feedback.ShowMessage(activity == ActivityKind.Fertilizer ?
                    "Adubo: produção dobrada na colheita." : "Plantação avançou um dia.");
                SendWorkerHome(worker);
                return;
            }
            Plot target = targets[index];
            MoveAndWork(worker, target.WorkPosition, plowWorkDuration, () =>
            {
                if (activity == ActivityKind.Fertilizer) target.Fertilize();
                else
                {
                    target.AdvanceGrowth(1);
                    target.RefreshGrowth(dayCycle.CurrentDay, seedCatalog);
                }
                ImproveNext(index + 1);
            });
        }
    }

    private void PlowPlot(Plot plot)
    {
        if (plot.IsPlowed || plot.IsFenced || plot.Animal != AnimalKind.None)
        {
            feedback.ShowMessage("Escolha um terreno livre para arar.");
            return;
        }
        ActivityCardDrag card = pendingPlotCard;
        WorkerAssignment worker = TakePendingWorker();
        CommitAction(worker, ActivityKind.Plow, "arando", card);
        MoveAndWork(worker, plot.WorkPosition, plowWorkDuration, () =>
        {
            plot.Plow();
            PlotsPlowedToday++;
            worker.SetStatus("terreno arado");
            feedback.ShowMessage("Terreno preparado para plantio.");
            SendWorkerHome(worker);
        });
    }

    private void PlantPlot(Plot plot, CropType crop)
    {
        if (!plot.IsPlowed || plot.IsFenced || !plot.IsEmpty || plot.Animal != AnimalKind.None)
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
        MoveAndWork(worker, plot.WorkPosition, plowWorkDuration, () =>
        {
            plot.Plant(crop, dayCycle.CurrentDay, seedCatalog);
            worker.SetStatus("plantio concluído");
            feedback.ShowMessage(seedCatalog.Get(crop).displayName + " plantado.");
            SendWorkerHome(worker);
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
        var targets = plot.ReadyConnectedPlots(dayCycle.CurrentDay, seedCatalog);
        CommitAction(worker, ActivityKind.Harvest, "colhendo", card);
        HarvestNext(0);

        void HarvestNext(int index)
        {
            if (index >= targets.Count)
            {
                worker.SetStatus("colheita concluída");
                SendWorkerHome(worker);
                return;
            }
            Plot target = targets[index];
            if (target == null || !target.IsReady(dayCycle.CurrentDay, seedCatalog))
            {
                HarvestNext(index + 1);
                return;
            }
            MoveAndWork(worker, target.WorkPosition, plowWorkDuration, () =>
            {
                int harvestYield = target.HarvestYield(dayCycle.CurrentDay, seedCatalog);
                CropType crop = target.Harvest();
                CropSettings settings = seedCatalog.Get(crop);
                int gained = Mathf.Max(0, harvestYield - worker.ProductionReduction -
                    (dayEvents != null ? dayEvents.HarvestReduction : 0));
                resources.AddCrop(crop, gained);
                if (resourceHud != null)
                {
                    ResourceKind kind = crop == CropType.Corn ? ResourceKind.Corn :
                        crop == CropType.Pumpkin ? ResourceKind.Pumpkin : ResourceKind.Jerimum;
                    resourceHud.FlyFromWorld(kind, gained, target.transform.position + Vector3.up,
                        0.8f);
                }
                CropsStoredToday += gained;
                CropsHarvestedToday++;
                feedback.ShowMessage("+" + gained + " " + settings.displayName + " guardado(s)");
                HarvestNext(index + 1);
            });
        }
    }

    public bool ConfirmFoodPreparation(int fish, int corn, int pumpkin, int jerimum, int milk, int eggs)
    {
        if (pendingFoodWorker == null ||
            (pendingFoodWorker.UsedToday &&
             (pendingFoodCard == null || pendingFoodCard.Kind != ActivityKind.Replay)) ||
            inventoryTrading == null ||
            resources == null) return false;
        if (!inventoryTrading.TryPrepare(fish, corn, pumpkin, jerimum, milk, eggs,
            pendingFoodPlus, out int preparedFood))
        {
            feedback.ShowMessage("Selecione uma receita válida com ingredientes disponíveis.");
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
            if (resourceHud != null) resourceHud.FlyFromWorld(ResourceKind.Food,
                preparedFood, worker.transform.position + Vector3.up);
            FoodGainedToday += preparedFood;
            worker.SetStatus("comida preparada");
            feedback.ShowMessage("+" + preparedFood + " comida preparada");
            SendWorkerHome(worker);
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

    private WorkerAssignment TakePendingWorker()
    {
        WorkerAssignment worker = pendingPlotWorker;
        pendingPlotWorker = null;
        pendingPlotCard = null;
        if (cancelSelectionButton != null) cancelSelectionButton.gameObject.SetActive(false);
        PendingSelectionChanged?.Invoke(false);
        Plot.ClearHighlights();
        HouseSite.ClearHighlights();
        pendingFoodCard = null;
        pendingFoodWorker = null;
        return worker;
    }

    public void ReturnWorkersHome()
    {
        ClearPendingPlot();
        foreach (WorkerAssignment worker in workers)
        {
            if (worker == null || movements.ContainsKey(worker) ||
                Vector3.Distance(worker.transform.position, worker.HomePosition) < 0.01f)
                continue;
            SendWorkerHome(worker);
        }
    }

    public void BeginNextDay()
    {
        if (forestTrees != null) forestTrees.ResetTrees();
        ClearPendingPlot();
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

    private void StopMovement(WorkerAssignment worker)
    {
        if (movements.TryGetValue(worker, out Tween previous))
        {
            previous.Kill();
            movements.Remove(worker);
        }
        if (busyMovements.Remove(worker)) busyWorkers--;
        returningHome.Remove(worker);
    }

    private void MoveWorker(WorkerAssignment worker, Vector3 destination, System.Action onArrived,
        bool countAsBusy = true, bool isReturn = false)
    {
        bool braking = returningHome.Contains(worker) && !isReturn;
        StopMovement(worker);
        if (countAsBusy)
        {
            busyWorkers++;
            busyMovements.Add(worker);
        }
        if (isReturn) returningHome.Add(worker);
        float duration = Mathf.Max(0.01f, Vector3.Distance(worker.transform.position, destination) / movementSpeed);
        Tween travel = worker.transform.DOMove(destination, duration).SetEase(Ease.Linear);
        if (braking) travel = DOTween.Sequence().AppendInterval(0.1f).Append(travel);
        movements[worker] = travel;
        travel.OnComplete(() =>
        {
            movements.Remove(worker);
            if (busyMovements.Remove(worker)) busyWorkers--;
            returningHome.Remove(worker);
            onArrived?.Invoke();
        });
    }

    public void BringWorkerOutOfHouse(WorkerAssignment worker, Vector3 doorwayPosition)
    {
        if (worker == null) return;
        worker.transform.position = doorwayPosition;
        worker.SetStatus("saindo de casa");
        Vector3 outsideDoor = doorwayPosition + Vector3.back * 0.6f;
        Vector3 besideHouse = new Vector3(worker.HomePosition.x, outsideDoor.y, outsideDoor.z);
        MoveAlongRoute(worker, new[] { outsideDoor, besideHouse, worker.HomePosition },
            () => worker.SetStatus("em casa"));
        returningHome.Add(worker);
    }

    private void SendWorkerHome(WorkerAssignment worker)
    {
        if (worker == null) return;
        if (Vector3.Distance(worker.transform.position, worker.HomePosition) < 0.05f)
        {
            worker.SetStatus("em casa");
            return;
        }
        worker.SetStatus("voltando para casa");
        MoveViaVillagePath(worker, worker.HomePosition, () => worker.SetStatus("em casa"), false);
    }

    private void MoveAndWork(WorkerAssignment worker, Vector3 destination, float workDuration, System.Action onFinished)
    {
        if (Vector3.Distance(worker.transform.position, destination) <= 0.15f)
        {
            StopMovement(worker);
            WorkAtStation(worker, workDuration, onFinished);
            return;
        }
        worker.SetStatus("caminhando");
        MoveViaVillagePath(worker, destination, () => WorkAtStation(worker, workDuration, onFinished));
    }

    private void MoveViaVillagePath(WorkerAssignment worker, Vector3 destination,
        System.Action onArrived, bool countAsBusy = true)
    {
        if (villageExit == null)
        {
            MoveWorker(worker, destination, onArrived, countAsBusy, !countAsBusy);
            return;
        }
        var route = new System.Collections.Generic.List<Vector3>();
        Vector3 from = worker.transform.position;
        if (fencePassage != null && from.x < fencePassage.position.x - 0.1f &&
            destination.x > fencePassage.position.x)
        {
            route.Add(fencePassage.position);
            from = villageExit.position;
            route.Add(from);
        }
        float pathZ = villageExit.position.z;
        Vector3 onPath = new Vector3(from.x, from.y, pathZ);
        if (Vector3.Distance(from, onPath) > 0.05f) route.Add(onPath);
        Vector3 atDestination = new Vector3(destination.x, destination.y, pathZ);
        if (Vector3.Distance(onPath, atDestination) > 0.05f) route.Add(atDestination);
        route.Add(destination);
        MoveAlongRoute(worker, route.ToArray(), onArrived, countAsBusy);
    }

    private void WorkAtStation(WorkerAssignment worker, float workDuration, System.Action onFinished)
    {
        busyWorkers++;
        working.Add(worker);
        worker.SetStatus("trabalhando");
        if (worker.Progress != null) worker.Progress.Show();
        float progress = 0;
        DOTween.To(() => progress, value =>
        {
            progress = value;
            if (worker.Progress != null) worker.Progress.SetProgress(value);
        }, 1f, workDuration).SetEase(Ease.Linear).OnComplete(() =>
        {
            if (worker.Progress != null) worker.Progress.Hide();
            busyWorkers--;
            working.Remove(worker);
            onFinished?.Invoke();
        });
    }

    private void MoveToGatheringArea(GatheringTask task)
    {
        WorkerAssignment worker = task.Worker;
        worker.SetStatus("caminhando");
        Vector3 homePath = new Vector3(worker.transform.position.x,
            worker.transform.position.y, villageExit.position.z);
        MoveAlongRoute(worker, new[] { homePath, villageExit.position, fencePassage.position,
            task.TargetPosition }, () =>
        {
            task.Arrived = true;
            task.LastPosition = worker.transform.position;
            task.StillTime = 0f;
            worker.SetStatus("aguardando jogador");
            UpdateVisitButton(task);
        });
    }

    private void VisitForestWorker() => VisitGatheringWorker(WaitingTask(forestTasks));
    private void VisitFishingWorker() => VisitGatheringWorker(WaitingTask(fishingTasks));

    private static GatheringTask WaitingTask(GatheringTask[] tasks)
    {
        if (tasks == null) return null;
        foreach (GatheringTask task in tasks)
            if (task.Worker != null && task.Arrived && !task.Started) return task;
        foreach (GatheringTask task in tasks)
            if (task.Worker != null && !task.Started) return task;
        return null;
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
            bool stopped = !movements.ContainsKey(task.Worker) &&
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
        GatheringTask[] areaTasks = task.Activity == ActivityKind.CollectWood ? forestTasks : fishingTasks;
        GatheringTask waiting = WaitingTask(areaTasks);
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
        bool wood = activity == ActivityKind.CollectWood || activity == ActivityKind.WoodPlus;
        ResourceHud.DropBatch fishDrops = !wood && resourceHud != null ?
            resourceHud.CreateDropBatch(ResourceKind.Fish) : null;
        task.Started = true;
        activeMinigame = task;
        communityCamera.SetFollowLocked(true);
        UpdateVisitButton(forestTasks[0]);
        UpdateVisitButton(fishingTasks[0]);
        busyWorkers++;
        worker.SetStatus("atividade interativa");
        gatheringGame.Begin(task.SelectedActivity, rating =>
        {
            task.Worker = null;
            task.Arrived = false;
            task.Started = false;
            communityCamera.HoldCurrentView();
            if (wood && rating >= 0)
            {
                ResolveWoodGathering(worker, activity, rating);
                return;
            }
            CompleteGathering(worker, activity, rating, () =>
            {
                busyWorkers--;
                activeMinigame = null;
                communityCamera.ReturnToCommunity();
                UpdateVisitButton(forestTasks[0]);
                UpdateVisitButton(fishingTasks[0]);
            }, fishDrops);
            SendGatheringWorkerHome(worker);
        }, (activity == ActivityKind.Fish || activity == ActivityKind.FishPlus) &&
            dayEvents != null ? dayEvents.FishingReduction : 0,
            communityCamera.ShakeWoodHit, communityCamera.SetFishingTension,
            () =>
            {
                if (fishDrops != null)
                    resourceHud.DropOne(fishDrops, worker.transform.position + Vector3.up);
            });
    }

    private void ResolveWoodGathering(WorkerAssignment worker, ActivityKind activity, int rating)
    {
        System.Action afterFall = () =>
        {
            int gained = Mathf.Max(0, woodReward + rating +
                (activity == ActivityKind.WoodPlus ? 1 : 0) - worker.ProductionReduction);
            ResourceHud.DropBatch drops = resourceHud != null ?
                resourceHud.CreateDropBatch(ResourceKind.Wood) : null;
            Vector3 origin = forestTrees != null ? forestTrees.LastImpactPosition :
                worker.transform.position + Vector3.up;
            Sequence dropSequence = DOTween.Sequence().SetTarget(this);
            for (int i = 0; i < gained; i++)
            {
                dropSequence.AppendCallback(() =>
                {
                    if (drops != null) resourceHud.DropOne(drops, origin);
                });
                dropSequence.AppendInterval(0.11f);
            }
            dropSequence.AppendInterval(0.28f);
            dropSequence.OnComplete(() =>
            {
                CompleteGathering(worker, activity, rating, () =>
                {
                    busyWorkers--;
                    activeMinigame = null;
                    UpdateVisitButton(forestTasks[0]);
                    UpdateVisitButton(fishingTasks[0]);
                }, drops);
                communityCamera.ReturnToCommunity();
                SendGatheringWorkerHome(worker);
            });
        };
        if (forestTrees != null) forestTrees.FallNear(worker.transform.position, afterFall);
        else afterFall();
    }

    private void SendGatheringWorkerHome(WorkerAssignment worker)
    {
        int returnDay = dayCycle.CurrentDay;
        worker.SetStatus("voltando para casa");
        Vector3 homePath = new Vector3(worker.HomePosition.x,
            worker.HomePosition.y, villageExit.position.z);
        MoveAlongRoute(worker, new[] { fencePassage.position, villageExit.position,
            homePath, worker.HomePosition }, () =>
        {
            if (dayCycle.CurrentDay == returnDay) worker.SetStatus("em casa");
        }, false);
    }

    private void MoveAlongRoute(WorkerAssignment worker, Vector3[] points, System.Action onArrived, bool countAsBusy = true)
    {
        bool braking = returningHome.Contains(worker) && countAsBusy;
        StopMovement(worker);
        if (countAsBusy)
        {
            busyWorkers++;
            busyMovements.Add(worker);
        }
        else returningHome.Add(worker);
        Sequence route = DOTween.Sequence();
        if (braking) route.AppendInterval(0.1f);
        Vector3 from = worker.transform.position;
        foreach (Vector3 point in points)
        {
            float duration = Mathf.Max(0.01f, Vector3.Distance(from, point) / movementSpeed);
            route.Append(worker.transform.DOMove(point, duration).SetEase(Ease.Linear));
            from = point;
        }
        movements[worker] = route;
        route.OnComplete(() =>
        {
            movements.Remove(worker);
            if (busyMovements.Remove(worker)) busyWorkers--;
            returningHome.Remove(worker);
            onArrived?.Invoke();
        });
    }
}
