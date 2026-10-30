using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HoldDraft;
public sealed class DraftSettings : ModSettings
{
    public bool showAutoUndraftButton;
    public float warningHours = 24;
    public void Validate() => warningHours = Math.Clamp(float.IsFinite(warningHours) ? warningHours : 24, 1, 168);
    public override void ExposeData()
    {
        Scribe_Values.Look(ref showAutoUndraftButton, "showAutoUndraftButton", false);
        Scribe_Values.Look(ref warningHours, "warningHours", 24);
        Validate();
    }
}
public sealed class DraftMod : Mod
{
    public static DraftSettings Settings = new();
    public DraftMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<DraftSettings>();
        new Harmony("w0bderful.holddraft").PatchAll(typeof(DraftMod).Assembly);
    }
    public override string SettingsCategory() => "Stay Drafted";
    public override void WriteSettings() { Settings.Validate(); base.WriteSettings(); DraftMonitor.Current?.Refresh(); }
    public override void DoSettingsWindowContents(Rect rect)
    {
        var list = new Listing_Standard(); list.Begin(rect);
        list.Label("SD_Help".Translate());
        list.CheckboxLabeled("SD_ShowButton".Translate(), ref Settings.showAutoUndraftButton);
        list.Label("SD_WarningHours".Translate(Settings.warningHours.ToString("0")));
        Settings.warningHours = (float)Math.Round(list.Slider(Settings.warningHours, 1, 168));
        if (list.ButtonText("SD_Reset".Translate())) { Settings.showAutoUndraftButton = false; Settings.warningHours = 24; }
        list.End();
    }
}
public sealed class PawnDraftState
{
    public bool allowAutoUndraft;
    public int draftedSince = -1;
}
public static class DraftState
{
    private static readonly ConditionalWeakTable<Pawn, PawnDraftState> states = new();
    public static PawnDraftState Raw(Pawn pawn) => states.GetOrCreateValue(pawn);
    public static void ClearTimer(Pawn pawn) { if (states.TryGetValue(pawn, out var s)) s.draftedSince = -1; }
    public static bool Overdue(int now, int since, float hours) => since >= 0 && (long)now - since >= hours * 2500d;
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExposeData))]
public static class DraftSavePatch
{
    public static void Postfix(Pawn __instance)
    {
        var state = DraftState.Raw(__instance);
        Scribe_Values.Look(ref state.allowAutoUndraft, "SD_allowAutoUndraft", false);
        Scribe_Values.Look(ref state.draftedSince, "SD_draftedSince", -1);
    }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
public static class DraftTogglePatch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result) yield return gizmo;
        if (!DraftMod.Settings.showAutoUndraftButton || __instance.Faction?.IsPlayer != true || __instance.drafter?.ShowDraftGizmo != true) yield break;
        var state = DraftState.Raw(__instance);
        yield return new Command_Toggle
        {
            defaultLabel = "SD_AllowAuto".Translate(),
            defaultDesc = "SD_AllowAutoDesc".Translate(),
            icon = TexCommand.ClearPrioritizedWork,
            isActive = () => state.allowAutoUndraft,
            toggleAction = () => state.allowAutoUndraft = !state.allowAutoUndraft
        };
    }
}
// Skip only vanilla idle undrafting. Manual/forced changes are never prevented.
[HarmonyPatch(typeof(AutoUndrafter), "ShouldAutoUndraft")]
public static class DraftHoldPatch
{
    public static bool Prefix(Pawn ___pawn, ref bool __result)
    {
        if (___pawn?.Faction?.IsPlayer != true || DraftState.Raw(___pawn).allowAutoUndraft) return true;
        __result = false; return false;
    }
}
[HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
public static class DraftChangedPatch
{
    public static void Prefix(Pawn_DraftController __instance, out bool __state) => __state = __instance.Drafted;
    public static void Postfix(Pawn_DraftController __instance, bool __state)
    {
        if (__state != __instance.Drafted) DraftMonitor.Current?.Observe(__instance.pawn);
    }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
public static class DraftSpawnPatch
{
    public static void Postfix(Pawn __instance) => DraftMonitor.Current?.Observe(__instance);
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction), new[] { typeof(Faction), typeof(Pawn) })]
public static class DraftFactionPatch
{
    public static void Postfix(Pawn __instance) => DraftMonitor.Current?.Observe(__instance);
}
