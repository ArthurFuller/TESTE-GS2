using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public enum SertaoEventKind { None, Drought, Pests, Rain, Comadre, Calm, HeadOfCuia }

public sealed class DayEventController : MonoBehaviour
{
    public readonly struct TimelineEvent
    {
        public readonly string Title;
        public readonly string Description;
        public readonly Sprite Art;
        public readonly int StartedOn;

        public TimelineEvent(string title, string description, Sprite art, int startedOn)
        {
            Title = title;
            Description = description;
            Art = art;
            StartedOn = startedOn;
        }
    }

    private sealed class ActiveComadre
    {
        public int StartedOn;
        public int DaysRemaining;
    }

    private sealed class ActiveGeneral
    {
        public SertaoEventKind Kind;
        public int StartedOn;
        public int DaysRemaining;
    }

    [SerializeField] private EventCardView card;
    [SerializeField] private ResourceBank resources;
    [SerializeField] private Plot[] plots;
    [SerializeField] private WorkerAssignment[] workers;
    [Header("Artes dos eventos")]
    [SerializeField] private Sprite droughtArt;
    [SerializeField] private Sprite rainArt;
    [SerializeField] private Sprite comadreArt;
    [SerializeField] private Sprite calmArt;
    [SerializeField] private Sprite headOfCuiaArt;
    [Header("Parâmetros provisórios de eventos")]
    [SerializeField, Range(0f, 1f)] private float generalEventChance = 0.35f;
    [SerializeField, Min(1)] private int eventDurationDays = 2;
    [SerializeField, Min(0)] private int droughtFishingReduction = 1;
    [SerializeField, Min(0)] private int pestHarvestReduction = 1;
    [Header("Pragas na macaxeira - configurar apos definir balanceamento")]
    [SerializeField, Min(0)] private int cassavaRegressionWithoutProtection;
    [SerializeField, Min(0)] private int cassavaRegressionWithJerimum;
    [SerializeField, Min(0)] private int cassavaRegressionWithJerimumAndCow;
    [SerializeField, Min(0)] private int droughtGrowthDelay = 1;
    [SerializeField, Min(0)] private int rainGrowthBonus = 1;
    [SerializeField, Min(1)] private int forestExploitationThreshold = 6;
    [SerializeField, Range(0f, 1f)] private float comadreChance = 0.5f;
    [SerializeField, Min(1)] private int comadreAbsenceDays = 1;
    [SerializeField, Min(0)] private int rainForestRecovery = 2;
    [SerializeField, Min(1)] private int headOfCuiaFishTheft = 2;

    private readonly List<ActiveGeneral> activeGeneralEvents = new();
    private SertaoEventKind lastAnnounced;
    private int forestExploitation;
    private int stolenFishToday;
    private CropType pestTarget = CropType.None;
    public bool IsRaining => IsActive(SertaoEventKind.Rain);
    public bool IsDrought => IsActive(SertaoEventKind.Drought);
    public event System.Action WeatherChanged;
    public CropType PestTarget => pestTarget;
    public int HarvestReductionFor(CropType crop) =>
        crop != CropType.None && crop == pestTarget && IsActive(SertaoEventKind.Pests)
            ? pestHarvestReduction : 0;
    private readonly List<ActiveComadre> activeComadre = new();
    private readonly Dictionary<int, List<TimelineEvent>> eventsByDay = new();

    public bool IsPanelOpen => card != null && card.IsOpen;
    public IReadOnlyList<TimelineEvent> EventsForDay(int day) =>
        eventsByDay.TryGetValue(day, out List<TimelineEvent> events) ?
            events : System.Array.Empty<TimelineEvent>();

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
    public int FishingReduction => IsActive(SertaoEventKind.Drought) ? droughtFishingReduction : 0;
    public int HarvestReduction => IsActive(SertaoEventKind.Pests) ? pestHarvestReduction : 0;

