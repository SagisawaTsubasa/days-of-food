using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// The repeat-mode dropdown. The "1/3/5 days" modes are deliberately NOT new
    /// <see cref="BillRepeatModeDef"/>s: RimWorld's repeat-mode system is hardcoded
    /// (Bill_Production.ShouldDoNow / RepeatInfoText throw on an unrecognised mode every frame),
    /// so an auto mode is TargetCount plus a per-bill "days" flag stored in
    /// <see cref="AutoFoodGameComponent"/>. Vanilla's counting/gating/+/- UI keep working untouched,
    /// and the daily refresh simply rewrites <c>bill.targetCount</c>.
    ///
    /// Full-replace prefix at Priority.First: among competing full-replace prefixes only the
    /// highest-priority one runs, so ours wins and re-adds the other mods' modes via
    /// <see cref="RepeatModeCompat"/> — its menu is the complete one. The three day-modes are
    /// offered only when the recipe's product is edible food with nutrition > 0.
    /// </summary>
    [HarmonyPatch(typeof(BillRepeatModeUtility), nameof(BillRepeatModeUtility.MakeConfigFloatMenu))]
    public static class Patch_RepeatModeMenu
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(Bill_Production bill)
        {
            var comp = AutoFoodGameComponent.Instance;
            var opts = new List<FloatMenuOption>();

            // --- the three vanilla modes (faithful copies; picking one turns auto tracking OFF) ---
            opts.Add(new FloatMenuOption(BillRepeatModeDefOf.RepeatCount.LabelCap, delegate
            {
                bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                comp?.Clear(bill);
            }));
            opts.Add(new FloatMenuOption(BillRepeatModeDefOf.TargetCount.LabelCap, delegate
            {
                if (!bill.recipe.WorkerCounter.CanCountProducts(bill))
                    Messages.Message("RecipeCannotHaveTargetCount".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                else
                {
                    bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                    comp?.Clear(bill);
                }
            }));
            opts.Add(new FloatMenuOption(BillRepeatModeDefOf.Forever.LabelCap, delegate
            {
                bill.repeatMode = BillRepeatModeDefOf.Forever;
                comp?.Clear(bill);
            }));

            // --- other mods' custom repeat modes that our full replace would otherwise hide ---
            RepeatModeCompat.TryInsertModes(opts, bill);

            // --- the auto day modes: food recipes only ---
            if (comp != null && bill.recipe.WorkerCounter.CanCountProducts(bill)
                && NutritionCalc.TryGetFoodNutritionPerItem(bill.recipe, out _))
            {
                AddDayMode(opts, comp, bill, 1, "DaysOfFood.Menu.OneDay", "DaysOfFood.Menu.TipOneDay");
                AddDayMode(opts, comp, bill, 3, "DaysOfFood.Menu.ThreeDays", "DaysOfFood.Menu.TipThreeDays");
                AddDayMode(opts, comp, bill, 5, "DaysOfFood.Menu.FiveDays", "DaysOfFood.Menu.TipFiveDays");
            }

            Find.WindowStack.Add(new FloatMenu(opts));
            return false; // fully replaces vanilla's 3-entry menu
        }

        private static void AddDayMode(List<FloatMenuOption> opts, AutoFoodGameComponent comp,
            Bill_Production bill, int days, string labelKey, string tipKey)
        {
            var opt = new FloatMenuOption(labelKey.Translate(), delegate
            {
                bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                comp.SetDays(bill, days); // also computes and writes targetCount immediately
            });
            opt.tooltip = tipKey.Translate();
            opts.Add(opt);
        }
    }
}
