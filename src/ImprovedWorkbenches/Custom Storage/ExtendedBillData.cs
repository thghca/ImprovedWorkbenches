﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Harmony;

namespace ImprovedWorkbenches
{
    public class ExtendedBillData : IExposable
    {
        public bool CountAway;
        public string Name;
        public ThingFilter ProductAdditionalFilter;

        //HSK_Containers
        public Building_Storage storeBuilding;
        public Building_Storage includeFromBuilding;

        public ExtendedBillData()
        {
        }

        public void CloneFrom(ExtendedBillData other, bool cloneName)
        {
            CountAway = other.CountAway;
            ProductAdditionalFilter = new ThingFilter();
            if(other.ProductAdditionalFilter != null)
                ProductAdditionalFilter.CopyAllowancesFrom(other.ProductAdditionalFilter);

            if (cloneName)
                Name = other.Name;

            //HSK_Containers
            storeBuilding = other.storeBuilding;
            includeFromBuilding = other.includeFromBuilding;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref CountAway, "countAway", false);
            Scribe_Values.Look(ref Name, "name", null);
            Scribe_Deep.Look(ref ProductAdditionalFilter, "productFilter");

            ////HSK_Containers
            Scribe_References.Look<Building_Storage>(ref storeBuilding, "storeBuilding");
            Scribe_References.Look<Building_Storage>(ref includeFromBuilding, "includeFromBuilding");
        }
    }


    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ExposeData))]
    public static class ExtendedBillData_ExposeData
    {
        public static void Postfix(Bill_Production __instance)
        {
            var storage = HugsLib.Utils.UtilityWorldObjectManager.GetUtilityWorldObject<ExtendedBillDataStorage>();
            storage.GetOrCreateExtendedDataFor(__instance).ExposeData();
        }
    }


    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Clone))]
    public static class ExtendedBillData_Clone
    {
        public static void Postfix(Bill_Production __instance, Bill_Production __result)
        {
            var storage = Main.Instance.GetExtendedBillDataStorage();
            var sourceExtendedData = storage.GetExtendedDataFor(__instance);
            var destinationExtendedData = storage.GetOrCreateExtendedDataFor(__result);
            
            destinationExtendedData?.CloneFrom(sourceExtendedData, true);
        }
    }

}