    private bool IsActive(SertaoEventKind kind)
    {
        foreach (ActiveGeneral active in activeGeneralEvents)
            if (active.Kind == kind) return true;
        return false;
    }

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
        stolenFishToday = 0;
        for (int i = activeGeneralEvents.Count - 1; i >= 0; i--)
            if (--activeGeneralEvents[i].DaysRemaining <= 0) activeGeneralEvents.RemoveAt(i);
        for (int i = activeComadre.Count - 1; i >= 0; i--)
        {
            activeComadre[i].DaysRemaining--;
            if (activeComadre[i].DaysRemaining <= 0) activeComadre.RemoveAt(i);
        }

        if (!IsActive(SertaoEventKind.Pests)) pestTarget = CropType.None;
        SertaoEventKind announced = SertaoEventKind.None;
        if (lastAnnounced != SertaoEventKind.Comadre && activeComadre.Count == 0 &&
            forestExploitation >= forestExploitationThreshold && Random.value < comadreChance)
        {
            var candidates = new System.Collections.Generic.List<WorkerAssignment>();
            foreach (var worker in workers)
                if (worker != null && !worker.IsUnavailableToday) candidates.Add(worker);
            if (candidates.Count > 1)
            {
                candidates[Random.Range(0, candidates.Count)].ScheduleAbsence(comadreAbsenceDays);
                activeComadre.Add(new ActiveComadre
                {
                    StartedOn = day, DaysRemaining = comadreAbsenceDays
                });
                forestExploitation = 0;
                announced = SertaoEventKind.Comadre;
            }
        }
        if (announced == SertaoEventKind.None && Random.value < generalEventChance)
        {
            var options = new List<SertaoEventKind>();
            foreach (SertaoEventKind kind in new[] { SertaoEventKind.Drought, SertaoEventKind.Pests,
                         SertaoEventKind.Rain, SertaoEventKind.HeadOfCuia })
            {
                if (kind == lastAnnounced || IsActive(kind) ||
                    (kind == SertaoEventKind.Drought && IsActive(SertaoEventKind.Rain)) ||
                    (kind == SertaoEventKind.Rain && IsActive(SertaoEventKind.Drought)) ||
                    (kind == SertaoEventKind.HeadOfCuia && (resources == null || resources.Fish <= 0)))
                    continue;
                options.Add(kind);
            }
            if (options.Count > 0) announced = options[Random.Range(0, options.Count)];
            if (announced == SertaoEventKind.HeadOfCuia)
            {
                stolenFishToday = Mathf.Min(resources.Fish, headOfCuiaFishTheft);
                resources.TryDeliverFish(stolenFishToday);
            }
            else if (announced != SertaoEventKind.None)
            {
                if (announced == SertaoEventKind.Pests)
                {
                    pestTarget = SelectAffectedCrop();
                    ApplyPestRegression();
                }
                activeGeneralEvents.Add(new ActiveGeneral
                {
                    Kind = announced, StartedOn = day, DaysRemaining = eventDurationDays
                });
            }
        }

