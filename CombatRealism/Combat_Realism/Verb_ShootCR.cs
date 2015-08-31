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
			return angle;
		}
		
		private float DistanceTraveled(float velocity, float angle, float heightDifference)
		{
			float gravity = 9.8f;
			float distance = (float)((velocity * Math.Cos(angle)) / gravity) * (float)(velocity * Math.Sin(angle) + Math.Sqrt(Math.Pow(velocity * Math.Sin(angle), 2) + 2 * gravity * heightDifference));
			Log.Message("v=" + velocity + " a=" + angle + " h0="+heightDifference+" d="+distance);
			return distance;
		}
		
        /// <summary>
        /// Shifts the original target position in accordance with target leading, range estimation and weather/lighting effects
        /// </summary>
        private Vector3 ShiftTarget()
        {
        		// ----------------------------------- STEP 0: Actual location
        	
            Pawn targetPawn = this.currentTarget.Thing as Pawn;
            Vector3 targetLoc = targetPawn != null ? targetPawn.DrawPos : this.currentTarget.Cell.ToVector3();
            Vector3 sourceLoc = this.CasterPawn != null ? this.CasterPawn.DrawPos : this.caster.Position.ToVector3();
            targetLoc.Scale(new Vector3(1, 0, 1));
            sourceLoc.Scale(new Vector3(1, 0, 1));
            
            Log.Message("targetLoc after initialize: " + targetLoc.ToString());
            
            /*
            // Calculating recoil before use
            Vector2 recoil = new Vector2(0, 0);
            if (this.cpCustomGet != null)
            {
            	recoil = this.GetRecoilVec();
                recoil *= (float)(1 - 0.015 * this.shootingAccuracy);	//very placeholder
            }
             */
            
            	// ----------------------------------- STEP 1: Estimated location
            
            Vector3 shotVec = targetLoc - sourceLoc;    //Assigned for use in Estimated Location
            
            //Shift for lighting
            float shiftDistance = shotVec.magnitude * Mathf.Lerp(0.05f, 0f, Find.GlowGrid.GameGlowAt(targetLoc.ToIntVec3()));
            //Log.Message("Target lighting: " + Find.GlowGrid.GameGlowAt(targetLoc.ToIntVec3()).ToString());
            //Log.Message("Shift after lighting: " + shiftDistance.ToString());

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
            }
            
            //Log.Message("targetLoc after shifting: " + targetLoc.ToString());
			
            	// ----------------------------------- STEP 2: Estimated shot to hit location
            
            shotVec = targetLoc - sourceLoc;	//Updated for the estimation to hit Estimated Location 
            
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
			
            //Log.Message("targetLoc after range: " + targetLoc.ToString());
            
            //Lead a moving target
            if (targetPawn != null && targetPawn.pather != null && targetPawn.pather.Moving)
            {
                float timeToTarget = this.estimatedTargetDistance / this.verbProps.projectileDef.projectile.speed;
                float leadDistance = targetPawn.GetStatValue(StatDefOf.MoveSpeed, false) * timeToTarget;
                Vector3 moveVec = targetPawn.pather.nextCell.ToVector3() - Vector3.Scale(targetPawn.DrawPos, new Vector3(1, 0, 1));

                float leadVariation = (1 - shootingAccuracy) * (1.5f - this.verbProps.accuracyMedium);

                //targetLoc += moveVec * Rand.Gaussian(leadDistance, leadDistance * leadVariation);		GAUSSIAN removed for now
                targetLoc += moveVec * (leadDistance + Rand.Range(-leadVariation, leadVariation));
            }
            
            //Log.Message("targetLoc after lead: " + targetLoc.ToString());
            
            	// ----------------------------------- STEP 3: Recoil, Skewing, Skill checks, Cover calculations
            
            shotVec = targetLoc - sourceLoc;	//Reassigned for further calculations
            
            Vector2 skewVec = new Vector2(0, 0);
            
            skewVec += this.GetRecoilVec();
            
            	//Height difference calculations for ShotAngle
            float heightDifference = 0;
            	Building cover = GridsUtility.GetEdifice((targetLoc + shotVec.normalized).ToIntVec3());
            	float targetableHeight = (targetPawn != null ? targetPawn.BodySize : (this.currentTarget.Thing != null ? this.currentTarget.Thing.def.fillPercent : 0));
	        	if (cover != null)
	        	{
	        		targetableHeight -= cover.def.fillPercent;
	        	}
	        	heightDifference += targetableHeight * 0.5f;		//Optimal hit level is halfway
                this.shotHeight = (this.CasterPawn != null ? this.CasterPawn.BodySize * 0.75f : (this.caster != null ? this.caster.def.fillPercent : 0));
	        	heightDifference -= this.shotHeight;		//Assuming pawns shoot at 3/4ths of their body size
	        skewVec += new Vector2(0, ShotAngle(this.verbProps.projectileDef.projectile.speed, shotVec.magnitude, heightDifference) * (180 / (float)Math.PI));
            
           		//Get shootervariation
        	int prevSeed = Rand.Seed;
	        	Rand.Seed = this.caster.thingIDNumber;
	        	float rangeVariation = Rand.Range(0, 2);
        	Rand.Seed = prevSeed;
	        
	        int ticks = Find.TickManager.TicksAbs / 60;
	        float shooterAmplitude = (float)(1 - Math.Sqrt(1.2 - shootingAccuracy));
	        Vector2 shooterVec = new Vector2(shooterAmplitude * (float)Math.Sin(ticks + rangeVariation), 0.5f * shooterAmplitude * (float)Math.Sin((2 * ticks) + rangeVariation));
		        //sin(2*t)*(cos(5*t)-1)
		        //sin(2*t)*(cos(5*t)-1)*sin(t)
	        skewVec += shooterVec;
            
            	// ----------------------------------- STEP 4: Mechanical variation
            	
            //Get shotvariation
            Vector2 shotVarVec = new Vector2(0, 0);
            if(this.cpCustomGet.shotVariation != 0)
            {
            	shotVarVec.Set(Rand.Range(-1, 1), Rand.Range(-1, 1));
            	float shotVarAmplitude = shotVarVec.magnitude * this.cpCustomGet.shotVariation * (1.5f - this.verbProps.accuracyShort);
	            shotVarVec = shotVarVec.normalized * shotVarAmplitude * this.cpCustomGet.shotVariation;
            }
	       	skewVec += shotVarVec;
            
            Log.Message("combined Skew vector: " + skewVec.ToString() + " ..+recoilVec: "+this.GetRecoilVec().ToString()+" ..+skill: "+shooterVec.ToString()+" ..+moa: "+shotVarVec.ToString());
			
            //Skewing		-		Applied after the leading calculations to not screw them up
            float distanceTraveled = DistanceTraveled(this.verbProps.projectileDef.projectile.speed, (float)(skewVec.y * (Math.PI / 180)), this.shotHeight);
            Log.Message("DistanceTraveled "+distanceTraveled.ToString());
            targetLoc = sourceLoc + ((targetLoc - sourceLoc).normalized * distanceTraveled);
            Log.Message("targetLoc Calculation: "+sourceLoc.ToString()+" + ("+(targetLoc - sourceLoc).normalized+" * "+distanceTraveled+")");
            Log.Message("targetLoc after distance adjusting"+targetLoc.ToString());
            targetLoc = sourceLoc + (Quaternion.AngleAxis(skewVec.x, Vector3.up) * (targetLoc - sourceLoc));
            Log.Message("targetLoc after skewing: " + targetLoc.ToString());
            
            this.shotAngle = (float)(skewVec.y * (Math.PI / 180));
            
            
            return targetLoc;
        }
        
        /// <summary>
        /// Calculates the amount of recoil at a given point in a burst
        /// </summary>
        private Vector2 GetRecoilVec()
        {
        	Vector2 recoilVec = new Vector2(0, 0);
            if (this.cpCustomGet != null)
            {
                Vector2 recoilOffsetX = this.cpCustomGet.recoilOffsetX;
                Vector2 recoilOffsetY = this.cpCustomGet.recoilOffsetY;
                if (!(recoilOffsetX.Equals(Vector2.zero) && recoilOffsetY.Equals(Vector2.zero)))
                {
                    int currentBurst = Math.Min(this.verbProps.burstShotCount - this.burstShotsLeft, 10);
                    recoilVec.Set(Rand.Range(recoilOffsetX.x, recoilOffsetX.y), Rand.Range(recoilOffsetY.x, recoilOffsetY.y));
                    recoilVec *= (float)Math.Sqrt((1 - shootingAccuracy) * currentBurst);

                    /*if (this.CasterIsPawn)
                    {
                        recoilAmount += offset * (float)Math.Pow(currentBurst + 3, 1 - this.CasterPawn.GetStatValue(StatDefOf.ShootingAccuracy));
                    }
                    else
                    {
                        recoilAmount += offset * (float)Math.Pow(currentBurst + 3, 0.02f);
                    }*/
                }
            }
        	return recoilVec;
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
