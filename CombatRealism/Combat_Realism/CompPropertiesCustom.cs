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
		public float moaValue = 0.1f;
		public float recoil = 0.1f;
		public bool scope = false;
		
		public CompPropertiesCustom() : base()
		{
		}
		public CompPropertiesCustom(Type compClass) : base(compClass)
		{
		}
	}
}
