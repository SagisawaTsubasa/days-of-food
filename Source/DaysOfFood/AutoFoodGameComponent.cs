using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// The per-save state: which bills are auto-tracked and for how many days, plus the once-per-day
    /// refresh. Keyed by bill loadID (string) so it survives save/load; a bill that vanished (bench
    /// deconstructed, bill deleted, mode switched away from TargetCount by any means — including a
    /// compat mod's own menu entry) is dropped during the daily sweep.
    /// </summary>
    public class AutoFoodGameComponent : GameComponent
    {
        public static AutoFoodGameComponent Instance;

        /// <summary>bill loadID -> days to keep in stock (auto-maintain).</summary>
        private Dictionary<string, int> autoDays = new Dictionary<string, int>();

        /// <summary>bill loadID -> days of stock left that resumes production, or absent/0 for none.
        /// When set, the bill's vanilla pauseWhenSatisfied/unpauseWhenYouHave are driven by the mod.</summary>
        private Dictionary<string, int> autoPauseDays = new Dictionary<string, int>();

        private int ticksUntilRefresh = 0;

        private const int RefreshInterval = 60000; // 1 game day

        public AutoFoodGameComponent(Game game)
        {
            Instance = this;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref autoDays, "autoDays", LookMode.Value, LookMode.Value);
            if (autoDays == null)
                autoDays = new Dictionary<string, int>();
            // Absent in saves made before auto-pause existed -> empty, i.e. no auto-pause.
            Scribe_Collections.Look(ref autoPauseDays, "autoPauseDays", LookMode.Value, LookMode.Value);
            if (autoPauseDays == null)
                autoPauseDays = new Dictionary<string, int>();
        }

        /// <summary>Days this bill is tracked for, or 0 when not auto-tracked. Mode-aware: auto
        /// tracking only lives on TargetCount, so a bill switched to any other mode is untracked
        /// IMMEDIATELY (lazily clearing the entry) rather than waiting for the daily sweep — this
        /// is what clears the "[自动N天]" row marker the moment the player picks
        /// "做X次" or "无限制".</summary>
        public int DaysOf(Bill bill)
        {
            if (bill == null)
                return 0;
            if (bill is Bill_Production prod && prod.repeatMode != BillRepeatModeDefOf.TargetCount)
            {
                RemoveEntry(bill);
                return 0;
            }
            return autoDays.TryGetValue(bill.GetUniqueLoadID(), out int d) ? d : 0;
        }

        /// <summary>Days of stock left that resumes production (0 = no auto-pause). Only meaningful
        /// while <see cref="DaysOf"/> &gt; 0, and clamped to at most DaysOf - 1.</summary>
        public int PauseDaysOf(Bill bill)
        {
            if (bill == null || !(bill is Bill_Production prod) || prod.repeatMode != BillRepeatModeDefOf.TargetCount)
                return 0;
            return autoPauseDays.TryGetValue(bill.GetUniqueLoadID(), out int d) ? d : 0;
        }

        public bool IsTracked(Bill bill) => DaysOf(bill) > 0;

        /// <summary>Start (or re-arm) auto tracking for a bill, and apply the computed target now.
        /// <paramref name="pauseDays"/> (0 = none) is clamped into [0, days-1]; turning auto-pause
        /// OFF after it was ON releases the bill's vanilla pauseWhenSatisfied control.</summary>
        public void SetDays(Bill_Production bill, int days, int pauseDays = 0)
        {
            if (bill == null || days <= 0)
                return;
            var id = bill.GetUniqueLoadID();
            bool wasPaused = autoPauseDays.TryGetValue(id, out int oldPause) && oldPause > 0;
            autoDays[id] = days;
            int clamped = Mathf.Clamp(pauseDays, 0, days - 1);
            if (clamped > 0)
                autoPauseDays[id] = clamped;
            else
                autoPauseDays.Remove(id);
            if (wasPaused && clamped == 0)
            {
                bill.pauseWhenSatisfied = false;
                // Vanilla's pause check is skipped entirely when pauseWhenSatisfied is false, so a
                // bill that was already paused would stay paused forever — release it explicitly.
                bill.paused = false;
            }
            NutritionCalc.ApplyTarget(bill, days, clamped);
        }

        public void Clear(Bill bill)
        {
            if (bill != null)
                RemoveEntry(bill);
        }

        private void RemoveEntry(Bill bill)
        {
            var id = bill.GetUniqueLoadID();
            autoDays.Remove(id);
            autoPauseDays.Remove(id);
        }

        public override void GameComponentTick()
        {
            if (--ticksUntilRefresh > 0)
                return;
            ticksUntilRefresh = RefreshInterval;
            DailyRefresh();
        }

        /// <summary>
        /// Once a day: recompute every tracked bill's target from its OWN map's current eaters
        /// (no cross-map sharing), and prune entries whose bill no longer qualifies.
        /// </summary>
        private void DailyRefresh()
        {
            if (autoDays.Count == 0)
                return;
            var maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var map = maps[m];
                if (!map.IsPlayerHome)
                    continue;
                float dailyNeed = -1f; // computed lazily, only if this map has a tracked bill
                var buildings = map.listerBuildings.allBuildingsColonist;
                for (int b = 0; b < buildings.Count; b++)
                {
                    if (!(buildings[b] is IBillGiver giver))
                        continue;
                    var bills = giver.BillStack?.Bills;
                    if (bills == null)
                        continue;
                    for (int i = 0; i < bills.Count; i++)
                    {
                        if (!(bills[i] is Bill_Production bill))
                            continue;
                        int days = DaysOf(bill);
                        if (days <= 0)
                            continue;
                        // Switched away from TargetCount by any path (vanilla entry, compat mode) -> untrack.
                        if (bill.repeatMode != BillRepeatModeDefOf.TargetCount
                            || !NutritionCalc.TryGetFoodNutritionPerItem(bill.recipe, out float perItem))
                        {
                            Clear(bill);
                            continue;
                        }
                        if (dailyNeed < 0f)
                            dailyNeed = NutritionCalc.DailyNutritionNeed(map);
                        bill.targetCount = NutritionCalc.ComputeTarget(dailyNeed, days, perItem);
                        int pauseDays = PauseDaysOf(bill);
                        if (pauseDays > 0)
                        {
                            bill.pauseWhenSatisfied = true;
                            bill.unpauseWhenYouHave = NutritionCalc.ComputeTarget(dailyNeed, pauseDays, perItem);
                        }
                    }
                }
            }
            // Prune tracked ids that no longer belong to any live bill (bench deconstructed / bill deleted).
            if (autoDays.Count == 0)
                return;
            var live = new HashSet<string>();
            for (int m = 0; m < maps.Count; m++)
            {
                var bills = maps[m].listerBuildings.allBuildingsColonist;
                for (int b = 0; b < bills.Count; b++)
                {
                    if (!(bills[b] is IBillGiver giver) || giver.BillStack == null)
                        continue;
                    for (int i = 0; i < giver.BillStack.Bills.Count; i++)
                        live.Add(giver.BillStack.Bills[i].GetUniqueLoadID());
                }
            }
            staleBuffer.Clear();
            foreach (var kv in autoDays)
                if (!live.Contains(kv.Key))
                    staleBuffer.Add(kv.Key);
            for (int i = 0; i < staleBuffer.Count; i++)
            {
                autoDays.Remove(staleBuffer[i]);
                autoPauseDays.Remove(staleBuffer[i]);
            }
        }

        private readonly List<string> staleBuffer = new List<string>();
    }
}
