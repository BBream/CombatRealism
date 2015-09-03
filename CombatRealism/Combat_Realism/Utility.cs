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
        /// Determines the impact location accounting for cover obstruction not accounted for by vanilla collision, with chance of interception proportional to distance travelled.
        /// Collision occurs only after specified range, use 1 for collision detection at all ranges, 0 to disable distance effect on intercept chance.
        /// </summary>
        public static TargetInfo determineImpactPosition(IntVec3 originalPosition, TargetInfo originalTarget, int minCollisionRange)
        {
            //Flight path is divided into 1 cell segments
            Vector3 trajectory = originalTarget.Cell.ToVector3() - originalPosition.ToVector3();
            int numSegments = (int)trajectory.magnitude;
            Vector3 trajectorySegment = trajectory / trajectory.magnitude;

            Vector3 exactTestedPosition = originalPosition.ToVector3();
            IntVec3 testedPosition = originalPosition;

            //Go through flight path one segment at a time
            for (int segmentIndex = 1; segmentIndex < numSegments; segmentIndex++)
            {
                exactTestedPosition += trajectorySegment;
                testedPosition = exactTestedPosition.ToIntVec3();

                if (!exactTestedPosition.InBounds())
                {
                    break;
                }

                //Check for collision starting at minimum collision range
                if (segmentIndex >= minCollisionRange)
                {
                    List<Thing> list = Find.ThingGrid.ThingsListAt(testedPosition);
                    for (int i = 0; i < list.Count; i++)
                    {
                        Thing currentThing = list[i];

                        float collateralChance = 0f;

                        if (currentThing.def.Fillage == FillCategory.Partial && currentThing.def.category != ThingCategory.Pawn)
                        {
                            collateralChance = currentThing.def.fillPercent;
                        }
                        if (minCollisionRange != 0)
                        {
                            collateralChance *= segmentIndex / trajectory.magnitude;
                        }

                        if (Rand.Value < collateralChance)
                        {
                            return new TargetInfo(currentThing);
                        }
                    }
                }
            }
            return originalTarget;
        }

        public static TargetInfo determineImpactPosition(IntVec3 originalPosition, TargetInfo originalTarget)
        {
            return determineImpactPosition(originalPosition, originalTarget, 0);
        }

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
                if (pawn.Downed)
                {
                    collisionHeight = pawn.BodySize > 1 ? pawn.BodySize - 0.8f : 0.8f * pawn.BodySize;
                }
                return collisionHeight;
            }
            return thing.def.fillPercent;
        }
    }
}
