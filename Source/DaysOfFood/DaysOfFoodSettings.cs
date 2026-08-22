using UnityEngine;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// Mod options. Currently a single toggle: whether prisoners of the colony are counted into the
    /// daily nutrition need that every auto-maintained target is computed from. Defaults to on,
    /// matching the behaviour before this setting existed. The value is read live at every
    /// computation (arm a bill / daily refresh), so no restart is needed.
    /// </summary>
    public class DaysOfFoodSettings : ModSettings
    {
        public bool includePrisoners = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref includePrisoners, "includePrisoners", true);
        }

        /// <summary>Draw the options UI. Invoked from <see cref="DaysOfFoodMod.DoSettingsWindowContents"/>.</summary>
        public void DrawOptionsUI(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("DaysOfFood.Settings.IncludePrisoners".Translate(), ref includePrisoners);
            listing.Gap(4f);
            listing.Label("DaysOfFood.Settings.IncludePrisonersTip".Translate());
            listing.End();
        }
    }
}