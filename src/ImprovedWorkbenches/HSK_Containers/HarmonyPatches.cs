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

            harmony.Patch(
                original: AccessTools.Method(type: typeof(Dialog_BillConfig), name: "GenerateStockpileInclusion"),
                postfix: new HarmonyMethod(type: patchType, name: nameof(Dialog_BillConfig_GenerateStockpileInclusion_Postfix)));

            harmony.Patch(
                original: AccessTools.Method(type: typeof(RecipeWorkerCounter), name: nameof(RecipeWorkerCounter.CountProducts)),
                prefix: new HarmonyMethod(type: patchType, name: nameof(RecipeWorkerCounter_CountProducts_Prefix)));
        }

        public static void ValidateSettingsPostfix(Bill_Production __instance, BillStoreModeDef ___storeMode)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                //Not all things loaded here. Containers may not exist yet.
                return;
            }

            #region Validate storeBuilding
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
            #endregion

            #region Validate includeFromBuilding
            var includeFromBuilding = __instance.GetIncludeFromBuilding();
            if (includeFromBuilding != null)
            {
                if (__instance.includeFromZone != null)
                {
                    Log.Error("Found includeFromBuilding and includeFromZone not null at same time! Setting includeFromBuilding to null.");
                    __instance.SetIncludeFromBuilding(null);
                }

                if (includeFromBuilding.Destroyed)
                {
                    if (__instance != BillUtility.Clipboard)
                    {
                        Messages.Message("MessageBillValidationIncludeBuildingDeleted".Translate(__instance.LabelCap, __instance.billStack.billGiver.LabelShort.CapitalizeFirst(), includeFromBuilding.LabelCapNoCount), __instance.billStack.billGiver as Thing, MessageTypeDefOf.NegativeEvent, true);
                    }
                    __instance.SetIncludeFromBuilding(null);
                }
                else if (__instance.Map != null && (__instance.Map != includeFromBuilding.Map))
                {
                    if (__instance != BillUtility.Clipboard)
                    {
                        Messages.Message("MessageBillValidationIncludeBuildingUnavailable".Translate(__instance.LabelCap, __instance.billStack.billGiver.LabelShort.CapitalizeFirst(), includeFromBuilding.LabelCapNoCount), __instance.billStack.billGiver as Thing, MessageTypeDefOf.NegativeEvent, true);
                    }
                    __instance.SetIncludeFromBuilding(null);
                }
            }
            #endregion
        }

        public static void SetStoreModePostfix(Bill_Production __instance, BillStoreModeDef ___storeMode)
        {
            if (___storeMode != BillStoreModeDefOf.SpecificBuilding_Storage)
            {
                var extendedData = __instance.GetExtendedData();
                extendedData.storeBuilding = null;
            }
        }

        public static FastInvokeHandler Toils_Recipe_CalculateIngredients = MethodInvoker.GetHandler(AccessTools.Method(typeof(Toils_Recipe), "CalculateIngredients"));
        public static FastInvokeHandler Toils_Recipe_CalculateDominantIngredient = MethodInvoker.GetHandler(AccessTools.Method(typeof(Toils_Recipe), "CalculateDominantIngredient"));
        public static FastInvokeHandler Toils_Recipe_ConsumeIngredients = MethodInvoker.GetHandler(AccessTools.Method(typeof(Toils_Recipe), "ConsumeIngredients"));
        public static bool FinishRecipeAndStartStoringProductPrefix(ref Toil __result)
        {
            //mostly copy of Verse.AI.Toils_Recipe.FinishRecipeAndStartStoringProduct()
            //need recheck arfter RW update

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
                List<Thing> ingredients = (List<Thing>)Toils_Recipe_CalculateIngredients(null, new object[] { curJob, actor });
                Thing dominantIngredient = (Thing)Toils_Recipe_CalculateDominantIngredient(null, new object[] { curJob, ingredients });
                List <Thing> list = GenRecipe.MakeRecipeProducts(curJob.RecipeDef, actor, ingredients, dominantIngredient, jobDriver_DoBill.BillGiver).ToList<Thing>();
                Toils_Recipe_ConsumeIngredients(null, new object[] { ingredients, curJob.RecipeDef, actor.Map });
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

        public static void Dialog_BillConfig_DoWindowContents_Prefix(Dialog_BillConfig __instance, ref bool __state, Bill_Production ___bill)
        {
            var storeBuilding = ___bill.GetStoreBuilding();

            __state = storeBuilding == null || ___bill.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage(___bill, storeBuilding);
        }

        public static void Dialog_BillConfig_DoWindowContents_Postfix(Dialog_BillConfig __instance, ref bool __state, Bill_Production ___bill)
        {
            var storeBuilding = ___bill.GetStoreBuilding();

            if (__state && !(storeBuilding == null || ___bill.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage(___bill, storeBuilding)))
            {
                Messages.Message("MessageBillValidationStoreBuildingInsufficient".Translate(___bill.LabelCap, ___bill.billStack.billGiver.LabelShort.CapitalizeFirst(), storeBuilding.LabelCapNoCount), ___bill.billStack.billGiver as Thing, MessageTypeDefOf.RejectInput, false);
            }

            if (___bill.includeFromZone != null) ___bill.SetIncludeFromBuilding(null);
        }

        public static IEnumerable<CodeInstruction> Dialog_BillConfig_DoWindowContents_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //---Widgets.Dropdown<Bill_Production, Zone_Stockpile>(listing_Standard2.GetRect(30f), this.bill, (Bill_Production b) => b.includeFromZone, (Bill_Production b) => this.GenerateStockpileInclusion(), (this.bill.includeFromZone != null) ? "IncludeSpecific".Translate(this.bill.includeFromZone.label) : "IncludeFromAll".Translate(), null, null, null, null, false);
            //^0    IL_03E1: ldfld     class RimWorld.Zone_Stockpile RimWorld.Bill_Production::includeFromZone
            //^1    IL_03E6: brtrue IL_03FA
            //^2    IL_03EB: ldstr     "IncludeFromAll"
            //^3    IL_03F0: call      string Verse.Translator::Translate(string)
            //      IL_03F5: br IL_0419
            //      IL_03FA: ldstr     "IncludeSpecific"
            //      IL_03FF: ldarg.0
            //      IL_0400: ldfld     class RimWorld.Bill_Production RimWorld.Dialog_BillConfig::bill
            //      IL_0405: ldfld     class RimWorld.Zone_Stockpile RimWorld.Bill_Production::includeFromZone
            //      IL_040A: ldfld     string Verse.Zone::label
            //      IL_040F: call valuetype Verse.NamedArgument Verse.NamedArgument::op_Implicit(string)
            //      IL_0414: call      string Verse.TranslatorFormattedStringExtensions::Translate(string, valuetype Verse.NamedArgument)
            //      IL_0419: ldnull
            //      IL_041A: ldnull
            //      IL_041B: ldnull
            //      IL_041C: ldnull
            //      IL_041D: ldc.i4.0
            //     IL_041E: call void Verse.Widgets::Dropdown<class RimWorld.Bill_Production, class RimWorld.Zone_Stockpile>(valuetype[UnityEngine] UnityEngine.Rect, !!0, class [System.Core] System.Func`2<!!0, !!1>, class [System.Core] System.Func`2<!!0, class [mscorlib] System.Collections.Generic.IEnumerable`1<valuetype Verse.Widgets/DropdownMenuElement`1<!!1>>>, string, class [UnityEngine] UnityEngine.Texture2D, string, class [UnityEngine] UnityEngine.Texture2D, class [System.Core] System.Action, bool)
            //...
            //^4    IL_05B4: ldsfld    int32 RimWorld.Dialog_BillConfig::StoreModeSubdialogHeight
            //...
            //---listing_Standard3.ButtonText(text3, null)
            //^5    IL_0660: ldloc.s   V_14
            //^6    IL_0662: ldloc.s V_15
            //^7    IL_0664: ldnull
            //^8    IL_0665: callvirt instance bool Verse.Listing_Standard::ButtonText(string, string)
            //...
            //---Find.WindowStack.Add(new FloatMenu(list));
            //^9    IL_085F: call class Verse.WindowStack Verse.Find::get_WindowStack()
            //^10   IL_0864: ldloc.s V_16
            //      IL_0866: newobj instance void Verse.FloatMenu::.ctor(class [mscorlib] System.Collections.Generic.List`1<class Verse.FloatMenuOption>)
            //      IL_086B: callvirt instance void Verse.WindowStack::Add(class Verse.Window)
            //

            int step = 0;

            foreach (CodeInstruction i in instructions)
            {
                //include building patches
                if (step == 0 && i.opcode == OpCodes.Ldfld && i.operand == AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.includeFromZone)))
                {
                    yield return i;
                    step++;
                    continue;
                }

                if (step == 1 && i.opcode == OpCodes.Brtrue)
                {
                    yield return i;
                    step++;
                    continue;
                }

                if (step == 2 && i.opcode == OpCodes.Ldstr)
                {
                    yield return i;
                    step++;
                    continue;
                }

                if (step == 3 && i.opcode == OpCodes.Call && i.operand == AccessTools.Method(typeof(Translator), nameof(Translator.Translate),new Type[] {typeof(string)}))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(ButtonIncludeFromText))) {labels = i.labels, blocks = i.blocks};
                    step++;
                    continue;
                }

                //store building patches
                if (step == 4 && i.opcode == OpCodes.Ldsfld && i.operand == AccessTools.Field(typeof(Dialog_BillConfig), "StoreModeSubdialogHeight"))
                {
                    yield return i;
                    step++;
                    continue;
                }
                if (step == 5 && i.opcode == OpCodes.Ldloc_S)
                {
                    yield return i;
                    step++;
                    continue;
                }
                if (step == 6 && i.opcode == OpCodes.Ldloc_S)
                {
                    yield return i;
                    step++;
                    continue;
                }
                if (step == 7 && i.opcode == OpCodes.Ldnull)
                {
                    yield return i;
                    step++;
                    continue;
                }
                if (step == 8 && i.opcode == OpCodes.Callvirt && i.operand == AccessTools.Method(typeof(Listing_Standard), "ButtonText"))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(ButtonStoreModeText)));
                    yield return new CodeInstruction(OpCodes.Ldnull);
                    yield return i;
                    step++;
                    continue;
                }
                if (step == 9 && i.opcode == OpCodes.Call && i.operand == AccessTools.Property(typeof(Find), "WindowStack").GetGetMethod())
                {
                    yield return i;
                    step++;
                    continue;
                }
                if (step == 10 && i.opcode == OpCodes.Ldloc_S)
                {
                    yield return i;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(InsertStoreModeOptions)));
                    step++;
                    continue;
                }
                yield return i;
            }
            if (step < 11) Log.Error($"Transpiler Fail {step}");
        }

        public static AccessTools.FieldRef<Dialog_BillConfig, Bill_Production> Dialog_BillConfig_bill = AccessTools.FieldRefAccess<Dialog_BillConfig, Bill_Production>("bill");
        public static string ButtonStoreModeText(string text, string nullHere, Dialog_BillConfig dialog)
        {
            var bill = Dialog_BillConfig_bill(dialog);
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

            var bill = Dialog_BillConfig_bill(dialog);

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

        public static string ButtonIncludeFromText(string text, Dialog_BillConfig dialog)
        {
            var bill = Dialog_BillConfig_bill(dialog);
            var includeFromBuilding = bill.GetIncludeFromBuilding();
            if (includeFromBuilding == null) return text.Translate();
            return "IncludeSpecificBuilding".Translate(includeFromBuilding.LabelCapNoCount);
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

        public static FastInvokeHandler ITab_Storage_SelStoreSettingsParent_Get = MethodInvoker.GetHandler(AccessTools.Property(typeof(ITab_Storage), "SelStoreSettingsParent").GetGetMethod(true));
        public static void ITab_Storage_FillTab_Prefix(ITab_Storage __instance, ref Bill[] __state)
        {
            IStoreSettingsParent storeSettingsParent = (IStoreSettingsParent)ITab_Storage_SelStoreSettingsParent_Get(__instance, null);
            __state = (from b in BillUtility.GlobalBills()
                       where b is Bill_Production && (b as Bill_Production).GetStoreBuilding() == storeSettingsParent && b.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage((Bill_Production)b, (b as Bill_Production).GetStoreBuilding())
                       select b).ToArray<Bill>();
        }

        public static void ITab_Storage_FillTab_Postfix(ITab_Storage __instance, ref Bill[] __state)
        {
            IStoreSettingsParent storeSettingsParent = (IStoreSettingsParent)ITab_Storage_SelStoreSettingsParent_Get(__instance, null);
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

        #region Dialog_BillConfig_GenerateStockpileInclusion_Postfix

        public static void Dialog_BillConfig_GenerateStockpileInclusion_Postfix(Dialog_BillConfig __instance, ref IEnumerable<Widgets.DropdownMenuElement<Zone_Stockpile>> __result, Bill_Production ___bill)
        {
            __result = __result.Concat(GenerateBuildingInclusion(___bill));
        }

        public static IEnumerable<Widgets.DropdownMenuElement<Zone_Stockpile>> GenerateBuildingInclusion(Bill_Production bill)
        {
            //payload for Building is null

            List<SlotGroup> groupList = bill.billStack.billGiver.Map.haulDestinationManager.AllGroupsListInPriorityOrder;
            foreach (var group in groupList)
            {
                if (group.parent is Building_Storage storeBuilding)
                {
                    if (!bill.recipe.WorkerCounter.CanPossiblyStoreInBuilding_Storage(bill, storeBuilding))
                    {
                        yield return new Widgets.DropdownMenuElement<Zone_Stockpile>
                        {
                            option = new FloatMenuOption(string.Format("{0} ({1})", "IncludeSpecificBuilding".Translate(group.parent.SlotYielderLabel()), "IncompatibleLower".Translate()), null, MenuOptionPriority.Default, null, null, 0f, null, null),
                            payload = null
                        };
                    }
                    else
                    {
                        yield return new Widgets.DropdownMenuElement<Zone_Stockpile>
                        {
                            option = new FloatMenuOption("IncludeSpecificBuilding".Translate(group.parent.SlotYielderLabel()), delegate ()
                            {
                                bill.SetIncludeFromBuilding(storeBuilding);
                            }, MenuOptionPriority.Default, null, null, 0f, null, null),
                            payload = null
                        };
                    }
                }
            }
        }
        #endregion

        public static bool RecipeWorkerCounter_CountProducts_Prefix(ref RecipeWorkerCounter __instance, ref int __result, ref Bill_Production bill)
        {
            var includeFromBuilding = bill.GetIncludeFromBuilding();

            if (includeFromBuilding == null) return true;

            ThingDefCountClass thingDefCountClass = __instance.recipe.products[0];
            ThingDef thingDef = thingDefCountClass.thingDef;
            int count = 0;

            {
                foreach (Thing outerThing in includeFromBuilding.AllContainedThings())
                {
                    Thing innerIfMinified = outerThing.GetInnerIfMinified();
                    if (__instance.CountValidThing(innerIfMinified, bill, thingDef))
                    {
                        count += innerIfMinified.stackCount;
                    }
                }
            }

            if (bill.includeEquipped)
            {
                foreach (Pawn pawn in bill.Map.mapPawns.FreeColonistsSpawned)
                {
                    List<ThingWithComps> allEquipmentListForReading = pawn.equipment.AllEquipmentListForReading;
                    for (int j = 0; j < allEquipmentListForReading.Count; j++)
                    {
                        if (__instance.CountValidThing(allEquipmentListForReading[j], bill, thingDef))
                        {
                            count += allEquipmentListForReading[j].stackCount;
                        }
                    }
                    List<Apparel> wornApparel = pawn.apparel.WornApparel;
                    for (int k = 0; k < wornApparel.Count; k++)
                    {
                        if (__instance.CountValidThing(wornApparel[k], bill, thingDef))
                        {
                            count += wornApparel[k].stackCount;
                        }
                    }
                    ThingOwner directlyHeldThings = pawn.inventory.GetDirectlyHeldThings();
                    for (int l = 0; l < directlyHeldThings.Count; l++)
                    {
                        if (__instance.CountValidThing(directlyHeldThings[l], bill, thingDef))
                        {
                            count += directlyHeldThings[l].stackCount;
                        }
                    }
                }
            }
            __result = count;
            return false;
        }
    }
}
