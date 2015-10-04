using System;
using Verse;
using Verse.Sound;
using RimWorld;

namespace Combat_Realism
{
    public class BulletCR : ProjectileCR
    {
        private const float StunChance = 0.1f;
        protected override void Impact(Thing hitThing)
        {
            base.Impact(hitThing);
            if (hitThing != null)
            {
        		float height = this.GetProjectileHeight(this.shotHeight, this.distanceFromOrigin, this.shotAngle, this.shotSpeed);
                int damageAmountBase = this.def.projectile.damageAmountBase;
                BodyPartHeight? bodyPartHeight = null;
                Pawn pawn = hitThing as Pawn;
                if (pawn != null && pawn.GetPosture() == PawnPosture.Standing)	//Downed pawns randomly get damaged wherever, I guess
                {
	                float fullHeight = Utility.GetCollisionHeight(hitThing);
	                float percentOfBodySize = height / fullHeight;
	                if (percentOfBodySize >= 0.8)
	                {
	                	bodyPartHeight = BodyPartHeight.Top;
	                }
	                else
	                {
	                	if (percentOfBodySize < 0.45)
	                	{
	                		bodyPartHeight = BodyPartHeight.Bottom;
	                	}
	                	else
	                	{
	                		bodyPartHeight = BodyPartHeight.Middle;
	                	}
	                }
                }
                	//All the rest is handled further on - Even the BodyPartHeight not existing doesn't matter.
                BodyPartDamageInfo value = new BodyPartDamageInfo(bodyPartHeight, null);
                DamageInfo dinfo = new DamageInfo(this.def.projectile.damageDef, damageAmountBase, this.launcher, this.ExactRotation.eulerAngles.y, new BodyPartDamageInfo?(value), this.equipmentDef);
                hitThing.TakeDamage(dinfo);
            }
            else
            {
                SoundDefOf.BulletImpactGround.PlayOneShot(base.Position);
                MoteThrower.ThrowStatic(this.ExactPosition, ThingDefOf.Mote_ShotHit_Dirt, 1f);
            }
        }
    }
}
