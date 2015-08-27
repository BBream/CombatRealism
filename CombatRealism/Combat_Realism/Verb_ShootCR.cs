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
		
		private float ShotAngle(float velocity, float range, float heightDifference)
		{
			float gravity = 9.8f;
			float angle = (float)Math.Atan((Math.Pow(velocity, 2) - Math.Sqrt(Math.Pow(velocity, 4) - gravity * (gravity * Math.Pow(range, 2) + 2 * heightDifference * Math.Pow(velocity, 2)))) / (gravity * range));
			angle *= 180 / (float)Math.PI;
			return angle;
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
            
            Vector3 shotVec = targetLoc - sourceLoc;
            
            Log.Message("targetLoc after initialize: " + targetLoc.ToString());
            
            // Calculating recoil before use
            Vector2 recoil = new Vector2(0, 0);
            if (this.cpCustomGet != null)
            {
            	recoil = this.GetRecoilAmount();
	        	recoil *= (float)(1 - 0.015 * sourcePawn.skills.GetSkill(SkillDefOf.Shooting).level);	//very placeholder
            	//recoilSkew = Rand.Range(-recoilAmount / 4, recoilAmount / 4);
                //targetLoc += shotVec.normalized * Rand.Range(-recoilAmount / 2, recoilAmount);
            }
            
            	// ----------------------------------- STEP 1: Estimated location
            
            //Shift for lighting
            float shiftDistance = shotVec.magnitude * Mathf.Lerp(0.05f, 0f, Find.GlowGrid.GameGlowAt(targetLoc.ToIntVec3()));
            Log.Message("Target lighting: " + Find.GlowGrid.GameGlowAt(targetLoc.ToIntVec3()).ToString());
            Log.Message("Shift after lighting: " + shiftDistance.ToString());

            //Shift for weather
            if (!this.caster.Position.Roofed() || !targetLoc.ToIntVec3().Roofed())  //Change to more accurate algorithm?
            {
                shiftDistance += shotVec.magnitude * (1 - Find.WeatherManager.CurWeatherAccuracyMultiplier) / 10;
                Log.Message("Weather accuracy mult: " + Find.WeatherManager.CurWeatherAccuracyMultiplier.ToString());
                Log.Message("Shift after weather: " + shiftDistance.ToString());
            }

            //First modification of the loc, a random rectangle
            if (shiftDistance > 0)
            {
                targetLoc += new Vector3(Rand.Range(-shiftDistance, shiftDistance), 0, Rand.Range(-shiftDistance, shiftDistance));
                shotVec = targetLoc - sourceLoc;
            }

            Log.Message("targetLoc after shifting: " + targetLoc.ToString());
			
            	// ----------------------------------- STEP 2: Estimated shot to hit location
            
            //Estimate range on first shot of burst
            if (this.verbProps.burstShotCount == this.burstShotsLeft)
            {
                float actualRange = Vector3.Distance(targetLoc, sourceLoc);
                float estimationDeviation = ((1 - this.shootingAccuracy) * actualRange) * (1.5f - this.verbProps.accuracyLong);
                this.estimatedTargetDistance = Mathf.Clamp(Rand.Gaussian(actualRange, estimationDeviation / 3), actualRange - estimationDeviation, actualRange + estimationDeviation);
            }
            
            /*
            float estimationDeviation = (this.cpCustomGet.scope ? 0.5f : 1f) * (float)(Math.Pow(this.actualRange, 2) / (50 * 100)) * (float)Math.Pow((double)this.shootingAccuracy, this.accuracyExponent);
            this.rangeEstimate = Mathf.Clamp(Rand.Gaussian(this.actualRange, estimationDeviation), this.actualRange - (3 * estimationDeviation), this.actualRange + (3 * estimationDeviation));
            */
            
            targetLoc = sourceLoc + shotVec.normalized * this.estimatedTargetDistance;
			
            Log.Message("targetLoc after range: " + targetLoc.ToString());
			
            targetLoc += shotVec.normalized * (recoil.y + 0.5f * Rand.Range(-Math.Abs(recoil.y), Math.Abs(recoil.y)));
            
            Log.Message("targetLoc after recoil: " + targetLoc.ToString());
            
            //Lead a moving target
            if (targetPawn != null && targetPawn.pather.Moving)
            {
                float timeToTarget = this.estimatedTargetDistance / this.verbProps.projectileDef.projectile.speed;
                float leadDistance = targetPawn.GetStatValue(StatDefOf.MoveSpeed, false) * timeToTarget;
                Vector3 moveVec = targetPawn.pather.nextCell.ToVector3() - Vector3.Scale(targetPawn.DrawPos, new Vector3(1, 0, 1));

                float leadVariation = (1 - shootingAccuracy) * (1.5f - this.verbProps.accuracyMedium);

                //targetLoc += moveVec * Rand.Gaussian(leadDistance, leadDistance * leadVariation);		GAUSSIAN removed for now
                targetLoc += moveVec * (leadDistance + Rand.Range(-leadVariation, leadVariation));
            }
            
            Log.Message("targetLoc after lead: " + targetLoc.ToString());
			
            shotVec = targetLoc - sourceLoc;	//Reassigned for further calculations
            
            float angleRequired = ShotAngle(this.verbProps.projectileDef.projectile.speed, this.estimatedTargetDistance, 0f);
            
            	// ----------------------------------- STEP 3: Recoil, Skewing, Skill checks
            	
            float combinedSkew = 0;
            
            //Get shootervariation
	        	int prevSeed = Rand.Seed;
		        	Rand.Seed = this.caster.thingIDNumber;
		        	float rangeVariation = Rand.Range(0, 2);
	        	Rand.Seed = prevSeed;
	        //float randomSkillSkew = (float)Math.Sin((Find.TickManager.TicksAbs / 60) + rangeVariation) * (float)Math.Log(Math.Pow(shootingAccuracy,-3), 8);
	        
	        //recoilXAmount = 1 (to the right)
	        //shooterVariation = 
	        
	        combinedSkew += (recoil.x + 0.6f * Rand.Range(-Math.Abs(recoil.x), Math.Abs(recoil.x))) + (float)Math.Sin((Find.TickManager.TicksAbs / 60) + rangeVariation) * (float)(1 - Math.Sqrt(1.2 - shootingAccuracy));
	        
            Log.Message("recoil and skill Skew: " + combinedSkew.ToString());
            
            	// ----------------------------------- STEP 4: Mechanical variation
            	
            //Get shotvariation
            float effectiveMoa = this.cpCustomGet.moaValue * (1.5f - this.verbProps.accuracyShort);
            combinedSkew += this.cpCustomGet != null ? Rand.Range(-effectiveMoa, effectiveMoa) : 0;
			
            Log.Message("combined Skew: " + combinedSkew.ToString());

            //Skewing		-		Applied after the leading calculations to not screw them up
            targetLoc = sourceLoc + (Quaternion.AngleAxis(combinedSkew, Vector3.up) * shotVec);	//THIS ONE REQUIRES UPDATED SHOTVECTOR

            Log.Message("targetLoc after skewing: " + targetLoc.ToString());
            
            return targetLoc;
        }
        
        /// <summary>
        /// Calculates the amount of recoil at a given point in a burst
        /// </summary>
        private Vector2 GetRecoilAmount()
        {
        	Vector2 recoilAmount = new Vector2(0, 0);
        	Vector2 recoilOffset = this.cpCustomGet.recoilOffset;
			int currentBurst = (this.verbProps.burstShotCount - this.burstShotsLeft) <= 11 ? (this.verbProps.burstShotCount - this.burstShotsLeft) : 11;
			if (!(recoilOffset.x == 0 && recoilOffset.y == 0))
            {
            	recoilAmount += this.cpCustomGet.recoilOffset * (2 * (float)Math.Sqrt(0.1 * currentBurst)) * (this.CasterIsPawn ? 1 : 0.5f);
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
