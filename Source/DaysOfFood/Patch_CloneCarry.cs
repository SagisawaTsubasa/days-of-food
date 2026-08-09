using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// Carry a bill's auto-tracked "days" through clipboard copy/paste. A clone does NOT have its real
    /// loadID inside <c>Bill_Production.Clone()</c> (the caller assigns it via InitializeAfterClone
    /// AFTER Clone returns), so the days value is stashed against the CLONE OBJECT in a weak table and
    /// written to the tracked dict (under the now-real loadID) when the clone is added to a bill stack.
    /// Weak keys: an un-pasted clipboard clone is collected without leaking. Pattern from Hauler's Dream.
    /// </summary>
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Clone))]
    public static class Patch_BillClone_CarryDays
    {
        internal static readonly ConditionalWeakTable<Bill, DaysCarrier> Carry =
            new ConditionalWeakTable<Bill, DaysCarrier>();

        internal sealed class DaysCarrier
        {
            public int days;
            public DaysCarrier(int days) { this.days = days; }
        }

        static void Postfix(Bill_Production __instance, ref Bill __result)
        {
            if (!(__result is Bill_Production clone))
                return;
            int days = AutoFoodGameComponent.Instance?.DaysOf(__instance) ?? 0;
            // Also re-carry when the source is itself an un-pasted clipboard clone.
            if (days <= 0 && Carry.TryGetValue(__instance, out var prev))
                days = prev.days;
            Carry.Remove(clone);
            if (days > 0)
                Carry.Add(clone, new DaysCarrier(days));
        }
    }

    /// <summary>Drain the carried days once the clone is added with its real loadID. BillStack.AddBill
    /// is not called during save load, so loaded bills are untouched.</summary>
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.AddBill))]
    public static class Patch_BillStackAddBill_ApplyCarriedDays
    {
        static void Postfix(Bill bill)
        {
            if (!(bill is Bill_Production prod))
                return;
            if (!Patch_BillClone_CarryDays.Carry.TryGetValue(bill, out var carried))
                return;
            Patch_BillClone_CarryDays.Carry.Remove(bill);
            if (carried.days > 0 && prod.repeatMode == BillRepeatModeDefOf.TargetCount
                && NutritionCalc.TryGetFoodNutritionPerItem(prod.recipe, out _))
            {
                AutoFoodGameComponent.Instance?.SetDays(prod, carried.days);
            }
        }
    }
}
