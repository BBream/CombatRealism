using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
namespace Combat_Realism
{
	public class Verb_ShootCR : Verse.Verb_Shoot
	{
        private float ShooterRangeEstimation(Vector3 source, Vector3 target)
        {
            float actualRange = Vector3.Distance(target, source);
            return Rand.Gaussian(actualRange, (float)(Math.Pow(actualRange, 2) / (50 * 100)) * (float)Math.Pow((double)this.caster.GetStatValue(StatDefOf.ShootingAccuracy), -2));
        }

        /// <summary>
        /// Shifts the original target position in accordance with target leading, range estimation and weather/lighting effects
        /// </summary>
        private Vector3 ShiftTarget()
        {
            Vector3 targetLoc = this.currentTarget.Cell.ToVector3();
            float randomSkew = 0f;
            Vector3 sourceLoc = this.caster.Position.ToVector3();

            //Estimate range
            float targetDistance = ShooterRangeEstimation(sourceLoc, this.currentTarget.Cell.ToVector3());
            targetLoc = (this.currentTarget.Cell.ToVector3() - this.caster.DrawPos).normalized * targetDistance;

            //Get shotvariation
            if (this.ownerEquipment.def.HasComp(typeof(CompAim)))
            {
            	CompPropertiesCustom cpCustom = (CompPropertiesCustom)this.ownerEquipment.def.GetCompProperties(typeof(CompAim));
            	randomSkew += Rand.Range(-cpCustom.moaValue, cpCustom.moaValue);
            }
            
            //Get shootervariation
	        	int prevSeed = Rand.Seed;
		        	Rand.Seed = this.caster.thingIDNumber;
		        	float rangeVariation = Rand.Range(0, 2);
	        	Rand.Seed = prevSeed;
	        randomSkew += (float)Math.Sin((Find.TickManager.TicksAbs / 60) + rangeVariation) * (float)Math.Log(Math.Pow(this.caster.GetStatValue(StatDefOf.ShootingAccuracy),-3), 8);
            
            //Lead a moving target
            Pawn targetPawn = this.currentTarget.Thing as Pawn;
            if (targetPawn != null && targetPawn.pather.Moving)
            {
                targetLoc = targetPawn.DrawPos;

                float timeToTarget = targetDistance / this.verbProps.projectileDef.projectile.speed;
                float leadDistance = targetPawn.GetStatValue(StatDefOf.MoveSpeed, false) * timeToTarget;
                Vector3 moveVec = targetPawn.pather.nextCell.ToVector3() - targetPawn.DrawPos;

                float leadVariation = 0;
                if (this.CasterIsPawn)
                {
                    leadVariation = 1 - this.CasterPawn.GetStatValue(StatDefOf.ShootingAccuracy, false);
                }
                //targetLoc += moveVec * Rand.Gaussian(leadDistance, leadDistance * leadVariation);		GAUSSIAN removed for now
                targetLoc += moveVec * (leadDistance + Rand.Range(-leadVariation, leadVariation));
            }
            
            //Skewing		-		Applied after the leading calculations to not screw them up
            targetLoc = sourceLoc + (Quaternion.AngleAxis(randomSkew, Vector3.up) * (targetLoc - sourceLoc));

            //Shift for weather/lighting/recoil
            float shiftDistance = this.GetRecoilAmount();
            if (!this.caster.Position.Roofed() || !targetLoc.ToIntVec3().Roofed())  //Change to more accurate algorithm?
            {
                shiftDistance += targetDistance * 1 - Find.WeatherManager.CurWeatherAccuracyMultiplier;
            }
            if (Find.GlowGrid.PsychGlowAt(targetLoc.ToIntVec3()) == PsychGlow.Dark)
            {
                shiftDistance += targetDistance * 0.2f;
            }
            //Last modification of the loc, a random rectangle
            targetLoc += new Vector3(Rand.Range(-shiftDistance, shiftDistance), 0, Rand.Range(-shiftDistance, shiftDistance));
            
            return targetLoc;
        }
        
        /// <summary>
        /// Calculates the amount of recoil at a given point in a burst
        /// </summary>
        private float GetRecoilAmount()
        {
            float recoilAmount = 0;
        	int currentBurst = (this.verbProps.burstShotCount - this.burstShotsLeft) <= 10 ? (this.verbProps.burstShotCount - this.burstShotsLeft) - 1 : 10;
            if (this.verbProps.forcedMissRadius > 0)
            {
                if (this.CasterIsPawn)
                {
                    recoilAmount += this.verbProps.forcedMissRadius * (1 - this.CasterPawn.GetStatValue(StatDefOf.ShootingAccuracy)) * currentBurst;
                }
                else
                {
                    recoilAmount += this.verbProps.forcedMissRadius * 0.02f * currentBurst;
                }
            }
        	return recoilAmount;
        }
        
        /// <summary>
        /// Fires a projectile using a custom HitReportFor() method to override the vanilla one, as well as better collateral hit detection and adjustable range penalties and forcedMissRadius
        /// </summary>
        /// <returns>True for successful shot</returns>
        protected bool TryCastShot(float forcedMissRadius, float rangeFactor)
        {
            /*
             * Things to add:
             * 
             * shooter inaccuracy,
             * 		++ Alistaire did this
             * shooter ability to estimate range,
             * 		++ Alistaire/NoImageAvailable did this
             * shooter ability to lead,
             * 		++ NoImageAvailable did this
             * additional inaccuracies from weather and lighting
             * 		++ NoImageAvailable did this
             * recoil
             * 		-- NoImageAvailable started this
             * shooter ability to handle recoil
             * 		-- NoImageAvailable started this
             */

            ShootLine shootLine;
            if (!base.TryFindShootLineFromTo(this.caster.Position, this.currentTarget, out shootLine))
            {
                return false;
            }
            Vector3 casterExactPosition = this.caster.DrawPos;
            ProjectileCR projectile = (ProjectileCR)ThingMaker.MakeThing(this.verbProps.projectileDef, null);
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

            //New aiming algorithm
            projectile.canFreeIntercept = true;
            if (this.currentTarget.Thing != null)
            {
                projectile.Launch(this.caster, casterExactPosition, new TargetInfo(this.currentTarget.Thing), this.ShiftTarget(), this.ownerEquipment);
            }
            else
            {
                projectile.Launch(this.caster, casterExactPosition, new TargetInfo(shootLine.Dest), this.ShiftTarget(), this.ownerEquipment);
            }
            return true;
        }

        protected override bool TryCastShot()
        {
            return TryCastShot(this.verbProps.forcedMissRadius, 1);
        }
	}
}
