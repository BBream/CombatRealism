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
		 * 
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
	}
}
