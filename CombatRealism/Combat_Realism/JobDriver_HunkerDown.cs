﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Combat_Realism
{
    class JobDriver_HunkerDown : JobDriver
    {
        private const int getUpCheckInterval = 60;

        public override PawnPosture Posture
        {
            get
            {
                return PawnPosture.LayingAny;
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
           this.FailOnBroken(TargetIndex.A);

		   //Define Toil
		   Toil toilWait = new Toil();
		   toilWait.initAction = () =>
		   {
			   toilWait.actor.pather.StopDead();
		   };

		   Toil toilNothing = new Toil();
		   //toilNothing.initAction = () => {};
		   toilNothing.defaultCompleteMode = ToilCompleteMode.Delay;
		   toilNothing.defaultDuration = getUpCheckInterval;		   
		   
		   // Start Toil
           yield return toilWait;
		   yield return toilNothing;
		   yield return Toils_Jump.JumpIf(toilNothing, () => 
		   {
			   CompSuppressable comp = pawn.TryGetComp<CompSuppressable>();
			   if (comp == null)
			   {
			    	return false;
			   }
			   float distToSuppressor = (pawn.Position - comp.suppressorLoc).LengthHorizontal;
			   if (distToSuppressor < CompSuppressable.minSuppressionDist)
			   {
			    	return false;
			   }
			   return comp.isHunkering;
		   });
        }
    }
}
