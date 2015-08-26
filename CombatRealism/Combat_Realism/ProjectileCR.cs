using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse.Sound;
using Verse;

namespace Combat_Realism
{
	/// <summary>
	/// Description of ProjectileCR.
	/// </summary>
	public class ProjectileCR : Projectile
	{
		private Sustainer ambientSustainer;		//required for the Launch sounds
        private static List<IntVec3> checkedCells = new List<IntVec3>();
		private bool targetDownedOnSpawn = false;
        
		/*
		 * Things to add:
		 * 
		 * condition factored into shot variation
		 * optics improving ranged finding
		 * ++ Added, needs improvements
		 * increase chance of hitting a downed pawn if the pawn was downed before the shot was fired
		 * ++ Basically done
		 */
		
		new public void Launch(Thing launcher, TargetInfo targ, Thing equipment = null)
		{
			this.Launch(launcher, base.Position.ToVector3Shifted(), targ, null);
		}
		new public void Launch(Thing launcher, Vector3 origin, TargetInfo targ, Thing equipment = null)
		{
			this.launcher = launcher;
			this.origin = origin;
			if (equipment != null)
			{
				this.equipmentDef = equipment.def;
			}
			else
			{
				this.equipmentDef = null;
			}
			if (targ.Thing != null)
			{
				this.assignedTarget = targ.Thing;
				Pawn pawn = this.assignedTarget as Pawn;
				if (pawn != null)
				{
					this.targetDownedOnSpawn = pawn.Downed;
				}
			}
			if (this.destination == null)
				this.destination = targ.Cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, Rand.Range(-0.3f, 0.3f));
			
			this.ticksToImpact = this.StartingTicksToImpact;
			if (!this.def.projectile.soundAmbient.NullOrUndefined())
			{
				SoundInfo info = SoundInfo.InWorld(this, MaintenanceType.PerTick);
				this.ambientSustainer = this.def.projectile.soundAmbient.TrySpawnSustainer(info);
			}
		}
		public void Launch(Thing launcher, Vector3 origin, TargetInfo targ, Vector3 target, Thing equipment = null)
		{
			this.destination = target;
			Launch(launcher, origin, targ, equipment);
		}
		
