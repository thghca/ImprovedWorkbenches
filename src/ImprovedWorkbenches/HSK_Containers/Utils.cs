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

        public static bool CanPossiblyStoreInBuilding_Storage(this RecipeWorkerCounter recipeWorkerCounter, Bill_Production bill, Building_Storage building_Storage)
        {
            return !recipeWorkerCounter.CanCountProducts(bill) || building_Storage.GetStoreSettings().AllowedToAccept(recipeWorkerCounter.recipe.products[0].thingDef);
        }

    }
}
