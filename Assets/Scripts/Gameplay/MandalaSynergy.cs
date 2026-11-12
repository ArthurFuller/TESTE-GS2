using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum MandalaCombo
{
    None = 0,
    CassavaCow = 1,
    CassavaWater = 2,
    CornCow = 4,
    CassavaJerimum = 8,
    CassavaJerimumCow = 16
}

// Identifica somente as cinco relações propostas. Não cria valores de balanceamento.
public sealed class MandalaSynergy : MonoBehaviour
{
    [Serializable]
    public struct SlotEffect
    {
        public Plot plot;
        public ParticleSystem lightParticles;
    }

    [SerializeField] private Plot[] plots;
    [Tooltip("Arraste o poço/lago central; deixe vazio para desativar a sinergia de água.")]
    [SerializeField] private Transform centralWater;
    [SerializeField, Min(0.1f)] private float centralWaterReach = 2.5f;
    [Header("Balanceamento - zero mantém a economia atual")]
    [SerializeField, Min(0)] private int cassavaYieldBonusNearCow;
    [SerializeField, Min(0)] private int cassavaGrowthBonusNearCow;
    [SerializeField, Min(0)] private int cassavaGrowthBonusNearWater;
    [SerializeField, Min(0)] private int milkBonusNearCorn;
    [Header("Uma única emissão por slot; efeitos são editáveis na Hierarchy")]
    [SerializeField] private SlotEffect[] slotEffects;

    private readonly Dictionary<Plot, MandalaCombo> active = new();
    public static MandalaSynergy Instance { get; private set; }
    public event Action Changed;

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }


    private void OnEnable()
    {
        if (plots == null) return;
        foreach (Plot plot in plots)
            if (plot != null) plot.StateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (plots != null)
            foreach (Plot plot in plots)
                if (plot != null) plot.StateChanged -= Refresh;
        active.Clear();
        if (slotEffects == null) return;
        foreach (SlotEffect link in slotEffects)
            if (link.lightParticles != null)
                link.lightParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public MandalaCombo For(Plot plot) => plot != null && active.TryGetValue(plot, out var combos)
        ? combos : MandalaCombo.None;
    public int ExtraCassavaYield(Plot plot) => (For(plot) & MandalaCombo.CassavaCow) != 0
        ? cassavaYieldBonusNearCow : 0;
    public int ExtraMilk(Plot plot) => (For(plot) & MandalaCombo.CornCow) != 0
        ? milkBonusNearCorn : 0;

    public void ApplyDailyGrowthBonuses(int currentDay, SeedCatalog catalog)
    {
        if (plots == null) return;
        foreach (Plot plot in plots)
        {
            if (plot == null || plot.Crop != CropType.Pumpkin) continue;
            MandalaCombo combos = For(plot);
            int growth = 0;
            if ((combos & MandalaCombo.CassavaCow) != 0)
                growth += cassavaGrowthBonusNearCow;
            if ((combos & MandalaCombo.CassavaWater) != 0)
                growth += cassavaGrowthBonusNearWater;
            if (growth > 0) plot.AdvanceGrowthTowardMaturity(growth, currentDay, catalog);
        }
    }

    public void Refresh()
    {
        if (plots == null) return;
        var next = new Dictionary<Plot, MandalaCombo>(plots.Length);
        foreach (Plot plot in plots)
            if (plot != null) next[plot] = MandalaCombo.None;
        Plot waterPlot = centralWater != null ? centralWater.GetComponent<Plot>() : null;
        for (int i = 0; i < plots.Length; i++)
        {
            Plot a = plots[i];
            if (a == null) continue;
            if (a.Crop == CropType.Pumpkin && centralWater != null &&
                Vector3.Distance(a.transform.position, centralWater.position) <= centralWaterReach)
            {
                next[a] |= MandalaCombo.CassavaWater;
                if (waterPlot != null && waterPlot.IsWater && next.ContainsKey(waterPlot))
                    next[waterPlot] |= MandalaCombo.CassavaWater;
            }
            for (int j = i + 1; j < plots.Length; j++)
            {
                Plot b = plots[j];
                if (b == null || !a.IsAdjacentTo(b)) continue;
                AddPair(a, b, next);
                AddPair(b, a, next);
            }
        }
        // Combinação tripla: a mesma macaxeira deve ser adjacente a jerimum e vaca.
        foreach (Plot cassava in plots)
        {
            if (cassava == null || cassava.Crop != CropType.Pumpkin ||
                (next[cassava] & (MandalaCombo.CassavaCow | MandalaCombo.CassavaJerimum)) !=
                (MandalaCombo.CassavaCow | MandalaCombo.CassavaJerimum)) continue;
            next[cassava] |= MandalaCombo.CassavaJerimumCow;
            foreach (Plot other in plots)
                if (other != null && cassava.IsAdjacentTo(other) &&
                    (other.Animal == AnimalKind.Cow || other.Crop == CropType.Jerimum))
                    next[other] |= MandalaCombo.CassavaJerimumCow;
        }
        bool changed = active.Count != next.Count;
        foreach (var item in next)
            if (!active.TryGetValue(item.Key, out var old) || old != item.Value)
                changed = true;
        if (!changed) return;
        active.Clear();
        foreach (var item in next) active.Add(item.Key, item.Value);
        if (slotEffects != null)
            foreach (SlotEffect link in slotEffects)
            {
                if (link.plot == null || link.lightParticles == null) continue;
                bool shouldPlay = For(link.plot) != MandalaCombo.None;
                if (shouldPlay && !link.lightParticles.isEmitting) link.lightParticles.Play();
                else if (!shouldPlay && link.lightParticles.isEmitting)
                    link.lightParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        Changed?.Invoke();
    }

    private static void AddPair(Plot source, Plot other, Dictionary<Plot, MandalaCombo> next)
    {
        if (source.Crop == CropType.Pumpkin && other.Animal == AnimalKind.Cow)
        {
            next[source] |= MandalaCombo.CassavaCow;
            next[other] |= MandalaCombo.CassavaCow;
        }
        if (source.Crop == CropType.Corn && other.Animal == AnimalKind.Cow)
        {
            next[source] |= MandalaCombo.CornCow;
            next[other] |= MandalaCombo.CornCow;
        }
        if (source.Crop == CropType.Pumpkin && other.Crop == CropType.Jerimum)
        {
            next[source] |= MandalaCombo.CassavaJerimum;
            next[other] |= MandalaCombo.CassavaJerimum;
        }
    }
}
