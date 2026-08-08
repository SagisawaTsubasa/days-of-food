using HarmonyLib;
using Verse;

namespace DaysOfFood
{
    /// <summary>Mod entry: apply all Harmony patches.</summary>
    public class DaysOfFoodMod : Mod
    {
        public DaysOfFoodMod(ModContentPack content) : base(content)
        {
            new Harmony("lfore.daysoffood").PatchAll();
        }
    }
}
