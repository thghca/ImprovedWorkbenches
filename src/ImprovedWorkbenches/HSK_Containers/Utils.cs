using RimWorld;
using System.Collections.Generic;
using Verse;

namespace ImprovedWorkbenches.HSK_Containers
{
    public static class Utils
    {
        //      Here something BillStoreMode related:
        //      +RimWorld.Bill_Production.SetStoreMode(BillStoreModeDef, Zone_Stockpile)
        //		+RimWorld.Bill_Production.ValidateSettings() : void @060025B5
        //		+RimWorld.Dialog_BillConfig.DoWindowContents(Rect) : void @060025D2
        //          +check after bill thing filter change
        //		+Verse.AI.Toils_Recipe.FinishRecipeAndStartStoringProduct() : Toil @06003B93
        //		RimWorld.ITab_Storage.FillTab() : void @0600319B
        //          +check after building thing filter change
        //		+RimWorld.Dialog_BillConfig.DoWindowContents(Rect) : void @060025D2
        //      +clean storeBuilding when other storemode selected;
        // 
        //      Here something includeFromZone related:
        //      +RimWorld.Bill_Production.ValidateSettings() : void @060025B5
        //      +RimWorld.Dialog_BillConfig.GenerateStockpileInclusion() : IEnumerable<Widgets.DropdownMenuElement<Zone_Stockpile>> @060025D4
        //      +RimWorld.Dialog_BillConfig.DoWindowContents(Rect) : void @060025D2
        //      +Verse.RecipeWorkerCounter.CountProducts(Bill_Production) : int @06004064
        //      ImprovedWorkbenches:            
        //          +RecipeWorkerCounter_CountProducts_Detour
        //          +CountAdditionalProducts
        //      When includeFromBuilding is not null includeFromZone should be null
        //      When includeFromZone is not null includeFromBuilding should be null
        //
        //      +Strings translation (EN/RU)
        //      +validation after despawn container

        public static ExtendedBillData GetExtendedData(this Bill_Production bill)
        {
           var extendedDataStorage = HugsLib.Utils.UtilityWorldObjectManager.GetUtilityWorldObject<ExtendedBillDataStorage>();
           return extendedDataStorage.GetOrCreateExtendedDataFor(bill);
        }

        public static Building_Storage GetStoreBuilding(this Bill_Production bill)
        {
            return bill.GetExtendedData().storeBuilding;
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

        public static Building_Storage GetIncludeFromBuilding(this Bill_Production bill)
        {
            return bill.GetExtendedData().includeFromBuilding;
        }

        public static void SetIncludeFromBuilding(this Bill_Production bill, Building_Storage includeFromBuilding)
        {
            var extendedData = bill.GetExtendedData();
            extendedData.includeFromBuilding = includeFromBuilding;

            if (includeFromBuilding != null)
            {
                bill.includeFromZone = null;
            }
        }

        public static IEnumerable<Thing> AllContainedThings(this Building_Storage building_Storage)
        {
            ThingGrid grids = building_Storage.Map.thingGrid;
            var cells = building_Storage.AllSlotCellsList();
            foreach (var cell in cells)
            {
                List<Thing> thingList = grids.ThingsListAt(cell);
                foreach(var thing in thingList)
                {
                    if (thing != building_Storage && thing.def.category == ThingCategory.Item)
                        yield return thing;
                }
            }
        }
    }
}
