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
		private Vector2 recoilMagnitude = new Vector2(0.2f, 0.5f);
		private float shotAngle;
		private float shotHeight;
        
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
		
		private float DistanceTraveled(float velocity, float angle, float heightDifference)
		{
			float gravity = 9.8f;
			float distance = (float)((velocity * Math.Cos(angle)) / gravity) * (float)(velocity * Math.Sin(angle) + Math.Sqrt(Math.Pow(velocity * Math.Sin(angle), 2) + 2 * gravity * heightDifference));
			return distance;
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
            
            Log.Message("targetLoc after initialize: " + targetLoc.ToString());
            
            // Calculating recoil before use
            Vector2 recoil = new Vector2(0, 0);
            if (this.cpCustomGet != null)
            {
            	recoil = this.GetRecoilAmount();
	        	recoil *= (float)(1 - 0.015 * sourcePawn.skills.GetSkill(SkillDefOf.Shooting).level);	//very placeholder
            }
            
            	// ----------------------------------- STEP 1: Estimated location
            
            Vector3 shotVec = targetLoc - sourceLoc;    //Assigned for use in Estimated Location
            
            //Shift for weather/lighting/recoil
            float shiftDistance = 0;
            if (!this.caster.Position.Roofed() || !targetLoc.ToIntVec3().Roofed())  //Change to more accurate algorithm?
            {
            	shiftDistance += shotVec.magnitude * (1 - Find.WeatherManager.CurWeatherAccuracyMultiplier / 4);
            }
            if (Find.GlowGrid.PsychGlowAt(targetLoc.ToIntVec3()) == PsychGlow.Dark)
            {
                shiftDistance += shotVec.magnitude * 0.05f;
            }
            //First modification of the loc, a random rectangle
            targetLoc += new Vector3(Rand.Range(-shiftDistance, shiftDistance), 0, Rand.Range(-shiftDistance, shiftDistance));
            
            Log.Message("targetLoc after shifting: " + targetLoc.ToString());
			
            	// ----------------------------------- STEP 2: Estimated shot to hit location
            
            shotVec = targetLoc - sourceLoc;	//Updated for the estimation to hit Estimated Location 
            
            //Estimate range on first shot of burst
            if (this.verbProps.burstShotCount == this.burstShotsLeft)
            {
                float actualRange = Vector3.Distance(targetLoc, sourceLoc);
                float estimationDeviation = (this.cpCustomGet.scope ? 0.5f : 1f) * ((1 - this.shootingAccuracy) * actualRange);
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
            
            	// ----------------------------------- STEP 3: Recoil, Skewing, Skill checks, Cover calculations
            
            shotVec = targetLoc - sourceLoc;	//Reassigned for further calculations
            
            Vector2 skewVec = new Vector2(0, 0);
            Vector2 recoilVec = new Vector2(Rand.Range(-Math.Abs(recoil.x), Math.Abs(recoil.x)), Rand.Range(-Math.Abs(recoil.y), Math.Abs(recoil.y)));
            
            	//Height difference calculations for ShotAngle
            float heightDifference = 0;
            	Building cover = GridsUtility.GetEdifice((targetLoc + shotVec.normalized).ToIntVec3());
            	float targetableHeight = (targetPawn != null ? targetPawn.BodySize : (this.currentTarget.Thing != null ? this.currentTarget.Thing.def.fillPercent : 0));
	        	if (cover != null)
	        	{
	        		targetableHeight -= cover.def.fillPercent;
	        	}
	        	heightDifference += targetableHeight * 0.5f;		//Optimal hit level is halfway
	        	float shooterHeight = (sourcePawn != null ? sourcePawn.BodySize * 0.75f : (this.caster != null ? this.caster.def.fillPercent : 0));
	        	heightDifference -= shooterHeight;		//Assuming pawns shoot at 3/4ths of their body size
           	this.shotHeight = shooterHeight + heightDifference;
        	skewVec += new Vector2(0, ShotAngle(this.verbProps.projectileDef.projectile.speed, shotVec.magnitude, heightDifference));
            
            skewVec += (recoil + Vector2.Scale(recoilMagnitude, recoilVec));
            
           		//Get shootervariation
        	int prevSeed = Rand.Seed;
	        	Rand.Seed = this.caster.thingIDNumber;
	        	float rangeVariation = Rand.Range(0, 2);
        	Rand.Seed = prevSeed;
	        
	        int ticks = Find.TickManager.TicksAbs / 60;
	        float amplitude = (float)(1 - Math.Sqrt(1.2 - shootingAccuracy)) * (float)Math.Cos(5 * ticks);
	        Vector2 shooterVec = new Vector2(amplitude * (float)Math.Sin((ticks) + rangeVariation), amplitude * (float)Math.Sin((2 * ticks) + rangeVariation));
		        //sin(2*t)*(cos(5*t)-1)
		        //sin(2*t)*(cos(5*t)-1)*sin(t)
	        skewVec += shooterVec;
            
            	// ----------------------------------- STEP 4: Mechanical variation
            	
            //Get shotvariation
            Vector2 moaVec = new Vector2(0, 0);
            if(this.cpCustomGet.moaValue != 0)
            {
            	moaVec.Set(Rand.Range(-1, 1), Rand.Range(-1, 1));
	            moaVec = moaVec.normalized * (moaVec.magnitude < 1 ? moaVec.magnitude : 1) * this.cpCustomGet.moaValue;
            }
	       	skewVec += moaVec;
            
            Log.Message("combined Skew vector: " + skewVec.ToString() + " | recoil: "+recoilVec.ToString()+" ..+skill: "+shooterVec.ToString()+" ..+moa: "+moaVec.ToString());
			
            //Skewing		-		Applied after the leading calculations to not screw them up
            targetLoc = sourceLoc + (Quaternion.AngleAxis(skewVec.x, Vector3.up) * shotVec).normalized * DistanceTraveled(this.verbProps.projectileDef.projectile.speed, skewVec.y, heightDifference);
            
            this.shotAngle = skewVec.y;
            
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
            Vector3 targetVec3 = this.ShiftTarget();
            projectile.shotAngle = this.shotAngle;
            projectile.shotHeight = this.shotHeight;
            if (this.currentTarget.Thing != null)
            {
                projectile.Launch(this.caster, casterExactPosition, new TargetInfo(this.currentTarget.Thing), targetVec3, this.ownerEquipment);
            }
            else
            {
                projectile.Launch(this.caster, casterExactPosition, new TargetInfo(shootLine.Dest), targetVec3, this.ownerEquipment);
            }
            return true;
        }

        protected override bool TryCastShot()
        {
            return TryCastShot(this.verbProps.forcedMissRadius, 1);
        }
	}
}
