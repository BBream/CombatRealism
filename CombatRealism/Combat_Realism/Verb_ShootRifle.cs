using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Combat_Realism
{
    class Verb_ShootRifle : Verb_ShootCR
    {
        protected override bool TryCastShot()
        {
            return base.TryCastShot(this.verbProps.forcedMissRadius, 1/3);
        }
    }
}
