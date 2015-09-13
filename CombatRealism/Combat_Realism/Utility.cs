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
                    collisionHeight = pawn.BodySize > 1 ? pawn.BodySize - 0.8f : 0.2f * pawn.BodySize;
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
            CompProperties_AP props = null;
            VerbProperties verbProps = dinfo.Source.Verbs.Where(x => x.isPrimary).First();
            if (verbProps != null)
            {
                ThingDef projectile = verbProps.projectileDef;
                if (projectile != null && projectile.HasComp(typeof(CompAP)))
                {
                    props = (CompProperties_AP)projectile.GetCompProperties(typeof(CompAP));
                }
            }

            //Check weapon for comp if projectile doesn't have it
            if (props == null && dinfo.Source.HasComp(typeof(CompAP)))
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
                List<Apparel> wornApparel = new List<Apparel>(pawn.apparel.WornApparel);
                for (int i = wornApparel.Count - 1; i >= 0; i--)
                {
                    if (wornApparel[i].def.apparel.CoversBodyPart(part))
                    {
                        deflected = Utility.ApplyArmor(ref damageAmount, wornApparel[i].GetStatValue(deflectionStat, true), wornApparel[i], damageDef, pierceAmount);
                        if (damageAmount < 0.001)
                        {
                            return 0;
                        }
                        if (deflected)
                        {
                            return Mathf.CeilToInt(damageAmount);
                        }
                    }
                }
            }
            deflected = Utility.ApplyArmor(ref damageAmount, pawn.GetStatValue(deflectionStat, true), null, damageDef, pierceAmount);
            return Mathf.RoundToInt(damageAmount);
        }

        public static int GetAfterArmorDamage(Pawn pawn, int amountInt, BodyPartRecord part, DamageInfo dinfo)
        {
            bool flag = false;
            return Utility.GetAfterArmorDamage(pawn, amountInt, part, dinfo, ref flag);
        }

        private static bool ApplyArmor(ref float damAmount, float armorRating, Thing armorThing, DamageDef damageDef, float pierceAmount)
        {
            float originalDamage = damAmount;
            bool deflected = false;
            float deflectionChance = Mathf.Clamp((armorRating - pierceAmount) * 4, 0, 1);

            //Shot is deflected
            if (deflectionChance > 0 && Rand.Value < deflectionChance)
            {
                deflected = true;
            }
            //Damage calculations
            armorRating = Mathf.Clamp(2 * armorRating - pierceAmount, 0, 1);
            damAmount *= 1 - armorRating;

            //Damage armor
            if (armorThing != null && armorThing as Pawn == null)
            {
                armorThing.TakeDamage(new DamageInfo(damageDef, Mathf.CeilToInt(originalDamage - damAmount), null, null, null));
            }

            return deflected;
        }

    }
}
