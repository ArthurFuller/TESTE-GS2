using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class DayCycle : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ResourceHud resourceHud;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private HouseSite[] houseSites;
    [SerializeField] private Plot[] plots;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DayTimeline timeline;
    [SerializeField] private ActiveMissionsPanel activeMissionsPanel;
    [SerializeField] private DeliveryMissionBoard missionBoard;
    [SerializeField] private DayEventController dayEvents;
    [SerializeField] private ActivityCardAvailability hand;
    [SerializeField] private FeedingMinigame feedingGame;
    [SerializeField] private CardRewardChoice cardChoice;
    [SerializeField] private Text dayText;
    [SerializeField] private Button finishDayButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueButtonText;
    [SerializeField, Min(1)] private int totalDays = 12;
    [SerializeField, Min(1)] private int milkPerCow = 1;
    [SerializeField, Min(1)] private int eggsPerChicken = 1;

    private int currentDay = 1;
    private bool summaryOpen;
    private bool feedingOpen;
    private bool gameOver;
    private int todayMilk;
    private int todayEggs;
    private int lastSummarizedDay;
    private int totalWoodGained;
    private int totalFishGained;
    private int totalFoodGained;
    private int totalCropsStored;
    private int totalPlotsPlowed;
    private int totalCropsHarvested;

    public bool CanAcceptActions => !summaryOpen && !feedingOpen && !gameOver &&
        (dayEvents == null || !dayEvents.IsPanelOpen);
    public int CurrentDay => currentDay;

    public bool TryRepeatAnimalProduction(out int milk, out int eggs)
    {
        milk = todayMilk;
        eggs = todayEggs;
        if (milk + eggs == 0 || resources == null) return false;
        resources.AddAnimalProducts(milk, eggs);
        AnimateAnimalGains();
        return true;
    }

    private void ProduceAnimalResources()
    {
        todayMilk = 0;
        todayEggs = 0;
        foreach (Plot plot in plots)
        {
            if (plot == null) continue;
            if (plot.Animal == AnimalKind.Cow) todayMilk += milkPerCow +
                (MandalaSynergy.Instance != null ? MandalaSynergy.Instance.ExtraMilk(plot) : 0);
            if (plot.Animal == AnimalKind.Chicken) todayEggs += eggsPerChicken;
        }
    }

    private void AddDailyAnimalResources()
    {
        if (todayMilk + todayEggs == 0 || resources == null) return;
        resources.AddAnimalProducts(todayMilk, todayEggs);
        AnimateAnimalGains();
        if (feedback != null)
            feedback.ShowMessage("Animais: +" + todayMilk + " leite e +" + todayEggs + " ovos.");
    }

    private void AnimateAnimalGains()
    {
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.Animal == AnimalKind.None) continue;
            plot.PulseAnimal();
            if (resourceHud == null) continue;
            ResourceKind kind = plot.Animal == AnimalKind.Cow ? ResourceKind.Milk : ResourceKind.Eggs;
            int amount = plot.Animal == AnimalKind.Cow ? milkPerCow +
                (MandalaSynergy.Instance != null ? MandalaSynergy.Instance.ExtraMilk(plot) : 0)
                : eggsPerChicken;
            resourceHud.FlyFromWorld(kind, amount, plot.transform.position + Vector3.up, 0.8f);
        }
    }
    public event Action DayChanged;
    public event Action DayClosing;
    public event Action DayClosingCancelled;

    public void RegisterWorker(WorkerAssignment worker)
    {
        if (worker == null || Array.IndexOf(workers, worker) >= 0) return;
        var updated = new List<WorkerAssignment>(workers) { worker };
        workers = updated.ToArray();
    }

    private void Start()
    {
        UpdateDayLabel();
        UpdateMarket();
        if (dayEvents != null) dayEvents.StartDay(currentDay);
        if (actions != null) actions.BeginNextDay();
        OpenDayAfterEvent(false);
    }

    public void FinishDay()
    {
        if (finishDayButton != null &&
            finishDayButton.GetComponent<HoverPunchButton>()?.BlocksNewClicks == true) return;
        if (!CanAcceptActions || resources == null || actions == null)
            return;
        if (actions.IsBusy)
        {
            if (finishDayButton != null)
                finishDayButton.GetComponent<HoverPunchButton>()?.ShakeRejected();
            feedback.ShowMessage(actions.GatheringNeedsPlayer ?
                "Inicie a coleta pendente pelo botão do trabalhador." :
                actions.HasPendingSelection ? "Escolha um terreno ou cancele a carta ativa." :
                "Aguarde o fim das ações.");
            return;
        }

        if (timeline != null) timeline.ClosePanel();
        if (activeMissionsPanel != null) activeMissionsPanel.Close();
        if (merchant != null && merchant.IsOpen) merchant.HideShop();
        if (currentDay < totalDays && feedingGame == null)
        {
            feedback.ShowMessage("Minigame de alimentação não configurado.");
            return;
        }
        AudioManager.Play(SoundCue.DayFinish);
        DayClosing?.Invoke();
        if (currentDay >= totalDays)
        {
            feedingOpen = true;
            if (finishDayButton != null) finishDayButton.interactable = false;
            if (hand != null) hand.ExitForDay(() => ShowDaySummary(0, 0, 0, 0));
            else ShowDaySummary(0, 0, 0, 0);
            return;
        }
        feedingOpen = true;
        if (finishDayButton != null) finishDayButton.interactable = false;
        if (hand != null) hand.ExitForDay(() => feedingGame.Begin(workers, FinishFeeding,
            CancelFeeding));
        else feedingGame.Begin(workers, FinishFeeding, CancelFeeding);
    }

    private void CancelFeeding()
    {
        feedingOpen = false;
        if (finishDayButton != null) finishDayButton.interactable = true;
        if (hand != null) hand.ReturnAfterCancelledDay();
        DayClosingCancelled?.Invoke();
    }

    private void FinishFeeding(SaleItem?[] chosenFood)
    {
        int prepared = 0;
        int raw = 0;
        int unfed = 0;
        int absent = 0;

        for (int i = 0; i < workers.Length; i++)
        {
            WorkerAssignment worker = workers[i];
            if (worker == null) continue;
            if (worker.IsUnavailableToday)
            {
                absent++;
                continue;
            }

            SaleItem? received = chosenFood[i];
            if (received == SaleItem.Food)
            {
                prepared++;
                worker.ScheduleExtraAction();
            }
            else if (received.HasValue)
            {
                raw++;
            }
            else
            {
                unfed++;
            }
        }

        if (cardChoice != null && currentDay < totalDays)
        {
            cardChoice.Begin(currentDay, () => ShowDaySummary(prepared, raw, unfed, absent));
            return;
        }
        ShowDaySummary(prepared, raw, unfed, absent);
    }

    private void ShowDaySummary(int prepared, int raw, int unfed, int absent)
    {
        if (lastSummarizedDay != currentDay)
        {
            totalWoodGained += actions.WoodGainedToday;
            totalFishGained += actions.FishGainedToday;
            totalFoodGained += actions.FoodGainedToday;
            totalCropsStored += actions.CropsStoredToday;
            totalPlotsPlowed += actions.PlotsPlowedToday;
            totalCropsHarvested += actions.CropsHarvestedToday;
            lastSummarizedDay = currentDay;
        }
        feedingOpen = false;
        summaryOpen = true;
        gameOver = currentDay >= totalDays;
        if (finishDayButton != null) finishDayButton.interactable = false;
        if (continueButton != null) continueButton.interactable = !gameOver;
        if (gameOver && continueButtonText != null) continueButtonText.text = "PARTIDA ENCERRADA";

        if (gameOver)
        {
            feedback.ShowSummary(BuildFinalSummary());
            if (merchant != null) merchant.HideShop();
            actions.ReturnWorkersHome();
            return;
        }

        var summary = new StringBuilder();
        summary.AppendLine(gameOver ? "FIM DA PARTIDA" : "RESUMO DO DIA " + currentDay);
        summary.AppendLine("Madeira obtida: +" + actions.WoodGainedToday);
        summary.AppendLine("Comida obtida: +" + actions.FoodGainedToday);
        summary.AppendLine("Peixes guardados: +" + actions.FishGainedToday);
        summary.AppendLine("Colheita guardada: +" + actions.CropsStoredToday);
        summary.AppendLine("Terrenos arados: " + actions.PlotsPlowedToday);
        summary.AppendLine("Culturas colhidas: " + actions.CropsHarvestedToday);
        summary.AppendLine("Alimentos distribuídos: " + (prepared + raw));
        summary.AppendLine("Comida pronta: " + prepared + "  |  Outros: " + raw +
            "  |  Sem comida: " + unfed + "  |  Ausentes: " + absent);
        summary.Append("Dinheiro: " + resources.Coins);
        feedback.ShowSummary(summary.ToString());
        if (merchant != null) merchant.HideShop();
        actions.ReturnWorkersHome();
    }

    private string BuildFinalSummary()
    {
        int houses = 0, cows = 0, chickens = 0;
        if (houseSites != null)
            foreach (HouseSite site in houseSites)
                if (site != null && site.IsBuilt) houses++;
        if (plots != null)
            foreach (Plot plot in plots)
            {
                if (plot == null) continue;
                if (plot.Animal == AnimalKind.Cow) cows++;
                else if (plot.Animal == AnimalKind.Chicken) chickens++;
            }

        var summary = new StringBuilder();
        summary.AppendLine("FIM DA PARTIDA — " + currentDay + " DIAS");
        summary.AppendLine("Moedas finais: " + resources.Coins);
        summary.AppendLine("Coleta: " + totalWoodGained + " madeira | " + totalFishGained + " peixes");
        summary.AppendLine("Comida preparada: " + totalFoodGained);
        summary.AppendLine("Terrenos arados: " + totalPlotsPlowed + " | Colheitas: " + totalCropsHarvested);
        summary.AppendLine("Culturas colhidas: " + totalCropsStored);
        summary.AppendLine("Casas: " + houses + " | Vacas: " + cows + " | Galinhas: " + chickens);
        summary.AppendLine("Estoque: " + resources.Wood + " madeira | " + resources.Fish +
            " peixe | " + resources.Food + " comida");
        summary.AppendLine("Lavoura: " + resources.Corn + " milho | " + resources.Pumpkin +
            " macaxeira | " + resources.Jerimum + " jerimum");
        summary.Append("Animais: " + resources.Milk + " leite | " + resources.Eggs + " ovos");
        return summary.ToString();
    }

    public void ContinueToNextDay()
    {
        if (!summaryOpen || gameOver || actions.IsBusy ||
            (feedback != null && feedback.IsSummaryTransitioning))
            return;

        summaryOpen = false;
        currentDay++;
        ProduceAnimalResources();
        // O crescimento da mandala precede novos eventos para não anular uma praga recém-aplicada.
        if (MandalaSynergy.Instance != null)
            MandalaSynergy.Instance.ApplyDailyGrowthBonuses(currentDay, seedCatalog);
        if (dayEvents != null) dayEvents.StartDay(currentDay);
        actions.BeginNextDay();
        if (houseSites != null)
        {
            foreach (HouseSite site in houseSites)
            {
                if (site == null) continue;
                WorkerAssignment resident = site.CreateResident(currentDay);
                if (resident == null) continue;
                RegisterWorker(resident);
                actions.RegisterWorker(resident);
                if (dayEvents != null) dayEvents.RegisterWorker(resident);
                resident.BeginNextDay();
                resident.SetStatus("em casa");
            }
        }
        foreach (Plot plot in plots)
            if (plot != null) plot.RefreshGrowth(currentDay, seedCatalog);
        UpdateMarket();
        feedback.HideSummary();
        feedback.ShowMessage("Dia " + currentDay + " de " + totalDays);
        if (finishDayButton != null) finishDayButton.interactable = true;
        UpdateDayLabel();
        OpenDayAfterEvent(true);
    }

    private void OpenDayAfterEvent(bool addAnimalResources)
    {
        if (dayEvents != null && dayEvents.IsPanelOpen)
        {
            if (hand != null) hand.SuspendEntranceForEvent();
            dayEvents.AfterEventCardClosed(() =>
            {
                AudioManager.Play(SoundCue.DayStart);
                DayChanged?.Invoke();
                dayEvents.ReleasePendingReward();
                if (addAnimalResources) AddDailyAnimalResources();
            });
            return;
        }
        AudioManager.Play(SoundCue.DayStart);
        DayChanged?.Invoke();
        dayEvents?.ReleasePendingReward();
        if (addAnimalResources) AddDailyAnimalResources();
    }

    private void UpdateMarket()
    {
        if (merchant != null)
            merchant.SetDay(currentDay, missionBoard != null && missionBoard.HasDeliveryDueOn(currentDay));
        if (missionBoard != null) missionBoard.SetDay(currentDay, merchant != null && merchant.IsAvailable);
    }

    private void UpdateDayLabel()
    {
        if (dayText != null)
            dayText.text = "Dia " + currentDay;
    }
}
