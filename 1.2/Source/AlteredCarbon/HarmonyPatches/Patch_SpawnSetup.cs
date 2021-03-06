﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using static RimWorld.QuestGen.QuestGen_Pawns;

namespace AlteredCarbon
{
    [HarmonyPatch(typeof(Thing))]
    [HarmonyPatch("SpawnSetup")]
    public static class Patch_SpawnSetup
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance)
        {
            if (__instance is Pawn pawn && pawn.RaceProps.Humanlike && pawn.kindDef
                .HasModExtension<StackSpawnModExtension>())
            {
                var extension = pawn.kindDef.GetModExtension<StackSpawnModExtension>();
                if (extension.SpawnsWithStack && Rand.Chance((float)extension.ChanceToSpawnWithStack / 100f))
                {
                    BodyPartRecord neckRecord = pawn.def.race.body.AllParts.FirstOrDefault((BodyPartRecord x) => x.def == BodyPartDefOf.Neck);
                    var hediff = HediffMaker.MakeHediff(AlteredCarbonDefOf.AC_CorticalStack, pawn, neckRecord) as Hediff_CorticalStack;
                    hediff.gender = pawn.gender;
                    hediff.race = pawn.kindDef.race;
                    if (ACUtils.ACTracker.stacksRelationships != null)
                    {
                        hediff.stackGroupID = ACUtils.ACTracker.stacksRelationships.Count + 1;
                    }
                    else
                    {
                        hediff.stackGroupID = 0;
                    }
                    pawn.health.AddHediff(hediff, neckRecord);
                    ACUtils.ACTracker.RegisterPawn(pawn);
                    ACUtils.ACTracker.TryAddRelationships(pawn);
                }
            }
        }
    }

    [HarmonyPatch(typeof(QuestPart_BestowingCeremony))]
    [HarmonyPatch("MakeLord")]
    public static class MakeLord_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(QuestPart_BestowingCeremony __instance, Lord __result)
        {
            if (__instance.bestower.kindDef == PawnKindDefOf.Empire_Royal_Bestower)
            {
                RoyalTitleDef titleAwardedWhenUpdating = __instance.target.royalty.GetTitleAwardedWhenUpdating(__instance.bestower.Faction,
                    __instance.target.royalty.GetFavor(__instance.bestower.Faction));
                if (titleAwardedWhenUpdating.defName == "Baron" || titleAwardedWhenUpdating.defName == "Count")
                {
                    ThingOwner<Thing> innerContainer = __instance.bestower.inventory.innerContainer;
                    innerContainer.TryAdd(ThingMaker.MakeThing(AlteredCarbonDefOf.AC_EmptyCorticalStack), 1);
                }
            }
        }
    }

    [HarmonyPatch(typeof(LordJob_BestowingCeremony))]
    [HarmonyPatch("FinishCeremony")]
    public static class FinishCeremony_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(LordJob_BestowingCeremony __instance, Pawn pawn)
        {
            if (__instance.bestower.kindDef == PawnKindDefOf.Empire_Royal_Bestower)
            {
                ThingOwner<Thing> innerContainer = __instance.bestower.inventory.innerContainer;
                for (int num = innerContainer.Count - 1; num >= 0; num--)
                {
                    if (innerContainer[num].def == AlteredCarbonDefOf.AC_EmptyCorticalStack)
                    {
                        innerContainer.TryDrop(innerContainer[num], ThingPlaceMode.Near, out Thing lastResultingThing);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Reward_BestowingCeremony), "StackElements", MethodType.Getter)]
    public static class StackElements_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Reward_BestowingCeremony __instance, ref IEnumerable<GenUI.AnonymousStackElement> __result)
        {
            if (__instance.royalTitle.defName == "Baron" || __instance.royalTitle.defName == "Count")
            {
                var list = __result.ToList();
                var item = QuestPartUtility.GetStandardRewardStackElement(AlteredCarbonDefOf.AC_EmptyCorticalStack.label.CapitalizeFirst(), AlteredCarbonDefOf.AC_EmptyCorticalStack.uiIcon, () => AlteredCarbonDefOf.AC_EmptyCorticalStack.description, delegate
                {
                    Find.WindowStack.Add(new Dialog_InfoCard(AlteredCarbonDefOf.AC_EmptyCorticalStack));
                });
                list.Insert(1, item);
                __result = list;
            }
        }
    }
}

