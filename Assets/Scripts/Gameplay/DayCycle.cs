using System.Text;
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
    [SerializeField] private Text dayText;
    [SerializeField] private Button finishDayButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Text continueButtonText;
    [SerializeField, Min(1)] private int totalDays = 12;
    [SerializeField, Min(1)] private int foodPerWorker = 2;
    [SerializeField, Min(0)] private int partialFoodPenalty = 50;
    [SerializeField, Min(0)] private int noFoodPenalty = 100;
    [SerializeField, Min(0)] private int partialProductionReduction = 1;
    [SerializeField, Min(1f)] private float noFoodMovementDurationMultiplier = 1.3333334f;
    [SerializeField] private int workerAbsenceReputationThreshold = -200;
    [SerializeField, Range(0f, 1f)] private float workerAbsenceChance = 0.25f;
    [SerializeField, Min(1)] private int workerAbsenceDays = 1;

    private int currentDay = 1;
    private bool summaryOpen;
    private bool gameOver;

    public bool CanAcceptActions => !summaryOpen && !gameOver;
    public int CurrentDay => currentDay;

    private void Start()
    {
        UpdateDayLabel();
        if (merchant != null) merchant.SetDay(currentDay);
        if (missionBoard != null) missionBoard.SetDay(currentDay, merchant != null && merchant.IsAvailable);
    }

    public void FinishDay()
    {
        if (!CanAcceptActions || resources == null || actions == null)
            return;
        if (actions.IsBusy)
        {
            feedback.ShowMessage("Aguarde o fim das ações.");
            return;
        }

        int foodBefore = resources.Food;
        int fullyFed = 0;
        int partiallyFed = 0;
        int unfed = 0;
        int absent = 0;
        int penalty = 0;

        foreach (WorkerAssignment worker in workers)
        {
            if (worker == null) continue;
            if (worker.IsUnavailableToday)
            {
                absent++;
                continue;
            }

            int received = resources.ConsumeFood(Mathf.Min(foodPerWorker, resources.Food));
            if (received >= foodPerWorker)
            {
                fullyFed++;
                worker.ScheduleNextDayCondition(0, 1f);
            }
            else if (received > 0)
            {
                partiallyFed++;
                penalty += partialFoodPenalty;
                worker.ScheduleNextDayCondition(partialProductionReduction, 1f);
            }
            else
            {
                unfed++;
                penalty += noFoodPenalty;
                worker.ScheduleNextDayCondition(0, noFoodMovementDurationMultiplier);
            }
        }

        resources.AddReputation(-penalty);
        summaryOpen = true;
        gameOver = currentDay >= totalDays;
        if (finishDayButton != null) finishDayButton.interactable = false;
        if (continueButton != null) continueButton.interactable = !gameOver;
        if (gameOver && continueButtonText != null) continueButtonText.text = "PARTIDA ENCERRADA";

        var summary = new StringBuilder();
        summary.AppendLine(gameOver ? "FIM DA PARTIDA" : "RESUMO DO DIA " + currentDay);
        summary.AppendLine("Madeira obtida: +" + actions.WoodGainedToday);
        summary.AppendLine("Comida obtida: +" + actions.FoodGainedToday);
        summary.AppendLine("Terrenos arados: " + actions.PlotsPlowedToday);
        summary.AppendLine("Culturas colhidas: " + actions.CropsHarvestedToday);
        summary.AppendLine("Reputação da colheita: +" + actions.HarvestScoreGainedToday);
        summary.AppendLine("Comida consumida: " + (foodBefore - resources.Food));
        summary.AppendLine("Alimentados: " + fullyFed + "  |  Parcial: " + partiallyFed +
            "  |  Sem comida: " + unfed + "  |  Ausentes: " + absent);
        summary.AppendLine("Penalidade: -" + penalty);
        summary.Append("Reputação: " + resources.Reputation + "  |  Moedas: " + resources.Coins);
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
        if (resources.Reputation <= workerAbsenceReputationThreshold &&
            UnityEngine.Random.value < workerAbsenceChance)
        {
            var availableWorkers = new System.Collections.Generic.List<WorkerAssignment>();
            foreach (WorkerAssignment worker in workers)
                if (worker != null && !worker.IsUnavailableToday) availableWorkers.Add(worker);
            if (availableWorkers.Count > 1)
                availableWorkers[UnityEngine.Random.Range(0, availableWorkers.Count)].ScheduleAbsence(workerAbsenceDays);
        }
        actions.BeginNextDay();
        foreach (Plot plot in plots)
            if (plot != null) plot.RefreshGrowth(currentDay, seedCatalog);
        if (merchant != null) merchant.SetDay(currentDay);
        if (missionBoard != null) missionBoard.SetDay(currentDay, merchant != null && merchant.IsAvailable);
        feedback.HideSummary();
        feedback.ShowMessage("Dia " + currentDay + " de " + totalDays);
        if (finishDayButton != null) finishDayButton.interactable = true;
        UpdateDayLabel();
    }

    private void UpdateDayLabel()
    {
        if (dayText != null)
            dayText.text = "Dia: " + currentDay + " / " + totalDays;
    }
}