		// CUSTOM CHECKFORFREEINTERCEPTBETWEEN
        private bool CheckForFreeInterceptBetween(Vector3 lastExactPos, Vector3 newExactPos)
        {
            IntVec3 lastPos = lastExactPos.ToIntVec3();
            IntVec3 newPos = newExactPos.ToIntVec3();
            if (newPos == lastPos)
            {
                return false;
            }
            if (!lastPos.InBounds() || !newPos.InBounds())
            {
                return false;
            }
            if ((newPos - lastPos).LengthManhattan == 1)
            {
                return this.CheckForFreeIntercept(newPos);
            }
            if (this.origin.ToIntVec3().DistanceToSquared(newPos) > 16f)
            {
                Vector3 vector = lastExactPos;
                Vector3 v = newExactPos - lastExactPos;
                Vector3 b = v.normalized * 0.2f;
                int num = (int)(v.MagnitudeHorizontal() / 0.2f);
                ProjectileCR.checkedCells.Clear();
                int num2 = 0;
                while (true)
                {
                    vector += b;
                    IntVec3 intVec3 = vector.ToIntVec3();
                    if (!ProjectileCR.checkedCells.Contains(intVec3))
                    {
                        if (this.CheckForFreeIntercept(intVec3))
                        {
                            break;
                        }
                        ProjectileCR.checkedCells.Add(intVec3);
                    }
                    num2++;
                    if (num2 > num)
                    {
                        return false;
                    }
                    if (intVec3 == newPos)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private bool CheckForFreeIntercept(IntVec3 cell)
        {
            float distFromOrigin = (cell.ToVector3Shifted() - this.origin).MagnitudeHorizontalSquared();
            if (distFromOrigin < 16f)
            {
                return false;
            }
            List<Thing> list = Find.ThingGrid.ThingsListAt(cell);
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (thing != this.AssignedMissTarget)
                {
                    if (thing.def.Fillage == FillCategory.Full)
                    {
                        this.Impact(thing);
                        return true;
                    }
                    if (thing.def.category == ThingCategory.Pawn)
                    {
                    	if (this.assignedTarget != null)
                    	{
                    		Pawn pawnTarg = this.assignedTarget as Pawn;
                    		if (pawnTarg != null)
                    		{
	                    		return ImpactThroughBodySizeCheckWithTarget(thing, 0.45f);	//Added a factor for collaterals, hardcoded
                    		}
                    	}
                    	
                        /*Pawn pawn = (Pawn)thing;
                        float collateralChance = 0.45f;
                        if (pawn.GetPosture() != PawnPosture.Standing)
                        {
                            if (pawn.def.race.baseBodySize > 1)
                            {
                                collateralChance *= 0.7f;
                            }
                            else if (pawn.def.race.baseBodySize > 0.5)
                            {
                                collateralChance *= 0.2f;
                            }
                            else
                            {
                                collateralChance *= 0.8f;
                            }
                        }
                        collateralChance *= pawn.BodySize;

                        if (Rand.Value < collateralChance)
                        {
                            this.Impact(pawn);
                            return true;
                        }*/
                    }
                    if (Rand.Value < thing.def.fillPercent)
                    {
                        this.Impact(thing);
                        return true;
                    }

                }
            }
            return false;
        }
        
		/// <summary>
		/// Takes into account the target being downed and the projectile having been fired while the target was downed, and the target's bodySize
		/// </summary>
        private bool ImpactThroughBodySize(Thing thing, float factor = 1)
        {
        	Pawn pawn = thing as Pawn;
        	if (pawn != null)
        	{
        		if (pawn.Downed != this.targetDownedOnSpawn)
        		{
        			double checkNr;
        			if (pawn.BodySize >= 1.666)		//Why 1.666? that's the point at which (X - 0.5) / X = 0.7
        			{
        				checkNr = (pawn.BodySize - 0.5) / pawn.BodySize;
        			}
        			else
        			{
        					//This makes two lines (1.666, 0.7) -> (1.0, 0.8) -> (0.0, 1.0)
        				checkNr = (pawn.BodySize < 1 ? 1 : 0.95) - (pawn.BodySize < 1 ? 0.2 : 0.15) * pawn.BodySize;
        			}
        			bool hit = Rand.Value < checkNr * factor;
        			this.Impact(hit ? thing : null);
        			return hit;
        		}
        		else
        		{
        			bool hit = pawn.Downed ? Rand.Value < 0.93 * factor : true;
        			this.Impact(hit ? thing : null);
        			return hit;
        		}
        	}
        	if ((factor != 1 && Rand.Value < factor) || true)
        	{
        		this.Impact(thing);
        		return true;
        	}
        	
        	this.Impact(null);
        	return false;
        }
        
		/// <summary>
		/// Checks a new suggested target with the old target and decides whether it should go through an ImpactThroughBodySize
		/// Can only be called when this.assignedTarget exists
		/// </summary>
        private bool ImpactThroughBodySizeCheckWithTarget(Thing thing, float factor = 1)
        {
        	Pawn pawn = thing as Pawn;
        	Pawn pawnTarg = this.assignedTarget as Pawn;
        	if (pawn.def.race.body == pawnTarg.def.race.body
        	    || (pawn.BodySize >= pawnTarg.BodySize
        	    || (pawn.BodySize >= 0.5 * pawnTarg.BodySize && (!pawn.Downed && this.targetDownedOnSpawn)))
        	    || Rand.Value < pawn.BodySize / pawnTarg.BodySize)
        	{
        		return this.ImpactThroughBodySize(thing, factor);
        	}
        	this.Impact(null);
        	return false;
        }
        
		private void ImpactSomething()
		{
				//Not modified, just mortar code
			if (this.def.projectile.flyOverhead)
			{
				RoofDef roofDef = Find.RoofGrid.RoofAt(base.Position);
				if (roofDef != null && roofDef.isThickRoof)
				{
					this.def.projectile.soundHitThickRoof.PlayOneShot(base.Position);
					this.Destroy(DestroyMode.Vanish);
					return;
				}
			}
				//Modified
			if (this.assignedTarget != null && this.assignedTarget.Position == this.Position)	//it was aimed at something and that something is still there
			{
				this.ImpactThroughBodySize(this.assignedTarget);
				return;
			}
			else
			{
				Thing thing = Find.ThingGrid.ThingAt(base.Position, ThingCategory.Pawn);
				if (thing != null)
				{
					if (this.assignedTarget != null)
					{
						this.ImpactThroughBodySizeCheckWithTarget(thing);
						return;
					}
					this.Impact(thing);
					return;
				}
				List<Thing> list = Find.ThingGrid.ThingsListAt(base.Position);
				for (int i = 0; i < list.Count; i++)
				{
					Thing thing2 = list[i];
					if (thing2.def.fillPercent > 0f || thing2.def.passability != Traversability.Standable)
					{
						this.Impact(thing2);
						return;
					}
				}
				this.Impact(null);
				return;
			}
		}
		
		public override void Tick()
		{
				//I'm doing this cause base.Tick(); gives unwanted results - double hit detection and such
			ThingWithComps grandparent = this as ThingWithComps;
			grandparent.Tick();
			
			if (this.landed)
			{
				return;
			}
			Vector3 exactPosition = this.ExactPosition;
			this.ticksToImpact--;
			if (!this.ExactPosition.InBounds())
			{
				this.ticksToImpact++;
				base.Position = this.ExactPosition.ToIntVec3();
				this.Destroy(DestroyMode.Vanish);
				return;
			}
			Vector3 exactPosition2 = this.ExactPosition;
			if (!this.def.projectile.flyOverhead && this.canFreeIntercept && this.CheckForFreeInterceptBetween(exactPosition, exactPosition2))
			{
				return;
			}
			base.Position = this.ExactPosition.ToIntVec3();
			if ((float)this.ticksToImpact == 60f && Find.TickManager.CurTimeSpeed == TimeSpeed.Normal && this.def.projectile.soundImpactAnticipate != null)
			{
				this.def.projectile.soundImpactAnticipate.PlayOneShot(this);
			}
			if (this.ticksToImpact <= 0)
			{
				if (this.DestinationCell.InBounds())
				{
					base.Position = this.DestinationCell;
				}
				this.ImpactSomething();
				return;
			}
			if (this.ambientSustainer != null)
			{
				this.ambientSustainer.Maintain();
			}
		}
		
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.LookValue<bool>(ref this.targetDownedOnSpawn, "targetDownedOnSpawn", false, false);
		}
	}
}
