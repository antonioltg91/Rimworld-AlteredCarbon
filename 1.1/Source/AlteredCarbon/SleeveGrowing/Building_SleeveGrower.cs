﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AlteredCarbon
{
	public class Building_SleeveGrower : Building, IThingHolder, IOpenable
	{
		protected ThingOwner innerContainer;

		protected bool contentsKnown;

		public int runningOutPowerInTicks;

		public bool innerPawnIsDead;
		public bool HasAnyContents => innerContainer.Count > 0;

		public Thing ContainedThing
		{
			get
			{
				if (innerContainer.Count != 0)
				{
					return innerContainer[0];
				}
				return null;
			}
		}

		public bool CanOpen => HasAnyContents && !this.active;

		public Building_SleeveGrower()
		{
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public override void TickRare()
		{
			base.TickRare();
			innerContainer.ThingOwnerTickRare();
		}
		public virtual void Open()
		{
			if (HasAnyContents)
			{
				EjectContents();
			}
		}
		public override bool ClaimableBy(Faction fac)
		{
			if (innerContainer.Any)
			{
				for (int i = 0; i < innerContainer.Count; i++)
				{
					if (innerContainer[i].Faction == fac)
					{
						return true;
					}
				}
				return false;
			}
			return base.ClaimableBy(fac);
		}
		public virtual bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
		{
			if (!Accepts(thing))
			{
				return false;
			}
			bool flag = false;
			if (thing.holdingOwner != null)
			{
				thing.holdingOwner.TryTransferToContainer(thing, innerContainer, thing.stackCount);
				flag = true;
			}
			else
			{
				flag = innerContainer.TryAdd(thing);
			}
			if (flag)
			{
				if (thing.Faction != null && thing.Faction.IsPlayer)
				{
					contentsKnown = true;
				}
				return true;
			}
			return false;
		}

		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			if (innerContainer.Count > 0 && (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize) && !this.active && this.curTicksToGrow == this.totalTicksToGrow)
			{
				if (mode != DestroyMode.Deconstruct)
				{
					List<Pawn> list = new List<Pawn>();
					foreach (Thing item in (IEnumerable<Thing>)innerContainer)
					{
						Pawn pawn = item as Pawn;
						if (pawn != null)
						{
							list.Add(pawn);
						}
					}
					foreach (Pawn item2 in list)
					{
						HealthUtility.DamageUntilDowned(item2);
					}
				}
				EjectContents();
			}
			innerContainer.ClearAndDestroyContents();
			base.Destroy(mode);
		}

		public bool IsOperating
		{
			get
			{
				CompPowerTrader compPowerTrader = this.powerTrader;
				if (compPowerTrader == null || compPowerTrader.PowerOn)
				{
					CompBreakdownable compBreakdownable = this.breakdownable;
					return compBreakdownable == null || !compBreakdownable.BrokenDown;
				}
				return false;
			}
		}

		public bool Accepts(Thing thing)
		{
			return this.innerContainer.Count == 0;
		}
		public Pawn InnerPawn
		{
			get
			{
				return this.innerContainer.FirstOrDefault<Thing>() as Pawn;
			}
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (base.Faction != null && base.Faction.IsPlayer)
			{
				contentsKnown = true;
			}
			this.powerTrader = base.GetComp<CompPowerTrader>();
			this.breakdownable = base.GetComp<CompBreakdownable>();
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo in base.GetGizmos())
			{
				yield return gizmo;
			}
			if (base.Faction == Faction.OfPlayer && innerContainer.Count > 0 && this.active)
			{
				Command_Action command_Action = new Command_Action();
				command_Action.action = this.CancelGrowing;
				command_Action.defaultLabel = "AlteredCarbon.CancelSleeveBodyGrowing".Translate();
				command_Action.defaultDesc = "AlteredCarbon.CancelSleeveBodyGrowingDesc".Translate();
				command_Action.hotKey = KeyBindingDefOf.Misc8;
				command_Action.icon = ContentFinder<Texture2D>.Get("UI/Icons/CancelSleeve");
				yield return command_Action;
			}
			if (base.Faction == Faction.OfPlayer && (this.ContainedThing == null || this.innerPawnIsDead))
            {
				Command_Action command_Action2 = new Command_Action();
				command_Action2.action = new Action(this.CreateSleeve);
				command_Action2.defaultLabel = "AlteredCarbon.CreateSleeveBody".Translate();
				command_Action2.defaultDesc = "AlteredCarbon.CreateSleeveBodyDesc".Translate();
				command_Action2.hotKey = KeyBindingDefOf.Misc8;
				command_Action2.icon = ContentFinder<Texture2D>.Get("UI/Icons/CreateSleeve", true);
				yield return command_Action2;
			}
			if (Prefs.DevMode && active)
			{
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "Debug: Instant grow";
				command_Action.action = InstantGrowth;
				yield return command_Action;
			}
			yield break;
		}
		public override string GetInspectString()
		{
			if (this.ContainedThing != null)
			{
				return base.GetInspectString() + "\n" + "GrowthProgress".Translate() +
					Math.Round(((float)this.curTicksToGrow / this.totalTicksToGrow) * 100f, 2).ToString() + "%";
			}
			else
			{
				return base.GetInspectString();
			}
		}

		public Graphic fetus;
		public Graphic Fetus
		{
			get
			{
				if (fetus == null)
				{
					fetus = GraphicDatabase.Get<Graphic_Single>("Things/Pawn/Humanlike/Vat/Fetus", ShaderDatabase.CutoutFlying, Vector3.one, this.InnerPawn.story.SkinColor);
				}
				return fetus;
			}
		}

		public Graphic child;
		public Graphic Child
		{
			get
			{
				if (child == null)
				{
					child = GraphicDatabase.Get<Graphic_Multi>("Things/Pawn/Humanlike/Vat/Child", ShaderDatabase.CutoutFlying, Vector3.one, this.InnerPawn.story.SkinColor);
				}
				return child;
			}
		}

		public Graphic fetus_dead;
		public Graphic Fetus_Dead
		{
			get
			{
				if (fetus_dead == null)
				{
					fetus_dead = GraphicDatabase.Get<Graphic_Single>("Things/Pawn/Humanlike/Vat/Fetus_Dead", ShaderDatabase.CutoutFlying, Vector3.one, this.InnerPawn.story.SkinColor);
				}
				return fetus_dead;
			}
		}
		public Graphic child_dead;
		public Graphic Child_Dead
		{
			get
			{
				if (child_dead == null)
				{
					child_dead = GraphicDatabase.Get<Graphic_Multi>("Things/Pawn/Humanlike/Vat/Child_Dead", ShaderDatabase.CutoutFlying, Vector3.one, this.InnerPawn.story.SkinColor);
				}
				return child_dead;
			}
		}

		public Graphic adult_dead;
		public Graphic Adult_Dead
		{
			get
			{
				if (adult_dead == null)
				{
					adult_dead = GraphicDatabase.Get<Graphic_Multi>("Things/Pawn/Humanlike/Vat/Adult_Dead", ShaderDatabase.CutoutFlying, Vector3.one, this.InnerPawn.story.SkinColor);
				}
				return adult_dead;
			}
		}

		public override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			base.DrawAt(drawLoc, flip);

			if (this.ContainedThing is Pawn)
			{
				Vector3 newPos = drawLoc;
				newPos.z += 0.5f;
				var growthValue = (float)this.curTicksToGrow / this.totalTicksToGrow;
				if (!this.innerPawnIsDead)
				{
					if (growthValue < 0.33f)
					{
						Fetus.Draw(newPos, Rot4.North, this);
					}
					else if (growthValue < 0.66f)
					{
						Child.Draw(newPos, Rot4.North, this);
					}
					else
					{
						this.ContainedThing.Rotation = Rot4.South;
						this.ContainedThing.DrawAt(newPos, flip);
					}
				}
				else if (this.innerPawnIsDead)
				{
					if (growthValue < 0.33f)
					{
						Fetus_Dead.Draw(newPos, Rot4.North, this);
					}
					else if (growthValue < 0.66f)
					{
						Child_Dead.Draw(newPos, Rot4.North, this);
					}
					else
					{
						Adult_Dead.Draw(newPos, Rot4.North, this);
					}
				}
			}
			base.Comps_PostDraw();
		}


		public void ResetGraphics()
        {
			this.fetus = null;
			this.child = null;
			this.fetus_dead = null;
			this.child_dead = null;
			this.adult_dead = null;
		}
		public void CancelGrowing()
		{
			this.active = false;
			this.totalGrowthCost = 0;
			this.totalTicksToGrow = 0;
			this.curTicksToGrow = 0;
			this.innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
			ResetGraphics();
		}
		public void CreateSleeve()
		{
			Find.WindowStack.Add(new CustomizeSleeveWindow(this));
		}

		public void StartGrowth(Pawn newSleeve, int totalTicksToGrow, int totalGrowthCost)
        {
			this.ResetGraphics();
			this.innerContainer.ClearAndDestroyContents();
			this.TryAcceptThing(newSleeve);
			this.totalTicksToGrow = totalTicksToGrow;
			this.curTicksToGrow = 0;
			this.totalGrowthCost = totalGrowthCost;
			this.active = true;
			this.innerPawnIsDead = false;
			this.runningOutPowerInTicks = 0;
		}

		public void InstantGrowth()
        {
			this.curTicksToGrow = this.totalTicksToGrow;
			FinishGrowth();
		}
		public void FinishGrowth()
        {
			this.active = false;
			if (ACUtils.ACTracker.emptySleeves == null) ACUtils.ACTracker.emptySleeves = new HashSet<Pawn>();
			ACUtils.ACTracker.emptySleeves.Add(this.InnerPawn);
		}

		public void KillInnerPawn()
        {
			this.innerPawnIsDead = true;
		}

		public override void Tick()
		{
			base.Tick();
			if (this.ContainedThing == null && this.curTicksToGrow > 0)
			{
				curTicksToGrow = 0;
			}
			if (this.ContainedThing is Pawn)
            {
				if (this.active && base.GetComp<CompRefuelable>().HasFuel && powerTrader.PowerOn)
				{
					if (runningOutPowerInTicks > 0) runningOutPowerInTicks = 0;
					var fuelCost = this.totalGrowthCost / (float)this.totalTicksToGrow;
					base.GetComp<CompRefuelable>().ConsumeFuel(fuelCost);
					if (this.curTicksToGrow < totalTicksToGrow)
					{
						curTicksToGrow++;
					}
					else
					{
						this.FinishGrowth();
					}
				}
				else if (this.active && !powerTrader.PowerOn && runningOutPowerInTicks < 60000)
                {
					runningOutPowerInTicks++;
                }
				else if (runningOutPowerInTicks >= 60000 && this.active)
                {
					this.active = false;
					this.KillInnerPawn();
                }
			}
		}

		public void EjectContents()
		{
			ThingDef filth_Slime = ThingDefOf.Filth_Slime;
			foreach (Thing thing in this.innerContainer)
			{
				Pawn pawn = thing as Pawn;
				if (pawn != null)
				{
					PawnComponentsUtility.AddComponentsForSpawn(pawn);
					pawn.filth.GainFilth(filth_Slime);
					this.InnerPawn.health.AddHediff(AlteredCarbonDefOf.AC_EmptySleeve);
				}
			}
			if (!base.Destroyed)
			{
				SoundStarter.PlayOneShot(SoundDefOf.CryptosleepCasket_Eject,
					SoundInfo.InMap(new TargetInfo(base.Position, base.Map, false), 0));
			}
			this.innerContainer.TryDropAll(this.InteractionCell, base.Map, ThingPlaceMode.Near, null, null);
			this.contentsKnown = true;
			ResetGraphics();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
			Scribe_Values.Look(ref contentsKnown, "contentsKnown", defaultValue: false);

			Scribe_Values.Look<int>(ref this.totalTicksToGrow, "totalTicksToGrow", 0, true);
			Scribe_Values.Look<int>(ref this.curTicksToGrow, "curTicksToGrow", 0, true);

			Scribe_Values.Look<float>(ref this.totalGrowthCost, "totalGrowthCost", 0f, true);
			Scribe_Values.Look<bool>(ref this.contentsKnown, "contentsKnown", false, true);
			Scribe_Values.Look<bool>(ref this.active, "active", false, true);

			Scribe_Values.Look<int>(ref this.runningOutPowerInTicks, "runningOutPowerInTicks", 0, true);
			Scribe_Values.Look<bool>(ref this.innerPawnIsDead, "innerPawnIsDead", false, true);
		}

		public int totalTicksToGrow = 0;
		public int curTicksToGrow = 0;

		public float totalGrowthCost = 0;
		public bool active;

		private CompPowerTrader powerTrader;

		private CompBreakdownable breakdownable;

	}
}

