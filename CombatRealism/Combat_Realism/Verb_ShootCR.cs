using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

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
					return (float)Math.Pow(pawn.GetStatValue(StatDefOf.ShootingAccuracy, false), 2);
				}
				return 0.98f;
			}
		}

        private float aimingAccuracy
        {
            get
            {
                Pawn pawn = this.caster as Pawn;
                if (pawn != null)
                {
                    return (float)Math.Pow(pawn.GetStatValue(StatDef.Named("AimingAccuracy")), 2);
                }
                return 0.98f;
            }
        }
		
		private float GetShotAngle(float velocity, float range, float heightDifference)
		{
			float gravity = 9.8f;
			float angle = (float)Math.Atan((Math.Pow(velocity, 2) - Math.Sqrt(Math.Pow(velocity, 4) - gravity * (gravity * Math.Pow(range, 2) + 2 * heightDifference * Math.Pow(velocity, 2)))) / (gravity * range));
			return angle;
		}
		
		private float GetDistanceTraveled(float velocity, float angle, float heightDifference)
		{
			float gravity = 9.8f;
			float distance = (float)((velocity * Math.Cos(angle)) / gravity) * (float)(velocity * Math.Sin(angle) + Math.Sqrt(Math.Pow(velocity * Math.Sin(angle), 2) + 2 * gravity * heightDifference));
			return distance;
		}
		
        /// <summary>
        /// Shifts the original target position in accordance with target leading, range estimation and weather/lighting effects
        /// </summary>
        protected virtual Vector3 ShiftTarget()
        {
        		// ----------------------------------- STEP 0: Actual location
        	
            Pawn targetPawn = this.currentTarget.Thing as Pawn;
            Vector3 targetLoc = targetPawn != null ? targetPawn.DrawPos : this.currentTarget.Cell.ToVector3Shifted();
            Vector3 sourceLoc = this.CasterPawn != null ? this.CasterPawn.DrawPos : this.caster.Position.ToVector3Shifted();
            targetLoc.Scale(new Vector3(1, 0, 1));
            sourceLoc.Scale(new Vector3(1, 0, 1));
            	// ----------------------------------- STEP 1: Estimated location, target cover check

            Vector3 shotVec = targetLoc - sourceLoc;    //Assigned for use in Estimated Location
            
            //Shift for lighting
            float shiftDistance = shotVec.magnitude * Mathf.Lerp(0.05f, 0f, Find.GlowGrid.GameGlowAt(targetLoc.ToIntVec3()));

            //Shift for weather
            if (!this.caster.Position.Roofed() || !targetLoc.ToIntVec3().Roofed())  //Change to more accurate algorithm?
            {
                shiftDistance += shotVec.magnitude * (1 - Find.WeatherManager.CurWeatherAccuracyMultiplier) / 10;
            }
            
            //First modification of the loc, a random rectangle
            shiftDistance *= 1.5f - this.aimingAccuracy;
            Vector3 newTargetLoc = targetLoc;
            if (shiftDistance > 0)
            {
                newTargetLoc += new Vector3(UnityEngine.Random.Range(-shiftDistance, shiftDistance), 0, UnityEngine.Random.Range(-shiftDistance, shiftDistance));
            }
			
            	// ----------------------------------- STEP 2: Estimated shot to hit location
            
            shotVec = newTargetLoc - sourceLoc;	//Updated for the estimation to hit Estimated Location 
            
            //Estimate range on first shot of burst
            if (this.verbProps.burstShotCount == this.burstShotsLeft)
            {
                float actualRange = Vector3.Distance(newTargetLoc, sourceLoc);
                float estimationDeviation = ((1 - this.aimingAccuracy) * actualRange) * (2 - this.ownerEquipment.GetStatValue(StatDefOf.AccuracyLong) * 2);
                this.estimatedTargetDistance = Mathf.Clamp(Rand.Gaussian(actualRange, estimationDeviation / 3), actualRange - estimationDeviation, actualRange + estimationDeviation);
            }

            newTargetLoc = sourceLoc + shotVec.normalized * this.estimatedTargetDistance;
            
            //Lead a moving target
            if (targetPawn != null && targetPawn.pather != null && targetPawn.pather.Moving)
            {
                //Calculate current movement speed
                float targetSpeed = GetMoveSpeed(targetPawn);
                float timeToTarget = this.estimatedTargetDistance / this.verbProps.projectileDef.projectile.speed;
                float leadDistance = targetSpeed * timeToTarget;
                Vector3 moveVec = targetPawn.pather.nextCell.ToVector3() - Vector3.Scale(targetPawn.DrawPos, new Vector3(1, 0, 1));

                float leadVariation = (1 - aimingAccuracy) * (2 - this.ownerEquipment.GetStatValue(StatDefOf.AccuracyMedium) * 2);

                newTargetLoc += moveVec * (leadDistance + UnityEngine.Random.Range(-leadVariation, leadVariation));
            }
            
            	// ----------------------------------- STEP 3: Recoil, Skewing, Skill checks, Cover calculations
            
            shotVec = newTargetLoc - sourceLoc;	//Reassigned for further calculations
            
            Vector2 skewVec = new Vector2(0, 0);
            
            skewVec += this.GetRecoilVec();
            
            //Height difference calculations for ShotAngle
            float heightDifference = 0;
            float targetableHeight = Utility.GetCollisionHeight(this.currentTarget.Thing);
            Thing cover;
	        if (this.GetCoverBetween(sourceLoc, targetLoc, out cover))
                {
                    targetableHeight += cover.def.fillPercent;
                }
            heightDifference += targetableHeight * 0.5f;		//Optimal hit level is halfway
            this.shotHeight = (this.CasterPawn != null ? this.CasterPawn.BodySize * 0.75f : (this.caster != null ? this.caster.def.fillPercent : 0));
            heightDifference -= this.shotHeight;		//Assuming pawns shoot at 3/4ths of their body size
	        skewVec += new Vector2(0, GetShotAngle(this.verbProps.projectileDef.projectile.speed, shotVec.magnitude, heightDifference) * (180 / (float)Math.PI));
            
           	//Get shootervariation
	        int ticks = Find.TickManager.TicksAbs;
            float shooterAmplitude = 2.5f - shootingAccuracy;
            if (this.cpCustomGet != null)
            {
                shooterAmplitude *= cpCustomGet.shooterVariation;
            }
	        Vector2 shooterVec = new Vector2(shooterAmplitude * (float)Math.Sin(ticks * 2.2), 0.04f * shooterAmplitude * (float)Math.Sin(ticks * 1.65));
	        skewVec += shooterVec;
            
            	// ----------------------------------- STEP 4: Mechanical variation
            	
            //Get shotvariation
            Vector2 shotVarVec = new Vector2(0, 0);
            if(this.cpCustomGet.shotVariation != 0)
            {
                float shotVariation = this.cpCustomGet.shotVariation * (2f - this.ownerEquipment.GetStatValue(StatDefOf.AccuracyTouch) * 2);
                shotVarVec = Utility.GenRandInCircle(shotVariation);
                shotVarVec.x *= 3;
            }
            skewVec += shotVarVec;
			
            //Skewing		-		Applied after the leading calculations to not screw them up
            float distanceTraveled = GetDistanceTraveled(this.verbProps.projectileDef.projectile.speed, (float)(skewVec.y * (Math.PI / 180)), this.shotHeight);
            newTargetLoc = sourceLoc + ((newTargetLoc - sourceLoc).normalized * distanceTraveled);
            newTargetLoc = sourceLoc + (Quaternion.AngleAxis(skewVec.x, Vector3.up) * (newTargetLoc - sourceLoc));
            
            this.shotAngle = (float)(skewVec.y * (Math.PI / 180));
            
            
            return newTargetLoc;
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
                    int currentBurst = Math.Min(this.verbProps.burstShotCount - this.burstShotsLeft, 20);
                    recoilVec.Set(UnityEngine.Random.Range(recoilOffsetX.x, recoilOffsetX.y), UnityEngine.Random.Range(recoilOffsetY.x, recoilOffsetY.y));
                    recoilVec *= (float)Math.Sqrt((1 - shootingAccuracy) * currentBurst);
                }
            }
        	return recoilVec;
        }

        /// <summary>
        /// Calculates the actual current movement speed of a pawn
        /// </summary>
        private float GetMoveSpeed(Pawn pawn)
        {
            float movePerTick = 60 / pawn.GetStatValue(StatDefOf.MoveSpeed, false);    //Movement per tick
            movePerTick += PathGrid.CalculatedCostAt(pawn.Position, false);
            Building edifice = pawn.Position.GetEdifice();
            if (edifice != null)
            {
                movePerTick += (int)edifice.PathWalkCostFor(pawn);
            }

            //Case switch to handle walking, jogging, etc.
            switch (pawn.CurJob.locomotionUrgency)
            {
                case LocomotionUrgency.Amble:
                    movePerTick *= 3;
                    if (movePerTick < 60)
                    {
                        movePerTick = 60;
                    }
                    break;
                case LocomotionUrgency.Walk:
                    movePerTick *= 2;
                    if (movePerTick < 50)
                    {
                        movePerTick = 50;
                    }
                    break;
                case LocomotionUrgency.Jog:
                    break;
                case LocomotionUrgency.Sprint:
                    movePerTick = Mathf.RoundToInt(movePerTick * 0.75f);
                    break;
            }
            return 60 / movePerTick;
        }

        /// <summary>
        /// Checks for cover along the flight path of the bullet, doesn't check for walls, only intended for cover with partial fillPercent
        /// </summary>
        private bool GetCoverBetween(Vector3 sourceLoc, Vector3 targetLoc, out Thing cover)
        {
            sourceLoc.Scale(new Vector3(1, 0, 1));
            targetLoc.Scale(new Vector3(1, 0, 1));
            Thing targetThing = GridsUtility.GetEdifice(targetLoc.ToIntVec3());
            cover = GridsUtility.GetCover((targetLoc - (targetLoc - sourceLoc).normalized).ToIntVec3());
            if (!this.verbProps.projectileDef.projectile.flyOverhead 
                && cover != null 
                && !(targetThing != null && cover.Equals(targetThing)) 
                && cover.def.Fillage != FillCategory.Full
                && cover.def.category != ThingCategory.Plant)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the shooter can hit the target from a certain position with regards to cover height
        /// </summary>
        public override bool CanHitTargetFrom(IntVec3 root, TargetInfo targ)
        {
            if (base.CanHitTargetFrom(root, targ))
            {
                //Check if target is obstructed behind cover
                Thing coverTarg;
                if (this.GetCoverBetween(root.ToVector3Shifted(), targ.Cell.ToVector3Shifted(), out coverTarg))
                {
                    float targetHeight = Utility.GetCollisionHeight(targ.Thing);
                    if (targetHeight <= coverTarg.def.fillPercent)
                    {
                        return false;
                    }
                }
                //Check if shooter is obstructed by cover
                Thing coverShoot;
                if (this.GetCoverBetween(targ.Cell.ToVector3Shifted(), root.ToVector3Shifted(), out coverShoot))
                {
                    float shotHeight = Utility.GetCollisionHeight(this.caster);
                    Pawn casterPawn = this.caster as Pawn;
                    if (casterPawn != null)
                    {
                        shotHeight *= 0.75f;
                    }
                    if (shotHeight <= coverShoot.def.fillPercent)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Fires a projectile using a custom HitReportFor() method to override the vanilla one, as well as better collateral hit detection and adjustable range penalties and forcedMissRadius
        /// </summary>
        /// <returns>True for successful shot</returns>
        protected override bool TryCastShot()
        {
            ShootLine shootLine;
            if (!base.TryFindShootLineFromTo(this.caster.Position, this.currentTarget, out shootLine))
            {
                return false;
            }
            if (!this.CanHitTargetFrom(this.caster.Position, this.currentTarget))
            {
                return false;
            }
            Vector3 casterExactPosition = this.caster.DrawPos;
            ProjectileCR projectile = (ProjectileCR)ThingMaker.MakeThing(this.verbProps.projectileDef, null);
            GenSpawn.Spawn(projectile, shootLine.Source);
            float lengthHorizontalSquared = (this.currentTarget.Cell - this.caster.Position).LengthHorizontalSquared;

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
	}
}
