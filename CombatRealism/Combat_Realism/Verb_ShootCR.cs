using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
namespace Combat_Realism
{
	public class Verb_ShootCR : Verse.Verb_Shoot
	{
        //Custom HitReportFor with scaleable range penalties
        public virtual HitReport HitReportForModRange(TargetInfo target)
        {
            return HitReportForModRange(target, 1);
        }

        public HitReport HitReportForModRange(TargetInfo target, float rangeFactor)
        {
            IntVec3 cell = target.Cell;
            HitReport hitReport = new HitReport();
            hitReport.shotDistance = (cell - this.caster.Position).LengthHorizontal;
            hitReport.target = target;
            if (!this.verbProps.canMiss)
            {
                hitReport.hitChanceThroughPawnStat = 0.99f; //Down from 1 so turrets no longer ignore range penalties
                hitReport.covers = new List<CoverInfo>();
                hitReport.coversOverallBlockChance = 0f;
            }
            else
            {
                float f = 1f;
                if (base.CasterIsPawn)
                {
                    f = base.CasterPawn.GetStatValue(StatDefOf.ShootingAccuracy, true);
                }
                hitReport.hitChanceThroughPawnStat = Mathf.Pow(f, hitReport.shotDistance * rangeFactor); //Modifiable long range accuracy
                if (hitReport.hitChanceThroughPawnStat < 0.0201f)
                {
                    hitReport.hitChanceThroughPawnStat = 0.0201f;
                }
                if (base.CasterIsPawn)
                {
                    hitReport.hitChanceThroughSightEfficiency = base.CasterPawn.health.capacities.GetEfficiency(PawnCapacityDefOf.Sight);
                }
                hitReport.hitChanceThroughEquipment = this.verbProps.HitMultiplierAtDist(hitReport.shotDistance, this.ownerEquipment);
                hitReport.forcedMissRadius = this.verbProps.forcedMissRadius;
                hitReport.covers = CoverUtility.CalculateCoverGiverSet(cell, this.caster.Position);
                hitReport.coversOverallBlockChance = CoverUtility.CalculateOverallBlockChance(cell, this.caster.Position);
                hitReport.targetLighting = Find.GlowGrid.PsychGlowAt(cell);
                if (!this.caster.Position.Roofed() && !target.Cell.Roofed())
                {
                    hitReport.hitChanceThroughWeather = Find.WeatherManager.CurWeatherAccuracyMultiplier;
                }
                if (target.HasThing)
                {
                    Pawn pawn = target.Thing as Pawn;
                    if (pawn != null)
                    {
                        float num = pawn.BodySize;
                        num = Mathf.Clamp(num, 0.5f, 2f);
                        hitReport.hitChanceThroughTargetSize = num;
                    }
                }
            }
            return hitReport;
        }

        /// <summary>
        /// Fires a projectile using a custom HitReportFor() method to override the vanilla one, as well as better collateral hit detection and adjustable range penalties and forcedMissRadius
        /// </summary>
        /// <returns>True for successful shot</returns>
        protected bool TryCastShot(float forcedMissRadius, float rangeFactor)
        {
            ShootLine shootLine;
            if (!base.TryFindShootLineFromTo(this.caster.Position, this.currentTarget, out shootLine))
            {
                return false;
            }
            Vector3 casterExactPosition = this.caster.DrawPos;
            Projectile projectile = (Projectile)ThingMaker.MakeThing(this.verbProps.projectileDef, null);
            GenSpawn.Spawn(projectile, shootLine.Source);
            float lengthHorizontalSquared = (this.currentTarget.Cell - this.caster.Position).LengthHorizontalSquared;

            //Forced Miss Calculations
            if (lengthHorizontalSquared < 9f)
            {
                forcedMissRadius = 0f;
            }
            else
            {
                if (lengthHorizontalSquared < 25f)
                {
                    forcedMissRadius *= 0.5f;
                }
                else
                {
                    if (lengthHorizontalSquared < 49f)
                    {
                        forcedMissRadius *= 0.8f;
                    }
                }
            }
            if (forcedMissRadius > 0.5f)
            {
                int max = GenRadial.NumCellsInRadius(forcedMissRadius);
                int rand = Rand.Range(0, max);
                if (rand > 0)
                {
                    IntVec3 newTarget = this.currentTarget.Cell + GenRadial.RadialPattern[rand];
                    projectile.canFreeIntercept = true;
                    TargetInfo target = newTarget;
                    if (!projectile.def.projectile.flyOverhead)
                    {
                        target = Utility.determineImpactPosition(this.caster.Position, newTarget, (int)(this.currentTarget.Cell - this.caster.Position).LengthHorizontal / 2);
                    }
                    projectile.Launch(this.caster, casterExactPosition, target, this.ownerEquipment);
                    if (this.currentTarget.HasThing)
                    {
                        projectile.AssignedMissTarget = this.currentTarget.Thing;
                    }
                    return true;
                }
            }

            HitReport hitReport = this.HitReportForModRange(this.currentTarget, rangeFactor);

            //Wild Shot
            if (Rand.Value > hitReport.TotalNonWildShotChance)
            {
                shootLine.ChangeDestToMissWild();
                projectile.canFreeIntercept = true;
                TargetInfo target = shootLine.Dest;
                if (!projectile.def.projectile.flyOverhead)
                {
                    target = Utility.determineImpactPosition(this.caster.Position, shootLine.Dest, (int)(this.currentTarget.Cell - this.caster.Position).LengthHorizontal / 2);
                }
                projectile.Launch(this.caster, casterExactPosition, target, this.ownerEquipment);
                return true;
            }

            //Cover Shot
            if (Rand.Value > hitReport.HitChanceThroughCover && this.currentTarget.Thing != null && this.currentTarget.Thing.def.category == ThingCategory.Pawn)
            {
                Thing thing = hitReport.covers.RandomElementByWeight((CoverInfo c) => c.BlockChance).Thing;
                projectile.canFreeIntercept = true;
                projectile.Launch(this.caster, casterExactPosition, new TargetInfo(thing), this.ownerEquipment);
                return true;
            }

            //Hit
            if (this.currentTarget.Thing != null)
            {
                projectile.Launch(this.caster, casterExactPosition, new TargetInfo(this.currentTarget.Thing), this.ownerEquipment);
            }
            else
            {
                projectile.Launch(this.caster, casterExactPosition, new TargetInfo(shootLine.Dest), this.ownerEquipment);
            }
            return true;
        }

        protected override bool TryCastShot()
        {
            return TryCastShot(this.verbProps.forcedMissRadius, 1);
        }
	}
}
