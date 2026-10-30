using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace HoldDraft;

public sealed class DraftMonitor : GameComponent
{
    public const int CheckInterval = 600;
    private readonly HashSet<Pawn> tracked = new();
    private readonly List<Pawn> overdue = new();
    private readonly List<Pawn> remove = new();
    public static DraftMonitor Current => Verse.Current.Game?.GetComponent<DraftMonitor>();
    public int TrackedCount => tracked.Count;
    public int LastVisitCount { get; private set; }
    public IReadOnlyList<Pawn> Overdue => overdue;
    public DraftMonitor(Game game) { }
    public static bool Eligible(Pawn pawn) => pawn != null && !pawn.Destroyed && !pawn.Dead && pawn.Drafted && pawn.Faction?.IsPlayer == true && pawn.RaceProps?.Humanlike == true;
    public void Observe(Pawn pawn)
    {
        if (pawn == null || Scribe.mode != LoadSaveMode.Inactive) return;
        if (Eligible(pawn))
        {
            var s = DraftState.Raw(pawn);
            if (s.draftedSince < 0) s.draftedSince = Find.TickManager.TicksGame;
            tracked.Add(pawn);
        }
        else
        {
            tracked.Remove(pawn); overdue.Remove(pawn); DraftState.ClearTimer(pawn);
        }
    }
    public override void FinalizeInit()
    {
        tracked.Clear(); overdue.Clear();
        foreach (Map map in Find.Maps)
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned) Observe(pawn);
        Refresh();
    }
    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % CheckInterval == 0 && tracked.Count != 0) Refresh();
    }
    public void Refresh()
    {
        overdue.Clear(); remove.Clear(); LastVisitCount = 0;
        int now = Find.TickManager.TicksGame;
        foreach (Pawn pawn in tracked)
        {
            LastVisitCount++;
            if (!Eligible(pawn)) { remove.Add(pawn); DraftState.ClearTimer(pawn); continue; }
            if (pawn.Spawned && DraftState.Overdue(now, DraftState.Raw(pawn).draftedSince, DraftMod.Settings.warningHours)) overdue.Add(pawn);
        }
        foreach (Pawn pawn in remove) tracked.Remove(pawn);
    }
    public AlertReport Report => AlertReport.CulpritsAre(overdue);
}

public sealed class Alert_LongDrafted : Alert
{
    public Alert_LongDrafted() { defaultPriority = AlertPriority.Medium; }
    public override string GetLabel() => "SD_AlertLabel".Translate();
    public override TaggedString GetExplanation()
    {
        var text = new StringBuilder();
        var monitor = DraftMonitor.Current;
        if (monitor != null)
            foreach (Pawn pawn in monitor.Overdue) text.AppendLine("  - " + pawn.LabelShortCap);
        return "SD_AlertDesc".Translate(DraftMod.Settings.warningHours.ToString("0"), text.ToString());
    }
    public override AlertReport GetReport() => DraftMonitor.Current?.Report ?? false;
}
