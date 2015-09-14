using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Combat_Realism
{
    class CompProperties_Charges : CompProperties
    {
        public List<Vector2> charges = new List<Vector2>();

        public CompProperties_Charges()
        {
            this.compClass = typeof(CompProperties_Charges);
        }
    }

    class CompCharges : ThingComp
    {
        private CompProperties_Charges cprops;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            CompProperties_Charges cprops = props as CompProperties_Charges;
            if (cprops != null)
            {
                this.cprops = cprops;
            }
        }

        public bool GetChargeSpeed(float range, out float speed)
        {
            if (this.cprops != null && cprops.charges.Count > 0)
            {
                foreach (Vector2 vec in cprops.charges)
                {
                    if (range <= vec.y)
                    {
                        speed = vec.x;
                        return true;
                    }
                }
            }
            speed = 0;
            return false;
        }
    }
}
