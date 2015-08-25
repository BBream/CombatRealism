/*
 * Created by SharpDevelop.
 * User: tijn
 * Date: 24-8-2015
 * Time: 11:52
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
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
		 * minute of angle / shot variation
		 * -- if (gun has shot variation)
		 * ---- math.rand(-variation, variation)
		 * -- else
		 * ---- calculate variation
		 * condition factored into shot variation
		 * optics improving ranged finding
		 * increase chance of hitting a downed pawn if the pawn was downed before the shot was fired
		 * -- Requires a scribe
		 * -- Track a private bool downedOnSpawn or something
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
			this.destination = targ.Cell.ToVector3Shifted() + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, Rand.Range(-0.3f, 0.3f));
			this.ticksToImpact = this.StartingTicksToImpact;
			if (!this.def.projectile.soundAmbient.NullOrUndefined())
			{
				SoundInfo info = SoundInfo.InWorld(this, MaintenanceType.PerTick);
				this.ambientSustainer = this.def.projectile.soundAmbient.TrySpawnSustainer(info);
			}
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
                        Pawn pawn = (Pawn)thing;
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
                        }
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
        private void ImpactThroughBodySize(Thing thing)
        {
        	Pawn pawn = thing as Pawn;
        	if (pawn != null)
        	{
        		this.Impact(
    				(pawn.Downed != this.targetDownedOnSpawn
    			 		? Rand.Value > (pawn.BodySize >= 1.6 ? (pawn.BodySize - 0.5) / pawn.BodySize : (pawn.def.race.Humanlike ? 0.8 : 0.7))
    			 		: (pawn.Downed ? Rand.Value > 0.93 : true))
    				? thing : null);
        		return;
        	}
        	this.Impact(thing);
        }
        
		private void ImpactSomething()
		{
				//Not modified
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
			if (this.assignedTarget != null)
			{
				this.ImpactThroughBodySize(this.assignedTarget);
				return;
			}
				//Slightly modified
			else
			{
				Thing thing = Find.ThingGrid.ThingAt(base.Position, ThingCategory.Pawn);
				if (thing != null)
				{
					this.ImpactThroughBodySize(thing);
					return;
				}
				List<Thing> list = Find.ThingGrid.ThingsListAt(base.Position);
				for (int i = 0; i < list.Count; i++)
				{
					Thing thing2 = list[i];
					if (thing2.def.fillPercent > 0f || thing2.def.passability != Traversability.Standable)
					{
						this.ImpactThroughBodySize(thing2);
						return;
					}
				}
				this.Impact(null);
				return;
			}
		}
		
		
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.LookValue<bool>(ref this.targetDownedOnSpawn, "targetDownedOnSpawn", false, false);
		}
	}
}
