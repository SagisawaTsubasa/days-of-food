using RimWorld;
using UnityEngine;
using Verse;

namespace DaysOfFood
{
    /// <summary>
    /// Pure nutrition math. A pawn's daily food need is read from
    /// <see cref="Need_Food.FoodFallPerTickAssumingCategory"/> — that value already includes the pawn's
    /// HungerRateMultiplier stat, so gut worms / stomach infections and any other hunger-rate effect
    /// are accounted for with no special-casing.
    /// </summary>
    public static class NutritionCalc
    {
        /// <summary>
        /// The nutrition a single produced item gives, for recipes whose product is edible food.
        /// False when the recipe makes no edible product — those bills never get the auto modes.
        /// </summary>
        public static bool TryGetFoodNutritionPerItem(RecipeDef recipe, out float nutritionPerItem)
        {
            nutritionPerItem = 0f;
            var products = recipe?.products;
            if (products == null)
                return false;
            for (int i = 0; i < products.Count; i++)
            {
                var def = products[i].thingDef;
                if (def == null || !def.IsIngestible)
                    continue;
                float n = def.GetStatValueAbstract(StatDefOf.Nutrition);
                if (n > 0f)
                {
                    nutritionPerItem = n;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Total daily nutrition need of everyone on this map who eats: free colonists (slaves are
        /// colonists) plus the colony's prisoners. Visitors, hostiles and non-eaters are excluded.
        /// </summary>
        public static float DailyNutritionNeed(Map map)
        {
            float total = 0f;
            var pawns = map.mapPawns.FreeColonistsAndPrisoners;
            for (int i = 0; i < pawns.Count; i++)
            {
                var food = pawns[i].needs?.food;
                if (food == null)
                    continue;
                total += food.FoodFallPerTickAssumingCategory(HungerCategory.Fed) * 60000f;
            }
            return total;
        }

        /// <summary>How many items keep the map fed for <paramref name="days"/> days.</summary>
        public static int ComputeTarget(float dailyNeed, int days, float nutritionPerItem)
        {
            if (nutritionPerItem <= 0f)
                return 0;
            return Mathf.Max(1, Mathf.CeilToInt(dailyNeed * days / nutritionPerItem));
        }

        /// <summary>
        /// One-shot equivalent for "做X次 / RepeatCount" bills: how many CRAFTS cover
        /// <paramref name="days"/> days of food. A craft yields the product count of the food product
        /// (e.g. ×4 bulk recipes), so the per-craft nutrition is perItem × itemsPerCraft.
        /// </summary>
        public static int ComputeRepeatCount(Bill_Production bill, int days)
        {
            var map = bill?.Map;
            if (map == null || !TryGetFoodNutritionPerItem(bill.recipe, out float perItem))
                return bill?.repeatCount ?? 1;
            int itemsPerCraft = 1;
            var products = bill.recipe.products;
            if (products != null)
            {
                for (int i = 0; i < products.Count; i++)
                {
                    if (products[i].thingDef != null && products[i].thingDef.IsIngestible
                        && products[i].thingDef.GetStatValueAbstract(StatDefOf.Nutrition) > 0f)
                    {
                        itemsPerCraft = Mathf.Max(1, products[i].count);
                        break;
                    }
                }
            }
            float perCraft = perItem * itemsPerCraft;
            return Mathf.Max(1, Mathf.CeilToInt(DailyNutritionNeed(map) * days / perCraft));
        }

        /// <summary>Recompute and write the bill's target count immediately (used when a mode is picked).</summary>
        public static void ApplyTarget(Bill_Production bill, int days)
        {
            var map = bill?.Map;
            if (map == null || !TryGetFoodNutritionPerItem(bill.recipe, out float perItem))
                return;
            bill.targetCount = ComputeTarget(DailyNutritionNeed(map), days, perItem);
        }
    }
}
