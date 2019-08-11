using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Verse;
using Verse.AI;

namespace ImprovedWorkbenches.HSK_Containers
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private static readonly Type patchType = typeof(HarmonyPatches);
      
        static HarmonyPatches()
        {
            HarmonyInstance harmony = HarmonyInstance.Create(id: "rimworld.thghca.HSK_Containers");

            harmony.Patch(
                original: AccessTools.Method(type: typeof(Bill_Production), name: nameof(Bill_Production.ValidateSettings)),
                postfix: new HarmonyMethod(type: patchType, name: nameof(ValidateSettingsPostfix)));

            harmony.Patch(
                original: AccessTools.Method(type: typeof(Bill_Production), name: nameof(Bill_Production.SetStoreMode)),
                postfix: new HarmonyMethod(type: patchType, name: nameof(SetStoreModePostfix)));

            harmony.Patch(
                original: AccessTools.Method(type: typeof(Toils_Recipe), name: nameof(Toils_Recipe.FinishRecipeAndStartStoringProduct)),
                prefix: new HarmonyMethod(type: patchType, name: nameof(FinishRecipeAndStartStoringProductPrefix)));

            harmony.Patch(
                original: AccessTools.Method(type: typeof(Dialog_BillConfig), name: nameof(Dialog_BillConfig.DoWindowContents)),
                prefix: new HarmonyMethod(type: patchType, name: nameof(Dialog_BillConfig_DoWindowContents_Prefix)), 
                postfix: new HarmonyMethod(type: patchType, name: nameof(Dialog_BillConfig_DoWindowContents_Postfix)),
                transpiler: new HarmonyMethod(type: patchType, name: nameof(Dialog_BillConfig_DoWindowContents_Transpiler)));

            harmony.Patch(
                original: AccessTools.Method(type: typeof(Thing), name: nameof(Thing.DeSpawn)),
                postfix: new HarmonyMethod(type: patchType, name: nameof(Thing_DeSpawnPostfix)));

            harmony.Patch(
                original: AccessTools.Method(type: typeof(ITab_Storage), name: "FillTab"),
                prefix: new HarmonyMethod(type: patchType, name: nameof(ITab_Storage_FillTab_Prefix)),
                postfix: new HarmonyMethod(type: patchType, name: nameof(ITab_Storage_FillTab_Postfix)));
        }

        public static void ValidateSettingsPostfix(Bill_Production __instance, BillStoreModeDef ___storeMode)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                //Not all things loaded here. Container may not exist yet.
                return;
            }

            var storeBuilding = __instance.GetStoreBuilding();

            if (storeBuilding != null)
            {
                if (storeBuilding.Destroyed)
                {
                    if (__instance != BillUtility.Clipboard)
                    {
                        Messages.Message("MessageBillValidationStoreBuildingDeleted".Translate(__instance.LabelCap, __instance.billStack.billGiver.LabelShort.CapitalizeFirst(), storeBuilding.LabelCapNoCount), __instance.billStack.billGiver as Thing, MessageTypeDefOf.NegativeEvent, true);
                    }
                    __instance.SetStoreMode(RimWorld.BillStoreModeDefOf.DropOnFloor, null);
                }
                else if (__instance.Map != null && (__instance.Map != storeBuilding.Map))
                {
                    if (__instance != BillUtility.Clipboard)
                    {
                        Messages.Message("MessageBillValidationStoreBuildingUnavailable".Translate(__instance.LabelCap, __instance.billStack.billGiver.LabelShort.CapitalizeFirst(), storeBuilding.LabelCapNoCount), __instance.billStack.billGiver as Thing, MessageTypeDefOf.NegativeEvent, true);
                    }
                    __instance.SetStoreMode(RimWorld.BillStoreModeDefOf.DropOnFloor, null);
                }
            }
            else if (___storeMode == BillStoreModeDefOf.SpecificBuilding_Storage)
            {
                __instance.SetStoreMode(RimWorld.BillStoreModeDefOf.DropOnFloor, null);
                Log.Error("Found SpecificBuilding_Storage bill store mode without associated Building_Storage, recovering", false);
            }
        }

        public static void SetStoreModePostfix(Bill_Production __instance, BillStoreModeDef ___storeMode)
        {
            if (___storeMode != BillStoreModeDefOf.SpecificBuilding_Storage)
            {
                var extendedData = __instance.GetExtendedData();
                extendedData.storeBuilding = null;
            }
        }


        //mostly copy of Verse.AI.Toils_Recipe.FinishRecipeAndStartStoringProduct()
        //need recheck arfter RW update
        public static bool FinishRecipeAndStartStoringProductPrefix(ref Toil __result)
        {
            Toil toil = new Toil();
            toil.initAction = delegate ()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                JobDriver_DoBill jobDriver_DoBill = (JobDriver_DoBill)actor.jobs.curDriver;
                if (curJob.RecipeDef.workSkill != null && !curJob.RecipeDef.UsesUnfinishedThing)
                {
                    float xp = (float)jobDriver_DoBill.ticksSpentDoingRecipeWork * 0.1f * curJob.RecipeDef.workSkillLearnFactor;
                    actor.skills.GetSkill(curJob.RecipeDef.workSkill).Learn(xp, false);
                }
                List<Thing> ingredients = Traverse.Create(typeof(Toils_Recipe)).Method("CalculateIngredients",new object[] { curJob, actor }).GetValue<List<Thing>>();
                Thing dominantIngredient = Traverse.Create(typeof(Toils_Recipe)).Method("CalculateDominantIngredient", new object[] { curJob, ingredients }).GetValue<Thing>();
                List <Thing> list = GenRecipe.MakeRecipeProducts(curJob.RecipeDef, actor, ingredients, dominantIngredient, jobDriver_DoBill.BillGiver).ToList<Thing>();
                Traverse.Create(typeof(Toils_Recipe)).Method("ConsumeIngredients", new object[] { ingredients, curJob.RecipeDef, actor.Map }).GetValue();
                curJob.bill.Notify_IterationCompleted(actor, ingredients);
                RecordsUtility.Notify_BillDone(actor, list);
                UnfinishedThing unfinishedThing = curJob.GetTarget(TargetIndex.B).Thing as UnfinishedThing;
                if (curJob.bill.recipe.WorkAmountTotal((unfinishedThing == null) ? null : unfinishedThing.Stuff) >= 10000f && list.Count > 0)
                {
                    TaleRecorder.RecordTale(TaleDefOf.CompletedLongCraftingProject, new object[]
                    {
                        actor,
                        list[0].GetInnerIfMinified().def
                    });
                }
                if (list.Count == 0)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded, true);
                    return;
                }
                if (curJob.bill.GetStoreMode() == RimWorld.BillStoreModeDefOf.DropOnFloor)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!GenPlace.TryPlaceThing(list[i], actor.Position, actor.Map, ThingPlaceMode.Near, null, null))
                        {
                            Log.Error(string.Concat(new object[]
                            {
                                actor,
                                " could not drop recipe product ",
                                list[i],
                                " near ",
                                actor.Position
                            }), false);
                        }
                    }
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded, true);
                    return;
                }
                if (list.Count > 1)
                {
                    for (int j = 1; j < list.Count; j++)
                    {
                        if (!GenPlace.TryPlaceThing(list[j], actor.Position, actor.Map, ThingPlaceMode.Near, null, null))
                        {
                            Log.Error(string.Concat(new object[]
                            {
                                actor,
                                " could not drop recipe product ",
                                list[j],
                                " near ",
                                actor.Position
                            }), false);
                        }
                    }
                }
                IntVec3 invalid = IntVec3.Invalid;
                if (curJob.bill.GetStoreMode() == RimWorld.BillStoreModeDefOf.BestStockpile)
                {
                    StoreUtility.TryFindBestBetterStoreCellFor(list[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, out invalid, true);
                }
                else if (curJob.bill.GetStoreMode() == RimWorld.BillStoreModeDefOf.SpecificStockpile)
                {
                    StoreUtility.TryFindBestBetterStoreCellForIn(list[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, curJob.bill.GetStoreZone().slotGroup, out invalid, true);
                }
                else
                //---start
                if(curJob.bill.GetStoreMode() == BillStoreModeDefOf.SpecificBuilding_Storage)
                {
                    StoreUtility.TryFindBestBetterStoreCellForIn(list[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, ((Bill_Production)(curJob.bill)).GetStoreBuilding().slotGroup, out invalid, true);
                }
                else
                //---end
                {
                    Log.ErrorOnce("Unknown store mode", 9158246, false);
                }
                if (invalid.IsValid)
                {
                    actor.carryTracker.TryStartCarry(list[0]);
                    curJob.targetB = invalid;
                    curJob.targetA = list[0];
                    curJob.count = 99999;
                    return;
                }
                if (!GenPlace.TryPlaceThing(list[0], actor.Position, actor.Map, ThingPlaceMode.Near, null, null))
                {
                    Log.Error(string.Concat(new object[]
                    {
                        "Bill doer could not drop product ",
                        list[0],
                        " near ",
                        actor.Position
                    }), false);
                }
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, true);
            };
            __result = toil;

            return false;
        }

        #region Dialog_BillConfig_DoWindowContents

        public static void Dialog_BillConfig_DoWindowContents_Prefix(Dialog_BillConfig __instance, ref bool __state)
        {
            var bill = Traverse.Create(__instance).Field("bill").GetValue<Bill_Production>();
            var storeBuilding = bill.GetStoreBuilding();

            __state = storeBuilding == null || bill.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage(bill, storeBuilding);
        }

        public static void Dialog_BillConfig_DoWindowContents_Postfix(Dialog_BillConfig __instance, ref bool __state)
        {
            var bill = Traverse.Create(__instance).Field("bill").GetValue<Bill_Production>();
            var storeBuilding = bill.GetStoreBuilding();

            if (__state && !(storeBuilding == null || bill.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage(bill, storeBuilding)))
            {
                Messages.Message("MessageBillValidationStoreBuildingInsufficient".Translate(bill.LabelCap, bill.billStack.billGiver.LabelShort.CapitalizeFirst(), storeBuilding.LabelCapNoCount), bill.billStack.billGiver as Thing, MessageTypeDefOf.RejectInput, false);
            }
        }

        public static IEnumerable<CodeInstruction> Dialog_BillConfig_DoWindowContents_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //RimWorld.Dialog_BillConfig.DoWindowContents(Rect)
            //
            //^0IL_05B4: ldsfld    int32 RimWorld.Dialog_BillConfig::StoreModeSubdialogHeight
            //...
            //---listing_Standard3.ButtonText(text3, null)
            //^1IL_0660: ldloc.s   V_14
            //^2IL_0662: ldloc.s V_15
            //^3IL_0664: ldnull
            //^4IL_0665: callvirt instance bool Verse.Listing_Standard::ButtonText(string, string)
            //...
            //---Find.WindowStack.Add(new FloatMenu(list));
            //^5IL_085F: call class Verse.WindowStack Verse.Find::get_WindowStack()
            //^6IL_0864: ldloc.s V_16
            //IL_0866: newobj instance void Verse.FloatMenu::.ctor(class [mscorlib] System.Collections.Generic.List`1<class Verse.FloatMenuOption>)
            //IL_086B: callvirt instance void Verse.WindowStack::Add(class Verse.Window)
            //

            var get_WindowStackInfo = AccessTools.Property(typeof(Find), "WindowStack").GetGetMethod();

            int step = 0;
            foreach (CodeInstruction i in instructions)
            {
                if (step == 0 && i.opcode == OpCodes.Ldsfld && i.operand == AccessTools.Field(typeof(Dialog_BillConfig), "StoreModeSubdialogHeight"))
                {
                    step = 1;
                }
                if (step == 1 && i.opcode == OpCodes.Ldloc_S)
                {
                    step = 2;
                }
                if (step == 2 && i.opcode == OpCodes.Ldloc_S)
                {
                    step = 3;
                }
                if (step == 3 && i.opcode == OpCodes.Ldnull)
                {
                    step = 4;
                }
                if (step == 4 && i.opcode == OpCodes.Callvirt && i.operand == AccessTools.Method(typeof(Listing_Standard), "ButtonText"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(ButtonStoreModeText)));
                    yield return new CodeInstruction(OpCodes.Ldnull);
                    step = 5;
                }
                if (step == 5 && i.opcode == OpCodes.Call && i.operand == get_WindowStackInfo)
                {
                    step = 6;
                }
                if (step == 6 && i.opcode == OpCodes.Ldloc_S)
                {
                    yield return i;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(InsertStoreModeOptions)));
                    step = 7;
                    continue;
                }
                yield return i;
            }
        }

        public static string ButtonStoreModeText(string text, string nullHere, Dialog_BillConfig dialog)
        {
            var bill = Traverse.Create(dialog).Field("bill").GetValue<Bill_Production>();
            if (bill.GetStoreMode() == BillStoreModeDefOf.SpecificBuilding_Storage)
            {
                var storeBuilding = bill.GetStoreBuilding();

                text = string.Format(bill.GetStoreMode().LabelCap, (storeBuilding == null) ? string.Empty : storeBuilding.SlotYielderLabel());
                if (storeBuilding != null && !bill.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage(bill, storeBuilding))
                {
                    text += string.Format(" ({0})", "IncompatibleLower".Translate());
                    Text.Font = GameFont.Tiny;
                }

            }
            return text;
        }

        public static List<FloatMenuOption> InsertStoreModeOptions(List<FloatMenuOption> list, Dialog_BillConfig dialog)
        {
            list.RemoveAll(x => x.Label == BillStoreModeDefOf.SpecificBuilding_Storage.LabelCap);

            var bill = Traverse.Create(dialog).Field("bill").GetValue<Bill_Production>();

            List<SlotGroup> allGroupsListInPriorityOrder = bill.billStack.billGiver.Map.haulDestinationManager.AllGroupsListInPriorityOrder;
            int count = allGroupsListInPriorityOrder.Count;
            for (int i = 0; i < count; i++)
            {
                SlotGroup group = allGroupsListInPriorityOrder[i];
                Building_Storage building_Storage = group.parent as Building_Storage;
                if (building_Storage != null)
                {
                    if (!bill.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage(bill, building_Storage))
                    {
                        list.Add(new FloatMenuOption(string.Format("{0} ({1})", string.Format(BillStoreModeDefOf.SpecificBuilding_Storage.LabelCap, group.parent.SlotYielderLabel()), "IncompatibleLower".Translate()), null, MenuOptionPriority.Default, null, null, 0f, null, null));
                    }
                    else
                    {
                        list.Add(new FloatMenuOption(string.Format(BillStoreModeDefOf.SpecificBuilding_Storage.LabelCap, group.parent.SlotYielderLabel()), delegate ()
                        {
                            bill.SetSpecificBuildingStoreMode(building_Storage);
                        }, MenuOptionPriority.Default, null, null, 0f, null, null));
                    }
                }
            }

            return list;
        }
        #endregion

        public static void Thing_DeSpawnPostfix(Thing __instance)
        {
            var bs = __instance as Building_Storage;
            if (bs!=null)
            {
                Utils.Notify_Building_StorageRemoved(bs);
            }
        }

        #region ITab_Storage_FillTab

        public static void ITab_Storage_FillTab_Prefix(ITab_Storage __instance, ref Bill[] __state)
        {
            IStoreSettingsParent storeSettingsParent = Traverse.Create(__instance).Property("SelStoreSettingsParent").GetValue<IStoreSettingsParent>();
            __state = (from b in BillUtility.GlobalBills()
                       where b is Bill_Production && (b as Bill_Production).GetStoreBuilding() == storeSettingsParent && b.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage((Bill_Production)b, (b as Bill_Production).GetStoreBuilding())
                       select b).ToArray<Bill>();
        }

        public static void ITab_Storage_FillTab_Postfix(ITab_Storage __instance, ref Bill[] __state)
        {
            IStoreSettingsParent storeSettingsParent = Traverse.Create(__instance).Property("SelStoreSettingsParent").GetValue<IStoreSettingsParent>();
            Bill[] second = (from b in BillUtility.GlobalBills()
                             where b is Bill_Production && (b as Bill_Production).GetStoreBuilding() == storeSettingsParent && b.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage((Bill_Production)b, (b as Bill_Production).GetStoreBuilding())
                             select b).ToArray<Bill>();
            IEnumerable<Bill> enumerable = __state.Except(second);
            foreach (Bill bill in enumerable)
            {
                Messages.Message("MessageBillValidationStoreBuildingInsufficient".Translate(bill.LabelCap, bill.billStack.billGiver.LabelShort.CapitalizeFirst(), ((Bill_Production)bill).GetStoreBuilding().Label), bill.billStack.billGiver as Thing, MessageTypeDefOf.RejectInput, false);
            }
        } 
        #endregion
    }
}
