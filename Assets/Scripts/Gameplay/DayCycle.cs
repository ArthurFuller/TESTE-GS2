using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class DayCycle : MonoBehaviour
{
    [SerializeField] private ResourceBank resources;
    [SerializeField] private ActionResolver actions;
    [SerializeField] private GameFeedback feedback;
    [SerializeField] private WorkerAssignment[] workers;
    [SerializeField] private Plot[] plots;
    [SerializeField] private SeedCatalog seedCatalog;
    [SerializeField] private SeedMerchant merchant;
    [SerializeField] private DeliveryMissionBoard missionBoard;
    [SerializeField] private DayEventController dayEvents;
    [SerializeField] private FeedingMinigame feedingGame;
    [SerializeField] private CardRewardChoice cardChoice;
    [SerializeField] private Text dayText;
    [SerializeField] private Button finishDayButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueButtonText;
    [SerializeField, Min(1)] private int totalDays = 12;
    [SerializeField, Min(1)] private int milkPerCow = 1;
    [SerializeField, Min(1)] private int eggsPerChicken = 1;
    [SerializeField, Min(1f)] private float noFoodMovementDurationMultiplier = 1.3333334f;

    private int currentDay = 1;
    private bool summaryOpen;
    private bool feedingOpen;
    private bool gameOver;
    private int todayMilk;
    private int todayEggs;

    public bool CanAcceptActions => !summaryOpen && !feedingOpen && !gameOver &&
        (dayEvents == null || !dayEvents.IsPanelOpen);
    public int CurrentDay => currentDay;

    public bool TryRepeatAnimalProduction(out int milk, out int eggs)
    {
        milk = todayMilk;
        eggs = todayEggs;
        if (milk + eggs == 0 || resources == null) return false;
        resources.AddAnimalProducts(milk, eggs);
        foreach (Plot plot in plots)
            if (plot != null && plot.Animal != AnimalKind.None) plot.PulseAnimal();
        return true;
    }

    private void ProduceAnimalResources()
    {
        todayMilk = 0;
        todayEggs = 0;
        foreach (Plot plot in plots)
        {
            if (plot == null) continue;
            if (plot.Animal == AnimalKind.Cow) todayMilk += milkPerCow;
            if (plot.Animal == AnimalKind.Chicken) todayEggs += eggsPerChicken;
            if (plot.Animal != AnimalKind.None) plot.PulseAnimal();
        }
        if (todayMilk + todayEggs == 0) return;
        resources.AddAnimalProducts(todayMilk, todayEggs);
        if (feedback != null)
            feedback.ShowMessage("Animais: +" + todayMilk + " leite e +" + todayEggs + " ovos.");
    }
    public event Action DayChanged;

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
        DayChanged?.Invoke();
    }

    public void FinishDay()
    {
        if (!CanAcceptActions || resources == null || actions == null)
            return;
        if (actions.IsBusy)
        {
            feedback.ShowMessage(actions.GatheringNeedsPlayer ?
                "Inicie a coleta pendente pelo botão do trabalhador." : "Aguarde o fim das ações.");
            return;
        }

        if (feedingGame == null)
        {
            feedback.ShowMessage("Minigame de alimentação não configurado.");
            return;
        }
        feedingOpen = true;
        if (finishDayButton != null) finishDayButton.interactable = false;
        feedingGame.Begin(workers, FinishFeeding);
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
                worker.ScheduleNextDayCondition(0, 1f);
            }
            else if (received.HasValue)
            {
                raw++;
                worker.ScheduleNextDayCondition(0, 1f);
            }
            else
            {
                unfed++;
                worker.ScheduleNextDayCondition(0, noFoodMovementDurationMultiplier);
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
        feedingOpen = false;
        summaryOpen = true;
        gameOver = currentDay >= totalDays;
        if (finishDayButton != null) finishDayButton.interactable = false;
        if (continueButton != null) continueButton.interactable = !gameOver;
        if (gameOver && continueButtonText != null) continueButtonText.text = "PARTIDA ENCERRADA";

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

    public void ContinueToNextDay()
    {
        if (!summaryOpen || gameOver || actions.IsBusy)
            return;

        summaryOpen = false;
        currentDay++;
        resources.AdvanceFoodDay(currentDay);
        ProduceAnimalResources();
        if (dayEvents != null) dayEvents.StartDay(currentDay);
        actions.BeginNextDay();
        foreach (Plot plot in plots)
            if (plot != null) plot.RefreshGrowth(currentDay, seedCatalog);
        UpdateMarket();
        feedback.HideSummary();
        feedback.ShowMessage("Dia " + currentDay + " de " + totalDays);
        if (finishDayButton != null) finishDayButton.interactable = true;
        UpdateDayLabel();
        DayChanged?.Invoke();
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
