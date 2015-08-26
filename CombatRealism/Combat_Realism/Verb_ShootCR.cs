using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
namespace Combat_Realism
{
	public class Verb_ShootCR : Verse.Verb_Shoot
	{

        private float estimatedTargetDistance;  //Stores estimates target distance for each burst, so each burst shot uses the same
		private const float accuracyExponent = -2f;
        
		private CompPropertiesCustom cpCustom = null;
		private CompPropertiesCustom cpCustomGet
		{
			get
			{
				if (this.cpCustom == null)
				{
		            if (this.ownerEquipment != null && this.ownerEquipment.def.HasComp(typeof(CompAim)))
		            {
		                this.cpCustom = (CompPropertiesCustom)this.ownerEquipment.def.GetCompProperties(typeof(CompAim));
		            }
				}
				return this.cpCustom;
			}
		}
		
		private float shootingAccuracy
		{
			get
			{
				Pawn pawn = this.caster as Pawn;
				if (pawn != null)
				{
					return pawn.GetStatValue(StatDefOf.ShootingAccuracy, false);
				}
				return 0.98f;
			}
		}
		
		
        /// <summary>
        /// Shifts the original target position in accordance with target leading, range estimation and weather/lighting effects
        /// </summary>
        private Vector3 ShiftTarget()
        {
        		// ----------------------------------- STEP 0: Actual location
        	
            Pawn targetPawn = this.currentTarget.Thing as Pawn;
            Pawn sourcePawn = this.caster as Pawn;
            Vector3 targetLoc = targetPawn != null ? targetPawn.DrawPos : this.currentTarget.Cell.ToVector3();
            Vector3 sourceLoc = sourcePawn != null ? sourcePawn.DrawPos : this.caster.Position.ToVector3();
            targetLoc.Scale(new Vector3(1, 0, 1));
            sourceLoc.Scale(new Vector3(1, 0, 1));
            
            Vector3 shotVec = targetLoc - sourceLoc;    //Don't reassign this or silly things will happen
			
            float shootingAccuracy = sourcePawn.GetStatValue(StatDefOf.ShootingAccuracy, false);
            
            Log.Message("targetLoc after initialize: " + targetLoc.ToString());

            //Initialize cpCustom here so it can be called later on
            
            
            	// ----------------------------------- STEP 1: Estimated location
            
            //Shift for weather/lighting/recoil
            //float shiftDistance = this.GetRecoilAmount();
            //Log.Message("shiftDistance: " + shiftDistance.ToString());
            //if (!this.caster.Position.Roofed() || !targetLoc.ToIntVec3().Roofed())  //Change to more accurate algorithm?
            //{
            //    shiftDistance += targetDistance * 1 - Find.WeatherManager.CurWeatherAccuracyMultiplier;
            //}
            //if (Find.GlowGrid.PsychGlowAt(targetLoc.ToIntVec3()) == PsychGlow.Dark)
            //{
            //    shiftDistance += targetDistance * 0.2f;
            //}
            //Last modification of the loc, a random rectangle
            //targetLoc += new Vector3(Rand.Range(-shiftDistance, shiftDistance), 0, Rand.Range(-shiftDistance, shiftDistance));

            //Log.Message("targetLoc after shifting: " + targetLoc.ToString());
			
            	// ----------------------------------- STEP 2: Estimated shot to hit location
            
            //Estimate range
            
            //Estimate range on first shot of burst
            if (this.verbProps.burstShotCount == this.burstShotsLeft)
            {
                float actualRange = Vector3.Distance(targetLoc, sourceLoc);
                float estimationDeviation = (cpCustom.scope ? 0.5f : 1f) * (this.CasterIsPawn ? (1 - this.shootingAccuracy) * actualRange : 0.02f * actualRange);
                this.estimatedTargetDistance = Mathf.Clamp(Rand.Gaussian(actualRange, estimationDeviation / 3), actualRange - estimationDeviation, actualRange + estimationDeviation);
            }
            
            /*
            float estimationDeviation = (this.cpCustomGet.scope ? 0.5f : 1f) * (float)(Math.Pow(this.actualRange, 2) / (50 * 100)) * (float)Math.Pow((double)this.shootingAccuracy, this.accuracyExponent);
            this.rangeEstimate = Mathf.Clamp(Rand.Gaussian(this.actualRange, estimationDeviation), this.actualRange - (3 * estimationDeviation), this.actualRange + (3 * estimationDeviation));
            */
            
            targetLoc = sourceLoc + shotVec.normalized * this.estimatedTargetDistance;
			
            Log.Message("targetLoc after range: " + targetLoc.ToString());
			
            //Lead a moving target
            if (targetPawn != null && targetPawn.pather.Moving)
            {
                float timeToTarget = this.estimatedTargetDistance / this.verbProps.projectileDef.projectile.speed;
                float leadDistance = targetPawn.GetStatValue(StatDefOf.MoveSpeed, false) * timeToTarget;
                Vector3 moveVec = targetPawn.pather.nextCell.ToVector3() - targetPawn.DrawPos;
				
                float leadVariation = 0;
                if (this.CasterIsPawn)
                {
                    if (this.cpCustomGet != null)
                    {
                        leadVariation = this.cpCustomGet.scope ? (1 - shootingAccuracy) / 4 : 1 - shootingAccuracy;
                    }
                }
                //targetLoc += moveVec * Rand.Gaussian(leadDistance, leadDistance * leadVariation);		GAUSSIAN removed for now
                targetLoc += moveVec * (leadDistance + Rand.Range(-leadVariation, leadVariation));
            }
            
            Log.Message("targetLoc after lead: " + targetLoc.ToString());
			
            	// ----------------------------------- STEP 3: Recoil, start of skewing
           	
            float combinedSkew = 0;
            float recoilXAmount = 0;
            
            if (this.cpCustomGet != null)
            {
            	recoilXAmount = this.GetRecoilAmount();
	        	recoilXAmount *= (float)(1 - 0.015 * sourcePawn.skills.GetSkill(SkillDefOf.Shooting).level);	//very placeholder
            	//recoilSkew = Rand.Range(-recoilAmount / 4, recoilAmount / 4);
                //targetLoc += shotVec.normalized * Rand.Range(-recoilAmount / 2, recoilAmount);
            }
            
            	// ----------------------------------- STEP 4: Skill checks
            	
            //Get shootervariation
	        	int prevSeed = Rand.Seed;
		        	Rand.Seed = this.caster.thingIDNumber;
		        	float rangeVariation = Rand.Range(0, 2);
	        	Rand.Seed = prevSeed;
	        //float randomSkillSkew = (float)Math.Sin((Find.TickManager.TicksAbs / 60) + rangeVariation) * (float)Math.Log(Math.Pow(shootingAccuracy,-3), 8);
	        combinedSkew += (1 + recoilXAmount + 0.2f * Rand.Range(-recoilXAmount, recoilXAmount)) * (float)Math.Sin((Find.TickManager.TicksAbs / 60) + rangeVariation) * (float)(1 - Math.Sqrt(1.2 - shootingAccuracy));
	        
            Log.Message("recoil and skill Skew: " + combinedSkew.ToString());
            
            	// ----------------------------------- STEP 5: Mechanical variation
            	
            //Get shotvariation
            combinedSkew += this.cpCustomGet != null ? Rand.Range(-this.cpCustomGet.moaValue, this.cpCustomGet.moaValue) : 0;
			
            Log.Message("combined Skew: " + combinedSkew.ToString());

            //Skewing		-		Applied after the leading calculations to not screw them up
            targetLoc = sourceLoc + (Quaternion.AngleAxis(combinedSkew, Vector3.up) * (targetLoc - sourceLoc));	//THIS ONE REQUIRES UPDATED SHOTVECTOR

            Log.Message("targetLoc after skewing: " + targetLoc.ToString());
            
            return targetLoc;
        }
        
        /// <summary>
        /// Calculates the amount of recoil at a given point in a burst
        /// </summary>
        private float GetRecoilAmount()
        {
            float recoilAmount = 0;
        	int currentBurst = (this.verbProps.burstShotCount - this.burstShotsLeft) <= 10 ? (this.verbProps.burstShotCount - this.burstShotsLeft) - 1 : 10;
            if (this.cpCustomGet.recoilXOffset != 0)
            {
            	recoilAmount += this.cpCustomGet.recoilXOffset * (2 * (float)Math.Sqrt(0.1 * currentBurst)) * (this.CasterIsPawn ? 1 : 0.5f);
                /*if (this.CasterIsPawn)
                {
                    recoilAmount += offset * (float)Math.Pow(currentBurst + 3, 1 - this.CasterPawn.GetStatValue(StatDefOf.ShootingAccuracy));
                }
                else
                {
                    recoilAmount += offset * (float)Math.Pow(currentBurst + 3, 0.02f);
                }*/
            }
        	return recoilAmount;
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
