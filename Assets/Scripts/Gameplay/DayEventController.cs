using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public enum SertaoEventKind { None, Drought, Pests, Rain, Comadre, Calm }

public sealed class DayEventController : MonoBehaviour
{
    [SerializeField] private EventCardView card;
    [SerializeField] private Plot[] plots;
    [SerializeField] private WorkerAssignment[] workers;
    [Header("Artes dos eventos")]
    [SerializeField] private Sprite droughtArt;
    [SerializeField] private Sprite rainArt;
    [SerializeField] private Sprite comadreArt;
    [SerializeField] private Sprite calmArt;
    [Header("Parâmetros provisórios de eventos")]
    [SerializeField, Range(0f, 1f)] private float generalEventChance = 0.35f;
    [SerializeField, Min(1)] private int eventDurationDays = 2;
    [SerializeField, Min(0)] private int droughtFishingReduction = 1;
    [SerializeField, Min(0)] private int pestHarvestReduction = 1;
    [SerializeField, Min(0)] private int droughtGrowthDelay = 1;
    [SerializeField, Min(0)] private int rainGrowthBonus = 1;
    [SerializeField, Min(1)] private int forestExploitationThreshold = 6;
    [SerializeField, Range(0f, 1f)] private float comadreChance = 0.5f;
    [SerializeField, Min(1)] private int comadreAbsenceDays = 1;
    [SerializeField, Min(0)] private int rainForestRecovery = 2;

    private SertaoEventKind activeGeneralEvent;
    private int generalDaysRemaining;
    private int forestExploitation;
    private readonly Dictionary<int, (string Title, Sprite Art)> announcedEvents = new();

    public bool IsPanelOpen => card != null && card.IsOpen;
    public bool TryGetEvent(int day, out string title, out Sprite art)
    {
        if (announcedEvents.TryGetValue(day, out var record))
        {
            title = record.Title;
            art = record.Art;
            return true;
        }
        title = null;
        art = null;
        return false;
    }

    public void AfterEventCardClosed(System.Action action)
    {
        if (card == null || !card.IsOpen)
        {
            action?.Invoke();
            return;
        }
        void OnClosed()
        {
            card.Closed -= OnClosed;
            action?.Invoke();
        }
        card.Closed += OnClosed;
    }
    public int FishingReduction => activeGeneralEvent == SertaoEventKind.Drought ? droughtFishingReduction : 0;
    public int HarvestReduction => activeGeneralEvent == SertaoEventKind.Pests ? pestHarvestReduction : 0;

    public void RegisterWorker(WorkerAssignment worker)
    {
        if (worker == null || System.Array.IndexOf(workers, worker) >= 0) return;
        workers = new List<WorkerAssignment>(workers) { worker }.ToArray();
    }

    public void RecordWood(int amount)
    {
        forestExploitation += Mathf.Max(0, amount);
    }

    public void StartDay(int day)
    {
        if (generalDaysRemaining > 0) generalDaysRemaining--;
        if (generalDaysRemaining == 0) activeGeneralEvent = SertaoEventKind.None;

        SertaoEventKind announced = SertaoEventKind.None;
        if (forestExploitation >= forestExploitationThreshold && Random.value < comadreChance)
        {
            var candidates = new System.Collections.Generic.List<WorkerAssignment>();
            foreach (var worker in workers)
                if (worker != null && !worker.IsUnavailableToday) candidates.Add(worker);
            if (candidates.Count > 1)
            {
                candidates[Random.Range(0, candidates.Count)].ScheduleAbsence(comadreAbsenceDays);
                forestExploitation = 0;
                announced = SertaoEventKind.Comadre;
            }
        }
        if (announced == SertaoEventKind.None && activeGeneralEvent == SertaoEventKind.None &&
            Random.value < generalEventChance)
        {
            activeGeneralEvent = (SertaoEventKind)Random.Range(1, 4);
            generalDaysRemaining = eventDurationDays;
            announced = activeGeneralEvent;
        }

        if (activeGeneralEvent == SertaoEventKind.Drought)
        {
            foreach (var plot in plots) if (plot != null) plot.AdvanceGrowth(-droughtGrowthDelay);
        }
        else if (activeGeneralEvent == SertaoEventKind.Rain)
        {
            foreach (var plot in plots) if (plot != null) plot.AdvanceGrowth(rainGrowthBonus);
            forestExploitation = Mathf.Max(0, forestExploitation - rainForestRecovery);
        }
        if (announced == SertaoEventKind.None && activeGeneralEvent == SertaoEventKind.None)
            announced = SertaoEventKind.Calm;
        if (announced != SertaoEventKind.None)
            announcedEvents[day] = (Title(announced), Art(announced));
        if (announced != SertaoEventKind.None && card != null)
            card.Show(Title(announced), Description(announced), Art(announced));
    }

    private Sprite Art(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => droughtArt,
        SertaoEventKind.Rain => rainArt,
        SertaoEventKind.Comadre => comadreArt,
        SertaoEventKind.Calm => calmArt,
        _ => null
    };

    private string Title(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "SECA",
        SertaoEventKind.Pests => "PRAGAS",
        SertaoEventKind.Rain => "CHUVA",
        SertaoEventKind.Calm => "DIA TRANQUILO",
        _ => "COMADRE FULOZINHA"
    };

    private string Description(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "A seca reduz a pesca e atrasa o crescimento. Duração: " + eventDurationDays + " dia(s).",
        SertaoEventKind.Pests => "As pragas reduzem a colheita. Duração: " + eventDurationDays + " dia(s).",
        SertaoEventKind.Rain => "A chuva acelera o crescimento e reduz o impacto da coleta de madeira. Duração: " + eventDurationDays + " dia(s).",
        SertaoEventKind.Calm => "O dia começou tranquilo. Nenhum evento especial altera a rotina hoje.",
        _ => "A exploração da floresta desorientou um trabalhador por " + comadreAbsenceDays + " dia(s)."
    };
}
