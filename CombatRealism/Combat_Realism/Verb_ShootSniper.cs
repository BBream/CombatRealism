using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace Combat_Realism
{
    class Verb_ShootSniper : Verb_ShootCR
    {
        protected override bool TryCastShot()
        {
            return base.TryCastShot(this.verbProps.forcedMissRadius, 0.1f);
        }
    }
}
