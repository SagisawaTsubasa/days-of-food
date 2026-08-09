using HarmonyLib;
using RimWorld;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// Prepend a "[自动3天] / [Auto 3d]" marker to a tracked bill's row info text (e.g. "[自动3天] 45/90"),
    /// so an auto bill is recognisable at a glance in the bills tab. The marker gates purely on the
    /// tracked flag; the daily refresh drops the flag the moment the bill stops qualifying, so the
    /// marker can never outlive the automation it advertises.
    /// </summary>
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.RepeatInfoText), MethodType.Getter)]
    public static class Patch_RepeatInfoText
    {
        static void Postfix(Bill_Production __instance, ref string __result)
        {
            var comp = AutoFoodGameComponent.Instance;
            if (comp != null && comp.IsTracked(__instance))
                __result = "DaysOfFood.RowMarker".Translate(comp.DaysOf(__instance)) + __result;
        }
    }
}
