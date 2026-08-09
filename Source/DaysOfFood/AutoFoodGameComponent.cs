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

        /// <summary>bill loadID -> days (1/3/5).</summary>
        private Dictionary<string, int> autoDays = new Dictionary<string, int>();

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
        }

        /// <summary>Days this bill is tracked for, or 0 when not auto-tracked. Mode-aware: auto
        /// tracking only lives on TargetCount, so a bill switched to any other mode is untracked
        /// IMMEDIATELY (lazily clearing the entry) rather than waiting for the daily sweep — this
        /// is what clears the "[自动N天]" row marker and button label the moment the player picks
        /// "做X次" or "无限制".</summary>
        public int DaysOf(Bill bill)
        {
            if (bill == null)
                return 0;
            if (bill is Bill_Production prod && prod.repeatMode != BillRepeatModeDefOf.TargetCount)
            {
                autoDays.Remove(bill.GetUniqueLoadID());
                return 0;
            }
            return autoDays.TryGetValue(bill.GetUniqueLoadID(), out int d) ? d : 0;
        }

        public bool IsTracked(Bill bill) => DaysOf(bill) > 0;

        /// <summary>Start (or re-arm) auto tracking for a bill, and apply the computed target now.</summary>
        public void SetDays(Bill_Production bill, int days)
        {
            if (bill == null || days <= 0)
                return;
            autoDays[bill.GetUniqueLoadID()] = days;
            NutritionCalc.ApplyTarget(bill, days);
        }

        public void Clear(Bill bill)
        {
            if (bill != null)
                autoDays.Remove(bill.GetUniqueLoadID());
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
                autoDays.Remove(staleBuffer[i]);
        }

        private readonly List<string> staleBuffer = new List<string>();
    }
}
