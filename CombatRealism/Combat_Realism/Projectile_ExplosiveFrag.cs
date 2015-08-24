using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;   // Always needed
using RimWorld;      // RimWorld specific functions are found here
using Verse;         // RimWorld universal objects are here
//using Verse.AI;    // Needed when you do something with the AI
using Verse.Sound;   // Needed when you do something with the Sound

namespace Combat_Realism
{
    /// <summary>
    /// Explosive with fragmentation effect
    /// </summary>
	public class Projectile_ExplosiveFrag : Projectile_Explosive
	{
        //frag variables
        private int fragAmountSmall = 0;
        private int fragAmountMedium = 0;
        private int fragAmountLarge = 0;

        private float fragRange = 0;

        private ThingDef fragProjectileSmall = null;
        private ThingDef fragProjectileMedium = null;
        private ThingDef fragProjectileLarge = null;

        /// <summary>
        /// Read parameters from XML file
        /// </summary>
        /// <returns>True if parameters are in order, false otherwise</returns>
        public bool getParameters()
        {
            Combat_Realism.ThingDef_ProjectileFrag projectileDef = this.def as Combat_Realism.ThingDef_ProjectileFrag;
            if (projectileDef.fragAmountSmall + projectileDef.fragAmountMedium + projectileDef.fragAmountLarge > 0
                && projectileDef.fragRange > 0
                && projectileDef.fragProjectileSmall != null
                && projectileDef.fragProjectileMedium != null
                && projectileDef.fragProjectileLarge != null)
            {
                this.fragAmountSmall = projectileDef.fragAmountSmall;
                this.fragAmountMedium = projectileDef.fragAmountMedium;
                this.fragAmountLarge = projectileDef.fragAmountLarge;

                this.fragRange = projectileDef.fragRange;

                this.fragProjectileSmall = projectileDef.fragProjectileSmall;
                this.fragProjectileMedium = projectileDef.fragProjectileMedium;
                this.fragProjectileLarge = projectileDef.fragProjectileLarge;

                return true;
            }
            return false;
        }

        /// <summary>
        /// Scatters fragments around
        /// </summary>
        protected virtual void ScatterFragments(int radius, ThingDef projectileDef)
        {
            Projectile projectile = (Projectile)ThingMaker.MakeThing(projectileDef, null);
            int rand = Rand.Range(0, radius);
            TargetInfo targetCell = this.Position + GenRadial.RadialPattern[rand];
            TargetInfo target = Utility.determineImpactPosition(this.Position, targetCell);
            GenSpawn.Spawn(projectile, this.Position);
            projectile.Launch(this, target, this.launcher);
        }

        /// <summary>
        /// Explode and scatter fragments around
        /// </summary>
		protected override void Explode()
        {
            if (this.getParameters())
            {
                int radius = GenRadial.NumCellsInRadius(this.fragRange);

                //Spawn projectiles
                for (int i = 0; i < fragAmountSmall; i++)
                {
                    this.ScatterFragments(radius, this.fragProjectileSmall);
                }
                for (int i = 0; i < fragAmountMedium; i++)
                {
                    this.ScatterFragments(radius, this.fragProjectileMedium);
                }
                for (int i = 0; i < fragAmountLarge; i++)
                {
                    this.ScatterFragments(radius, this.fragProjectileLarge);
                }
            }
            base.Explode();
		}
	}
}
