using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Combat_Realism
{
    class Utility
    {
        /// <summary>
        /// Generates a random Vector2 in a circle with given radius
        /// </summary>
        public static Vector2 GenRandInCircle(float radius)
        {
            //Fancy math to get random point in circle
            float a = 1 - Random.Range(0f, 1f);
            float b = 1 - Random.Range(0f, 1f);
            if (b < a)
            {
                float c = a;
                a = b;
                b = c;
            }
            return new Vector2((float)(b * radius * Mathf.Cos(2 * Mathf.PI * a / b)), (float)(b * radius * Mathf.Sin(2 * Mathf.PI * a / b)));
        }

        //------------------------------ Physics Calculations ------------------------------

        public const float gravityConst = 9.8f;
        public const float collisionHeightFactor = 0.35f;
        /// <summary>
        /// Returns the collision height of a Thing
        /// </summary>
        public static float GetCollisionHeight(Thing thing)
        {
            if (thing == null)
            {
                return 0;
            }
            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                float collisionHeight = pawn.BodySize;
                if (pawn.GetPosture() != PawnPosture.Standing)
                {
                    collisionHeight = pawn.BodySize > 1 ? pawn.BodySize - 0.8f : 0.8f * pawn.BodySize;
                }
                return collisionHeight * collisionHeightFactor;
            }
            return thing.def.fillPercent * collisionHeightFactor;
        }

        //------------------------------ Armor Calculations ------------------------------

        /// <summary>
        /// Calculates deflection chance and damage through armor
        /// </summary>
        public static int GetAfterArmorDamage(Pawn pawn, int amountInt, BodyPartRecord part, DamageInfo dinfo, ref bool deflected)
        {
            DamageDef damageDef = dinfo.Def;
            if (damageDef.armorCategory == DamageArmorCategory.IgnoreArmor)
            {
                return amountInt;
            }

            float damageAmount = (float)amountInt;
            StatDef deflectionStat = damageDef.armorCategory.DeflectionStat();
            float pierceAmount = 0f;

            //Check if the projectile has the armor-piercing comp
            ThingDef projectile = dinfo.Source.Verbs.Where(x => x.isPrimary).First().projectileDef;
            CompProperties_AP props = null;
            if (projectile != null && projectile.HasComp(typeof(CompAP)))
            {
                props = (CompProperties_AP)projectile.GetCompProperties(typeof(CompAP));
            }
            else if (dinfo.Source.HasComp(typeof(CompAP)))
            {
                props = (CompProperties_AP)dinfo.Source.GetCompProperties(typeof(CompAP));
            }
            if (props != null)
            {
                pierceAmount = props.armorPenetration;
            }

            //Run armor calculations on all apparel and pawn
            if (pawn.apparel != null)
            {
                List<Apparel> wornApparel = pawn.apparel.WornApparel;
                foreach (Apparel apparel in wornApparel)
                {
                    if (apparel.def.apparel.CoversBodyPart(part))
                    {
                        deflected = Utility.ApplyArmor(ref damageAmount, apparel.GetStatValue(deflectionStat, true), apparel, damageDef, pierceAmount);
                        if (damageAmount < 0.001)
                        {
                            return 0;
                        }
                    }
                }
            }
            deflected = Utility.ApplyArmor(ref damageAmount, pawn.GetStatValue(deflectionStat, true), null, damageDef, pierceAmount);
            return Mathf.RoundToInt(damageAmount);
        }

        private static bool ApplyArmor(ref float damAmount, float armorRating, Thing armorThing, DamageDef damageDef, float pierceAmount)
        {
            float deflectionChance = Mathf.Clamp(0.5f + (pierceAmount - armorRating) * 2, 0, 1);

            //Shot is deflected
            if (deflectionChance > 0 && Rand.Value > deflectionChance)
            {
                damAmount *= armorRating;
                return true;
            }


            return false;
        }

    }
}
