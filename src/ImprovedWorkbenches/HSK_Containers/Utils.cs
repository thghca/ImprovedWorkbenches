using RimWorld;
using Verse;

namespace ImprovedWorkbenches.HSK_Containers
{
    public static class Utils
    {
        //      Here something BillStoreMode related:
        //      +RimWorld.Bill_Production.SetStoreMode(BillStoreModeDef, Zone_Stockpile)
        //		+RimWorld.Bill_Production.ValidateSettings() : void @060025B5
        //		+RimWorld.Dialog_BillConfig.DoWindowContents(Rect) : void @060025D2
        //		+Verse.AI.Toils_Recipe.FinishRecipeAndStartStoringProduct() : Toil @06003B93
        //		?RimWorld.ITab_Storage.FillTab() : void @0600319B - wtf?
        //		+RimWorld.Dialog_BillConfig.DoWindowContents(Rect) : void @060025D2
        //
        //      +clean storeBuilding when other storemode selected;
        //      -Strings translation
        //      +validation after despawn container

        public static ExtendedBillData GetExtendedData(this Bill_Production bill)
        {
           var extendedDataStorage = HugsLib.Utils.UtilityWorldObjectManager.GetUtilityWorldObject<ExtendedBillDataStorage>();
           return extendedDataStorage.GetOrCreateExtendedDataFor(bill);
        }

        public static  void SetSpecificBuildingStoreMode(this Bill_Production bill, Building_Storage storeBuilding)
        {
            
            var extendedData = bill.GetExtendedData();
            extendedData.storeBuilding = storeBuilding;

            bill.SetStoreMode(BillStoreModeDefOf.SpecificBuilding_Storage);

            if (storeBuilding == null)
            {
                Log.Error("storeBuilding is null!");
            }
        }

        //CanPossiblyStoreInStockpile but with Building_Storage
        public static bool CanPossiblyStoreInBuilding_Storage(this RecipeWorkerCounter recipeWorkerCounter, Bill_Production bill, Building_Storage building_Storage)
        {
            
            //butcher animal
            if (recipeWorkerCounter.GetType() == typeof(RecipeWorkerCounter_ButcherAnimals) || recipeWorkerCounter.GetType().IsSubclassOf(typeof(RecipeWorkerCounter_ButcherAnimals)))
            {
                foreach (ThingDef thingDef in bill.ingredientFilter.AllowedThingDefs)
                {
                    if (thingDef.ingestible != null && thingDef.ingestible.sourceDef != null)
                    {
                        RaceProperties race = thingDef.ingestible.sourceDef.race;
                        if (race != null && race.meatDef != null)
                        {
                            if (!building_Storage.GetStoreSettings().AllowedToAccept(race.meatDef))
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            //make stone blocks
            if (recipeWorkerCounter.GetType() == typeof(RecipeWorkerCounter_MakeStoneBlocks) || recipeWorkerCounter.GetType().IsSubclassOf(typeof(RecipeWorkerCounter_MakeStoneBlocks)))
            {
                foreach (ThingDef thingDef in bill.ingredientFilter.AllowedThingDefs)
                {
                    if (!thingDef.butcherProducts.NullOrEmpty<ThingDefCountClass>())
                    {
                        ThingDef thingDef2 = thingDef.butcherProducts[0].thingDef;
                        if (!building_Storage.GetStoreSettings().AllowedToAccept(thingDef2))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            //normal
            if (recipeWorkerCounter.GetType() == typeof(RecipeWorkerCounter))
            {
                return !recipeWorkerCounter.CanCountProducts(bill) || building_Storage.GetStoreSettings().AllowedToAccept(recipeWorkerCounter.recipe.products[0].thingDef);
            }
            Log.Error("Unknown recipe type");
            return false;
        }

        public static void Notify_Building_StorageRemoved(Building_Storage building_Storage)
        {
            foreach (Bill bill in BillUtility.GlobalBills())
            {
                bill.ValidateSettings();
            }
        }
    }
}
