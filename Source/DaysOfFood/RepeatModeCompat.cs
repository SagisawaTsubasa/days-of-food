using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// Reflection-only compatibility bridges for other mods that add their own bill repeat modes.
    /// Our repeat-mode menu (Patch_RepeatModeMenu) fully replaces vanilla's
    /// <c>BillRepeatModeUtility.MakeConfigFloatMenu</c>, which would hide those mods' entries
    /// (their transpilers live in the original body our prefix skips; a competing full-replace
    /// prefix loses the priority race). Each bridge re-invokes the other mod's OWN inserter so its
    /// entries reappear with that mod's exact labels and guards. Fail-open when the mod is absent.
    /// Pattern adapted from Hauler's Dream (Refzlund).
    /// </summary>
    public static class RepeatModeCompat
    {
        // "Everybody Gets One - Continued": transpiler hooks the list ctor and calls InsertMode(list, bill).
        private static MethodInfo egoInsertMode;

        // "Compositable Loadouts": transpiler splices a call to GetOptions(list, bill).
        private static MethodInfo clGetOptions;

        // "Ingredient Threshold": competing full-replace prefix; no reusable inserter — re-add its def entry.
        private static FieldInfo itModeField;

        private static bool initialized;

        private static void Init()
        {
            initialized = true;

            var egoType = AccessTools.TypeByName("Everybody_Gets_One.MakeConfigFloatMenu_Patch");
            if (egoType != null)
            {
                egoInsertMode = AccessTools.Method(egoType, "InsertMode",
                    new[] { typeof(List<FloatMenuOption>), typeof(Bill_Production) });
                if (egoInsertMode != null)
                    Log.Message("[Days of Food] Everybody Gets One detected — its repeat modes are surfaced in the repeat-mode menu.");
            }

            var clType = AccessTools.TypeByName("Inventory.MakeConfigFloatMenu_Patch");
            if (clType != null)
            {
                clGetOptions = AccessTools.Method(clType, "GetOptions",
                    new[] { typeof(List<FloatMenuOption>), typeof(Bill_Production) });
                if (clGetOptions != null)
                    Log.Message("[Days of Food] Compositable Loadouts detected — its repeat mode is surfaced in the repeat-mode menu.");
            }

            var itType = AccessTools.TypeByName("IngredientThreshold.ThresholdRepeatModeDef");
            if (itType != null)
            {
                itModeField = AccessTools.Field(itType, "IngredientThreshold");
                if (itModeField != null)
                    Log.Message("[Days of Food] Ingredient Threshold detected — its repeat mode is surfaced in the repeat-mode menu.");
            }
        }

        /// <summary>Append every detected compat mod's repeat-mode entries to the menu.</summary>
        public static void TryInsertModes(List<FloatMenuOption> options, Bill_Production bill)
        {
            if (!initialized)
                Init();
            if (options == null || bill == null)
                return;

            egoInsertMode?.Invoke(null, new object[] { options, bill });
            clGetOptions?.Invoke(null, new object[] { options, bill });

            if (itModeField?.GetValue(null) is BillRepeatModeDef mode)
                options.Add(new FloatMenuOption(mode.LabelCap, () => bill.repeatMode = mode));
        }
    }
}
