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

        public static readonly DamageDef absorbDamageDef = DamageDefOf.Blunt;   //The damage def to convert absorbed shots into

        /// <summary>
        /// Calculates deflection chance and damage through armor
        /// </summary>
        public static int GetAfterArmorDamage(Pawn pawn, int amountInt, BodyPartRecord part, DamageInfo dinfo, bool damageArmor, ref bool deflected)
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

            //Run armor calculations on all apparel
            if (pawn.apparel != null)
            {
                List<Apparel> wornApparel = new List<Apparel>(pawn.apparel.WornApparel);
                for (int i = wornApparel.Count - 1; i >= 0; i--)
                {
                    if (wornApparel[i].def.apparel.CoversBodyPart(part))
                    {
                        Thing armorThing = damageArmor ? wornApparel[i] : null;

                        //Check for deflection
                        if (Utility.ApplyArmor(ref damageAmount, ref pierceAmount, wornApparel[i].GetStatValue(deflectionStat, true), armorThing, damageDef))
                        {
                            deflected = true;
                            if (damageDef != absorbDamageDef)
                            {
                                damageDef = absorbDamageDef;
                                deflectionStat = damageDef.armorCategory.DeflectionStat();
                                i++;
                            }
                        }
                        if (damageAmount < 0.001)
                        {
                            return 0;
                        }
                    }
                }
            }
            //Check for pawn racial armor
            if (Utility.ApplyArmor(ref damageAmount, ref pierceAmount, pawn.GetStatValue(deflectionStat, true), null, damageDef))
            {
                deflected = true;
                if (damageAmount < 0.001)
                {
                    return 0;
                }
                damageDef = absorbDamageDef;
                deflectionStat = damageDef.armorCategory.DeflectionStat();
                Utility.ApplyArmor(ref damageAmount, ref pierceAmount, pawn.GetStatValue(deflectionStat, true), pawn, damageDef);
            }
            return Mathf.RoundToInt(damageAmount);
        }

        /// <summary>
        /// For use with misc DamageWorker functions
        /// </summary>
        public static int GetAfterArmorDamage(Pawn pawn, int amountInt, BodyPartRecord part, DamageInfo dinfo)
        {
            bool flag = false;
            return Utility.GetAfterArmorDamage(pawn, amountInt, part, dinfo, false, ref flag);
        }

        private static bool ApplyArmor(ref float damAmount, ref float pierceAmount, float armorRating, Thing armorThing, DamageDef damageDef)
        {
            float originalDamage = damAmount;
            bool deflected = false;
            float penetrationChance = Mathf.Clamp((pierceAmount - armorRating) * 4, 0, 1);

            //Shot is deflected
            if (penetrationChance == 0 || Rand.Value > penetrationChance)
            {
                deflected = true;
            }
            //Damage calculations
            damAmount *= 1 - Mathf.Clamp(2 * armorRating - pierceAmount, 0, 1);

            //Damage armor
            if (armorThing != null)
            {
                float absorbedDamage = 0f;
                if (damageDef == absorbDamageDef)
                {
                    absorbedDamage = (originalDamage - damAmount) * pierceAmount;
                }
                else
                {
                    absorbedDamage = originalDamage * Mathf.Max(0.3f, (1 - pierceAmount));
                }
                if (armorThing as Pawn == null)
                {
                    armorThing.TakeDamage(new DamageInfo(damageDef, Mathf.CeilToInt(absorbedDamage), null, null, null));
                }
                else
                {
                    damAmount += absorbedDamage;
                }
            }

            pierceAmount *= Mathf.Max(0, 1 - armorRating);
            return deflected;
        }

    }
}
