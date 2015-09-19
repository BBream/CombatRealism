using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using UnityEngine;

namespace Combat_Realism
{
    class CompProperties_Supressable : CompProperties
    {
        public float suppressionThreshold = 0f;

        public CompProperties_Supressable()
        {
            this.compClass = typeof(CompProperties_Supressable);
        }
    }

    class CompSupressable : ThingComp
    {
        private float currentSuppression = 0f;
        private bool isSuppressed = false;

        // --------------- Suppression threshold stuff ---------------
        private float baseSuppressionThreshold = 0f;
        private float suppressionThreshold
        {
            get
            {
                //Get pawn armor value
                float armorValue = 0f;
                Pawn pawn = this.parent as Pawn;
                if (pawn != null)
                {
                    foreach (Apparel apparel in pawn.apparel.WornApparel)
                    {
                        float apparelArmor = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp, true);
                        if (apparelArmor > armorValue)
                        {
                            armorValue = apparelArmor;
                        }
                    }
                }
                else
                {
                    Log.Warning("Tried to get suppression threshold of non-pawn");
                }
                return baseSuppressionThreshold * armorValue;
            }
        }

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            CompProperties_Supressable cprops = props as CompProperties_Supressable;
            if (cprops != null)
            {
                this.baseSuppressionThreshold = cprops.suppressionThreshold;
            }
        }

        public void AddSuppression(float amount)
        {
            this.currentSuppression += amount;
            if (!this.isSuppressed && this.currentSuppression > this.suppressionThreshold)
            {
                MoteThrower.ThrowText(this.parent.Position.ToVector3Shifted(), "Suppressed");
                this.BeSuppressed();
            }
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            this.currentSuppression--;
            if (!this.isSuppressed && this.currentSuppression > this.suppressionThreshold)
            {
                this.BeSuppressed();
            }
        }

        private void BeSuppressed()
        {
            //TODO
        }
    }
}