        if (IsActive(SertaoEventKind.Drought))
        {
            foreach (var plot in plots) if (plot != null) plot.AdvanceGrowth(-droughtGrowthDelay);
        }
        else if (IsActive(SertaoEventKind.Rain))
        {
            foreach (var plot in plots) if (plot != null) plot.AdvanceGrowth(rainGrowthBonus);
            forestExploitation = Mathf.Max(0, forestExploitation - rainForestRecovery);
        }
        if (announced == SertaoEventKind.None) announced = SertaoEventKind.Calm;
        var activeToday = new List<TimelineEvent>();
        foreach (ActiveGeneral active in activeGeneralEvents)
            activeToday.Add(new TimelineEvent(Title(active.Kind),
                Description(active.Kind), Art(active.Kind), active.StartedOn));
        foreach (ActiveComadre comadre in activeComadre)
            activeToday.Add(new TimelineEvent(Title(SertaoEventKind.Comadre),
                Description(SertaoEventKind.Comadre),
                Art(SertaoEventKind.Comadre), comadre.StartedOn));
        if (announced == SertaoEventKind.Calm)
            activeToday.Add(new TimelineEvent(Title(announced),
                Description(announced), Art(announced), day));
        else if (announced == SertaoEventKind.HeadOfCuia)
            activeToday.Add(new TimelineEvent(Title(announced),
                Description(announced), Art(announced), day));
        activeToday.Sort((a, b) => a.StartedOn.CompareTo(b.StartedOn));
        eventsByDay[day] = activeToday;
        lastAnnounced = announced;
        WeatherChanged?.Invoke();
        if (announced != SertaoEventKind.None && card != null)
        {
            card.Show(Title(announced), Description(announced), Art(announced));
            switch (announced)
            {
                case SertaoEventKind.Drought: AudioManager.Play(SoundCue.EventDrought); break;
                case SertaoEventKind.Rain: AudioManager.Play(SoundCue.EventRain); break;
                case SertaoEventKind.Pests: AudioManager.Play(SoundCue.EventPests); break;
                case SertaoEventKind.Comadre: AudioManager.Play(SoundCue.EventComadre); break;
                case SertaoEventKind.HeadOfCuia: AudioManager.Play(SoundCue.EventHeadOfCuia); break;
            }
        }
    }

    private void ApplyPestRegression()
    {
        if (pestTarget != CropType.Pumpkin || plots == null) return;
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.Crop != CropType.Pumpkin) continue;
            MandalaCombo combo = MandalaSynergy.Instance != null ?
                MandalaSynergy.Instance.For(plot) : MandalaCombo.None;
            int stages = (combo & MandalaCombo.CassavaJerimumCow) != 0 ?
                cassavaRegressionWithJerimumAndCow :
                (combo & MandalaCombo.CassavaJerimum) != 0 ?
                cassavaRegressionWithJerimum : cassavaRegressionWithoutProtection;
            if (stages > 0) plot.AdvanceGrowth(-stages);
        }
    }

    private CropType SelectAffectedCrop()
    {
        var options = new List<CropType>();
        if (plots != null)
            foreach (Plot plot in plots)
                if (plot != null && plot.Crop != CropType.None && !options.Contains(plot.Crop))
                    options.Add(plot.Crop);
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : CropType.None;
    }

    private Sprite Art(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => droughtArt,
        SertaoEventKind.Rain => rainArt,
        SertaoEventKind.Comadre => comadreArt,
        SertaoEventKind.Calm => calmArt,
        SertaoEventKind.HeadOfCuia => headOfCuiaArt,
        _ => null
    };

    private string Title(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "SECA",
        SertaoEventKind.Pests => "PRAGAS",
        SertaoEventKind.Rain => "CHUVA",
        SertaoEventKind.Calm => "DIA TRANQUILO",
        SertaoEventKind.HeadOfCuia => "CABEÇA DE CUIA",
        _ => "COMADRE FULOZINHA"
    };

    private string Description(SertaoEventKind kind) => kind switch
    {
        SertaoEventKind.Drought => "A seca reduz a pesca e atrasa o crescimento. Duração: " + eventDurationDays + " dia(s).",
        SertaoEventKind.Pests => "As pragas afetam " +
            (pestTarget == CropType.Pumpkin ? "a macaxeira" :
             pestTarget == CropType.Corn ? "o milho" :
             pestTarget == CropType.Jerimum ? "o jerimum" : "as lavouras") +
            ". Duração: " + eventDurationDays + " dia(s).",
        SertaoEventKind.Rain => "A chuva acelera o crescimento e reduz o impacto da coleta de madeira. Duração: " + eventDurationDays + " dia(s).",
        SertaoEventKind.Calm => "Nenhum novo evento especial começou hoje.",
        SertaoEventKind.HeadOfCuia => "O Cabeça de Cuia roubou " + stolenFishToday +
            (stolenFishToday == 1 ? " peixe." : " peixes."),
        _ => "A exploração da floresta desorientou um trabalhador por " + comadreAbsenceDays + " dia(s)."
    };
}
