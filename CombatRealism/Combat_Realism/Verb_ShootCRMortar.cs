using RimWorld;
using UnityEngine;
using Verse;
namespace Combat_Realism
{
	public class Verb_ShootCRMortar : Verb_ShootCR
	{
        //Mortar accuracy peaks at 50% of max range
        protected override Vector3 ShiftTarget()
        {
            Vector3 targetLoc = base.ShiftTarget();
            float rangePercentage = (targetLoc.ToIntVec3() - this.caster.Position).LengthHorizontal / (this.verbProps.range / 2);
            float forcedMissRadius = this.verbProps.forcedMissRadius;
            forcedMissRadius *= rangePercentage <= 1 ? 1 - rangePercentage : (rangePercentage - 1) / 2;
            Vector3 shiftVec = (new Vector3(1, 0, 1) * Random.Range(0, forcedMissRadius)).RotatedBy(Random.Range(0, 360));
            return targetLoc + shiftVec;
        }
	}
}
