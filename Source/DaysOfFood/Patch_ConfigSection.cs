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
    ///  - TargetCount ("维持X个"): an "自动维持数量" checkbox plus a vanilla IntEntry for the day
    ///    count (any positive integer) that arms daily auto-refresh of the target count.
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
        // Section heights for the mod's rows (checkbox 22, label ~26, IntEntry 30, +slack), matching
        // exactly what DrawAutoRow draws so Listing.BeginSection never clips. See AdjustHeight.
        private const int HeightCheckboxOnly = 28;
        private const int HeightMaintain = 84; // checkbox + maintain label + maintain IntEntry
        private const int HeightPauseCheckbox = 106; // + pause checkbox row
        private const int HeightPauseFull = 162; // + pause label + pause IntEntry

        // Per-dialog IntEntry edit buffers. Dialog_BillConfig is modal — at most one is open at a
        // time — so single static buffers are safe, mirroring vanilla's own *EditBuffer fields.
        // Note: unlike vanilla's (instance) buffers, these SURVIVE across dialog instances, so
        // DrawAutoRow resyncs them against the bill's real values before drawing (see SyncEditBuffer).
        private static string daysEditBuffer;
        private static string pauseEditBuffer;

        // NB: RepeatModeSubdialogHeight is an int field — this signature must take/return int,
        // an int-on-stack call to a float method is invalid IL.
        public static int AdjustHeight(int height, Bill_Production bill)
        {
            if (bill == null || bill.repeatMode != BillRepeatModeDefOf.TargetCount
                || !NutritionCalc.TryGetFoodNutritionPerItem(bill.recipe, out _))
                return height;
            var comp = AutoFoodGameComponent.Instance;
            if (comp == null)
                return height; // mod not loaded in this game; DrawAutoRow won't draw either
            if (!comp.IsTracked(bill))
                return height + HeightCheckboxOnly;
            if (comp.DaysOf(bill) <= 1)
                return height + HeightMaintain;
            return comp.PauseDaysOf(bill) > 0 ? height + HeightPauseFull : height + HeightPauseCheckbox;
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
                SyncEditBuffer(ref daysEditBuffer, days);
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
                    comp.SetDays(bill, edited, comp.PauseDaysOf(bill));

                // Auto-pause row: pause production when the maintained target is met, resume when
                // the remaining stock drops below the pause-days target. Only reachable for days > 1.
                if (days > 1)
                {
                    int pauseDays = comp.PauseDaysOf(bill);
                    bool pauseToggled = pauseDays > 0;
                    var pauseRect = listing.GetRect(22f);
                    Widgets.CheckboxLabeled(pauseRect, "DaysOfFood.Section.PauseLabel".Translate(), ref pauseToggled);
                    TooltipHandler.TipRegion(pauseRect, "DaysOfFood.Section.PauseTip".Translate());
                    if (pauseToggled != (pauseDays > 0))
                    {
                        // Enabling defaults to half the maintained days; disabling releases the
                        // vanilla control via SetDays.
                        comp.SetDays(bill, days, pauseToggled ? Mathf.Max(1, days / 2) : 0);
                    }
                    pauseDays = comp.PauseDaysOf(bill);
                    if (pauseDays > 0)
                    {
                        SyncEditBuffer(ref pauseEditBuffer, pauseDays);
                        listing.Label("DaysOfFood.Section.PauseDaysLabel".Translate(pauseDays));
                        int pEdited = pauseDays;
                        listing.IntEntry(ref pEdited, ref pauseEditBuffer);
                        if (pEdited < 1)
                        {
                            pEdited = 1;
                            pauseEditBuffer = pEdited.ToStringCached();
                        }
                        if (pEdited >= days)
                        {
                            // The pause threshold must stay below the maintained days, otherwise the
                            // vanilla resume check would fire immediately after every pause.
                            pEdited = Mathf.Max(1, days - 1);
                            pauseEditBuffer = pEdited.ToStringCached();
                        }
                        if (pEdited != pauseDays)
                            comp.SetDays(bill, days, pEdited);
                    }
                }
            }
        }

        /// <summary>Shared edit buffers survive across dialog instances (this method is static), so
        /// a stale buffer from a previously-opened bill can disagree with the value actually in
        /// effect on this one. Resync only when the buffer holds a full number different from the
        /// real value — an in-progress edit (empty or partial text like "-") is left alone.</summary>
        private static void SyncEditBuffer(ref string buffer, int realValue)
        {
            if (int.TryParse(buffer, out int bufVal) && bufVal != realValue)
                buffer = realValue.ToStringCached();
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
