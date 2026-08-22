using HarmonyLib;
using UnityEngine;
using Verse;

namespace DaysOfFood
{
    /// <summary>Mod entry: load settings, apply all Harmony patches.</summary>
    public class DaysOfFoodMod : Mod
    {
        /// <summary>Loaded mod options (see <see cref="DaysOfFoodSettings"/>). Never null once the mod is constructed.</summary>
        public static DaysOfFoodSettings Settings;

        public DaysOfFoodMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DaysOfFoodSettings>();
            new Harmony("lfore.daysoffood").PatchAll();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DrawOptionsUI(inRect);
        }

        public override string SettingsCategory()
        {
            return "Days of Food";
        }
    }
}
