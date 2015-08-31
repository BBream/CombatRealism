using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Combat_Realism
{
	/// <summary>
	/// Description of CompPropertiesCustom.
	/// </summary>
	public class CompPropertiesCustom : CompProperties
	{
		public float shotVariation = 0;
        public Vector2 recoilOffsetX = new Vector2(0, 0);
        public Vector2 recoilOffsetY = new Vector2(0, 0);
		
		public CompPropertiesCustom() : base()
		{
		}
		public CompPropertiesCustom(Type compClass) : base(compClass)
		{
		}
	}
}
