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
    [SerializeField, Min(1)] private int foodPerWorker = 2;
    [SerializeField, Min(0)] private int partialProductionReduction = 1;
    [SerializeField, Min(1f)] private float noFoodMovementDurationMultiplier = 1.3333334f;

    private int currentDay = 1;
    private bool summaryOpen;
    private bool feedingOpen;
    private bool gameOver;
    private int foodBeforeFeeding;

    public bool CanAcceptActions => !summaryOpen && !feedingOpen && !gameOver &&
        (dayEvents == null || !dayEvents.IsPanelOpen);
    public int CurrentDay => currentDay;
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
        foodBeforeFeeding = resources.Food;
        feedingOpen = true;
        if (finishDayButton != null) finishDayButton.interactable = false;
        feedingGame.Begin(workers, foodPerWorker, FinishFeeding);
    }

    private void FinishFeeding(int[] portions)
    {
        int fullyFed = 0;
        int partiallyFed = 0;
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

            int received = portions[i];
            if (received >= foodPerWorker)
            {
                fullyFed++;
                worker.ScheduleNextDayCondition(0, 1f);
            }
            else if (received > 0)
            {
                partiallyFed++;
                worker.ScheduleNextDayCondition(partialProductionReduction, 1f);
            }
            else
            {
                unfed++;
                worker.ScheduleNextDayCondition(0, noFoodMovementDurationMultiplier);
            }
        }

        if (cardChoice != null && currentDay < totalDays)
        {
            cardChoice.Begin(currentDay, () => ShowDaySummary(fullyFed, partiallyFed, unfed, absent));
            return;
        }
        ShowDaySummary(fullyFed, partiallyFed, unfed, absent);
    }

    private void ShowDaySummary(int fullyFed, int partiallyFed, int unfed, int absent)
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
        summary.AppendLine("Comida consumida: " + (foodBeforeFeeding - resources.Food));
        summary.AppendLine("Alimentados: " + fullyFed + "  |  Parcial: " + partiallyFed +
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
