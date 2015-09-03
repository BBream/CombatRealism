using RimWorld;
using System;
using UnityEngine;
using Verse;
namespace Combat_Realism
{
	public class Verb_ShootCRShotgun : Verb_ShootCR
	{
        //shotgun shell fires 8 pellets
		protected override bool TryCastShot()
		{
			if (base.TryCastShot())
			{
                int i = 1;
                while (i < 8 && base.TryCastShot())
                {
                    i++;
                }
                return true;
			}
			return false;
		}
	}
}
