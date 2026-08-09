using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// The mod's UI lives INSIDE the bill details dialog's repeat-mode section, as a native row:
    ///
    ///  - TargetCount ("维持X个"): an "自动维持数量" label plus a vanilla-style value button
    ///    (关闭 / 一天份 / 三天份 / 五天份) that arms daily auto-refresh of the target count.
    ///  - RepeatCount ("做X次"): a "按天填入" button that writes the craft count equivalent to
    ///    N days of food, once (crafts aren't stock — nothing to maintain).
    ///  - Forever / non-food bills: nothing is drawn and no extra height is added.
    ///
    /// Two transpiler edits on Dialog_BillConfig.DoWindowContents:
    ///  1. After the static RepeatModeSubdialogHeight field is loaded, add our extra height so the
    ///     pre-sized section (Listing.BeginSection clips overflow) has room for our rows.
    ///  2. Just before listing_Standard.EndSection(listing_Standard2), draw our rows INSIDE the
    ///     section, so they sit right under the vanilla "数量达到时暂停" block.
    ///
    /// We never touch the repeat-mode dropdown itself, so no mod that edits that menu can conflict
    /// with us. Fail-safe: any mismatch returns the original body unchanged (rows just don't appear).
    /// </summary>
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
    public static class Patch_ConfigSection
    {
        /// <summary>Extra section height needed when our rows are visible (checkbox + days entry).</summary>
        private const int ExtraHeight = 84;

        // Per-dialog IntEntry edit buffer. Dialog_BillConfig is modal — at most one is open at a
        // time — so a single static buffer is safe, mirroring vanilla's own *EditBuffer fields.
        private static string daysEditBuffer;

        // NB: RepeatModeSubdialogHeight is an int field — this signature must take/return int,
        // an int-on-stack call to a float method is invalid IL.
        public static int AdjustHeight(int height, Bill_Production bill)
        {
            if (bill != null && bill.repeatMode == BillRepeatModeDefOf.TargetCount
                && NutritionCalc.TryGetFoodNutritionPerItem(bill.recipe, out _))
                return height + ExtraHeight;
            return height;
        }

        /// <summary>
        /// Draw the "自动维持数量" block, mirroring the vanilla "数量达到时暂停" block right above it:
        /// a checkbox toggle, and — while enabled — a label + vanilla IntEntry for the day count.
        /// Any positive day count is accepted (1/3/5 are just the obvious picks).
        /// </summary>
        public static void DrawAutoRow(Listing_Standard listing, Bill_Production bill)
        {
            if (listing == null || bill == null)
                return;
            if (bill.repeatMode != BillRepeatModeDefOf.TargetCount)
                return; // TargetCount only
            if (!NutritionCalc.TryGetFoodNutritionPerItem(bill.recipe, out _))
                return;
            var comp = AutoFoodGameComponent.Instance;
            if (comp == null)
                return;

            bool enabled = comp.IsTracked(bill);
            bool toggled = enabled;
            var checkRect = listing.GetRect(22f);
            Widgets.CheckboxLabeled(checkRect, "DaysOfFood.Section.AutoLabel".Translate(), ref toggled);
            TooltipHandler.TipRegion(checkRect, "DaysOfFood.Section.TipToggle".Translate());
            if (toggled != enabled)
            {
                if (toggled)
                    comp.SetDays(bill, 1); // enabling starts at 1 day; the player edits it below
                else
                    comp.Clear(bill);
            }

            if (comp.IsTracked(bill))
            {
                int days = comp.DaysOf(bill);
                listing.Label("DaysOfFood.Section.DaysLabel".Translate(days));
                int edited = days;
                listing.IntEntry(ref edited, ref daysEditBuffer);
                // Days can never go below 1 — 0 would mean "maintain nothing", which is what the
                // checkbox being OFF already says. Mirrors vanilla's unpause clamp just above.
                if (edited < 1)
                {
                    edited = 1;
                    daysEditBuffer = edited.ToStringCached();
                }
                if (edited != days)
                    comp.SetDays(bill, edited); // recomputes the target immediately
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            try
            {
                var heightField = AccessTools.Field(typeof(Dialog_BillConfig), "RepeatModeSubdialogHeight");
                var billField = AccessTools.Field(typeof(Dialog_BillConfig), "bill");
                var adjust = AccessTools.Method(typeof(Patch_ConfigSection), nameof(AdjustHeight));
                var draw = AccessTools.Method(typeof(Patch_ConfigSection), nameof(DrawAutoRow));
                var endSection = AccessTools.Method(typeof(Listing_Standard), nameof(Listing_Standard.EndSection));
                if (heightField == null || billField == null || adjust == null || draw == null || endSection == null)
                {
                    Log.Warning("[Days of Food] config-section transpiler: a target member did not resolve; auto rows disabled.");
                    return code;
                }

                var output = new List<CodeInstruction>(code.Count + 12);
                bool heightDone = false, rowDone = false;
                for (int i = 0; i < code.Count; i++)
                {
                    var ins = code[i];

                    // 1. height adjustment: `ldsfld RepeatModeSubdialogHeight` -> append adjust call.
                    output.Add(ins);
                    if (!heightDone && ins.opcode == OpCodes.Ldsfld && Equals(ins.operand, heightField))
                    {
                        output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                        output.Add(new CodeInstruction(OpCodes.Ldfld, billField));
                        output.Add(new CodeInstruction(OpCodes.Call, adjust));
                        heightDone = true;
                        continue;
                    }

                    // 2. row injection: before `ldloc X; callvirt EndSection` -> draw our rows first.
                    if (!rowDone && i + 1 < code.Count
                        && (code[i + 1].opcode == OpCodes.Callvirt || code[i + 1].opcode == OpCodes.Call)
                        && Equals(code[i + 1].operand, endSection)
                        && ins.IsLdloc())
                    {
                        // Re-load the same section local, then the bill field, then draw.
                        var reload = ins.Clone();
                        reload.labels.Clear();
                        reload.blocks.Clear();
                        output.Add(reload);
                        output.Add(new CodeInstruction(OpCodes.Ldarg_0));
                        output.Add(new CodeInstruction(OpCodes.Ldfld, billField));
                        output.Add(new CodeInstruction(OpCodes.Call, draw));
                        rowDone = true;
                        // fall through: `ins` (the ldloc) was already emitted above
                    }
                }

                if (!heightDone || !rowDone)
                {
                    Log.Warning($"[Days of Food] config-section transpiler: pattern mismatch (height={heightDone}, row={rowDone}); auto rows disabled.");
                    return code;
                }
                return output;
            }
            catch (Exception e)
            {
                Log.Error($"[Days of Food] config-section transpiler failed; auto rows disabled.\n{e}");
                return code;
            }
        }
    }
}
