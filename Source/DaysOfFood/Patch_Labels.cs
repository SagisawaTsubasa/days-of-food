using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// Surface the auto state in the UI:
    ///  1. Postfix on <see cref="Bill_Production.RepeatInfoText"/> prepends a "[Auto 3d]" marker to the
    ///     bills-tab row, so a tracked bill is recognisable at a glance (its target count itself is
    ///     the auto value — vanilla already prints it).
    ///  2. A transpiler swaps every <c>bill.repeatMode.LabelCap</c> read in the repeat-mode BUTTON
    ///     draw sites (vanilla bills-tab row, vanilla details dialog, and — soft dependency — Nice
    ///     Bill Tab's row) for a helper that replaces the vanilla label with the auto marker, so a
    ///     tracked bill's button reads just "[自动3天] / [Auto 3d]".
    /// Pattern adapted from Hauler's Dream (Refzlund).
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

    public static class AutoRepeatButtonLabel
    {
        /// <summary>The repeat-mode label a bill's button should show: vanilla's own
        /// <c>repeatMode.LabelCap</c>, prepended with the auto marker when the bill is tracked.
        /// Defensive on null so a UI transpiler can never turn a cosmetic label into an exception.</summary>
        public static TaggedString Label(Bill_Production bill)
        {
            if (bill?.repeatMode == null)
                return default;
            TaggedString label = bill.repeatMode.LabelCap;
            var comp = AutoFoodGameComponent.Instance;
            // A tracked bill's button shows ONLY the auto marker ("[自动3天]" / "[Auto 3d]"), not the
            // vanilla "维持X个 / Do until you have X" label — the number input already shows the auto value.
            if (comp != null && comp.IsTracked(bill))
                return "DaysOfFood.RowMarker".Translate(comp.DaysOf(bill)).Resolve().Trim();
            return label;
        }

        /// <summary>
        /// Replace every <c>ldfld repeatMode; call(virt) get_LabelCap</c> pair with one call to
        /// <see cref="Label"/> (the bill reference is already on the stack and feeds the helper).
        /// Fail-safe: on no match or any error the original body is returned unchanged.
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var code = new List<CodeInstruction>(instructions);
            try
            {
                var helper = AccessTools.Method(typeof(AutoRepeatButtonLabel), nameof(Label));
                var output = new List<CodeInstruction>(code.Count);
                int wrapped = 0;
                for (int i = 0; i < code.Count; i++)
                {
                    if (i < code.Count - 1
                        && code[i].opcode == OpCodes.Ldfld && code[i].operand is FieldInfo f
                        && f.Name == "repeatMode" && f.DeclaringType != null
                        && typeof(Bill).IsAssignableFrom(f.DeclaringType)
                        && (code[i + 1].opcode == OpCodes.Callvirt || code[i + 1].opcode == OpCodes.Call)
                        && code[i + 1].operand is MethodInfo m && m.Name == "get_LabelCap")
                    {
                        var call = new CodeInstruction(OpCodes.Call, helper);
                        call.labels.AddRange(code[i].labels);
                        call.blocks.AddRange(code[i].blocks);
                        call.labels.AddRange(code[i + 1].labels);
                        call.blocks.AddRange(code[i + 1].blocks);
                        output.Add(call);
                        i++;
                        wrapped++;
                        continue;
                    }
                    output.Add(code[i]);
                }
                if (wrapped == 0)
                {
                    Log.Warning($"[Days of Food] repeat-label transpiler: no repeatMode.LabelCap read found in "
                                + $"{method?.DeclaringType?.Name}.{method?.Name}; the button keeps the plain label.");
                    return code;
                }
                return output;
            }
            catch (Exception e)
            {
                Log.Error($"[Days of Food] repeat-label transpiler failed on "
                          + $"{method?.DeclaringType?.Name}.{method?.Name}; the button keeps the plain label.\n{e}");
                return code;
            }
        }
    }

    /// <summary>Vanilla surfaces: the bills-tab row button and the details dialog button.</summary>
    [HarmonyPatch]
    public static class Patch_RepeatButtonLabel_Vanilla
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Bill_Production), "DoConfigInterface");
            yield return AccessTools.Method(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents));
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            => AutoRepeatButtonLabel.Transpiler(instructions, original);
    }

    /// <summary>Nice Bill Tab's own bill-row rendering. SOFT dependency: skipped entirely when absent.</summary>
    [HarmonyPatch]
    public static class Patch_RepeatButtonLabel_NiceBillTab
    {
        private static MethodBase ResolveTarget()
        {
            var type = GenTypes.GetTypeInAnyAssembly("NiceBillTab.TabBillsDrawer");
            return type == null ? null : AccessTools.Method(type, "DrawBillPreview");
        }

        static bool Prepare() => ResolveTarget() != null;

        static MethodBase TargetMethod() => ResolveTarget();

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            => AutoRepeatButtonLabel.Transpiler(instructions, original);
    }
}
