using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Combat_Realism
{
	public class DamageWorker_AddInjuryCR : DamageWorker
	{
		private struct LocalInjuryResult
		{
			public bool wounded;
			public bool headshot;
			public bool deflected;
			public BodyPartRecord lastHitPart;
			public float totalDamageDealt;
			public static DamageWorker_AddInjuryCR.LocalInjuryResult MakeNew()
			{
				return new DamageWorker_AddInjuryCR.LocalInjuryResult
				{
					wounded = false,
					headshot = false,
					deflected = false,
					lastHitPart = null,
					totalDamageDealt = 0f
				};
			}
		}
		private const float SpreadDamageChance = 0.5f;

		public override float Apply(DamageInfo dinfo, Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null)
			{
				return base.Apply(dinfo, thing);
			}
			return this.ApplyToPawn(dinfo, pawn);
		}

		private float ApplyToPawn(DamageInfo dinfo, Pawn pawn)
		{
            //Sanity checks
			if (dinfo.Amount <= 0)
			{
				return 0f;
			}
			if (!DebugSettings.enablePlayerDamage && pawn.Faction == Faction.OfColony)
			{
				return 0f;
			}
			if (!dinfo.Part.HasValue)
			{
				dinfo.SetPart(new BodyPartDamageInfo(null, null));
			}

			DamageWorker_AddInjuryCR.LocalInjuryResult localInjuryResult = DamageWorker_AddInjuryCR.LocalInjuryResult.MakeNew();

            //Code for damage sources which spread out
			if (dinfo.Def.spreadOut)
			{
				if (pawn.apparel != null)
				{
					List<Apparel> wornApparel = pawn.apparel.WornApparel;
					for (int i = wornApparel.Count - 1; i >= 0; i--)
					{
						this.CheckApplySpreadDamage(dinfo, wornApparel[i]);
					}
				}
				if (pawn.equipment != null && pawn.equipment.Primary != null)
				{
					this.CheckApplySpreadDamage(dinfo, pawn.equipment.Primary);
				}
				if (pawn.inventory != null)
				{
					ThingContainer container = pawn.inventory.container;
					for (int j = container.Count - 1; j >= 0; j--)
					{
						this.CheckApplySpreadDamage(dinfo, container[j]);
					}
				}
			}
			if (!this.FragmentDamageForDamageType(dinfo, pawn, ref localInjuryResult))
			{
				this.ApplyDamagePartial(dinfo, pawn, ref localInjuryResult);
				this.CheckDuplicateSmallPawnDamageToPartParent(dinfo, pawn, ref localInjuryResult);
			}
			if (localInjuryResult.wounded)
			{
				DamageWorker_AddInjuryCR.PlayWoundedVoiceSound(dinfo, pawn);
				pawn.drawer.Notify_DamageApplied(dinfo);
				DamageWorker_AddInjuryCR.InformPsychology(dinfo, pawn);
			}
			if (localInjuryResult.headshot && pawn.SpawnedInWorld)
			{
				MoteThrower.ThrowText(new Vector3((float)pawn.Position.x + 1f, (float)pawn.Position.y, (float)pawn.Position.z + 1f), "Headshot".Translate(), Color.white, -1);
			}
			if (localInjuryResult.deflected)
			{
				if (pawn.health.deflectionEffecter == null)
				{
					pawn.health.deflectionEffecter = EffecterDef.Named("ArmorRating").Spawn();
				}
				pawn.health.deflectionEffecter.Trigger(pawn, pawn);
			}
			else
			{
				ImpactSoundUtility.PlayImpactSound(pawn, dinfo.Def.impactSoundType);
			}
			return localInjuryResult.totalDamageDealt;
		}

        /// <summary>
        /// Reduces damage applied on spread out damage by random amount
        /// </summary>
        /// <param name="dinfo"></param>
        /// <param name="t"></param>
		private void CheckApplySpreadDamage(DamageInfo dinfo, Thing t)
		{
			if (UnityEngine.Random.value < 0.5f)
			{
				dinfo.SetAmount(Mathf.CeilToInt((float)dinfo.Amount * Rand.Range(0.35f, 0.7f)));
				t.TakeDamage(dinfo);
			}
		}

        /// <summary>
        /// Distributes damage over multiple body parts on damage types which spread out
        /// </summary>
        /// <param name="dinfo"></param>
        /// <param name="pawn"></param>
        /// <param name="result"></param>
        /// <returns></returns>
		private bool FragmentDamageForDamageType(DamageInfo dinfo, Pawn pawn, ref DamageWorker_AddInjuryCR.LocalInjuryResult result)
		{
			if (!dinfo.AllowDamagePropagation)
			{
				return false;
			}
			if (dinfo.Amount < 9)
			{
				return false;
			}
			if (!dinfo.Def.spreadOut)
			{
				return false;
			}
			int num = Rand.RangeInclusive(3, 4);
			for (int i = 0; i < num; i++)
			{
				DamageInfo dinfo2 = dinfo;
				dinfo2.SetAmount(dinfo.Amount / num);
				this.ApplyDamagePartial(dinfo2, pawn, ref result);
			}
			return true;
		}
		private void CheckDuplicateSmallPawnDamageToPartParent(DamageInfo dinfo, Pawn pawn, ref DamageWorker_AddInjuryCR.LocalInjuryResult result)
		{
			if (!dinfo.AllowDamagePropagation)
			{
				return;
			}
			if (result.lastHitPart != null && dinfo.Def.harmsHealth && result.lastHitPart != pawn.RaceProps.body.corePart && result.lastHitPart.parent != null && pawn.health.hediffSet.GetPartHealth(result.lastHitPart.parent) > 0f && dinfo.Amount >= 10 && pawn.HealthScale <= 0.5001f)
			{
				DamageInfo dinfo2 = dinfo;
				BodyPartDamageInfo part = new BodyPartDamageInfo(result.lastHitPart.parent, false);
				dinfo2.SetPart(part);
				this.ApplyDamagePartial(dinfo2, pawn, ref result);
			}
		}

        /// <summary>
        /// Handles damage and armor calculations
        /// </summary>
        /// <param name="dinfo"></param>
        /// <param name="pawn"></param>
        /// <param name="result"></param>
		private void ApplyDamagePartial(DamageInfo dinfo, Pawn pawn, ref DamageWorker_AddInjuryCR.LocalInjuryResult result)
		{
			BodyPartRecord exactPartFromDamageInfo = DamageWorker_AddInjuryCR.GetExactPartFromDamageInfo(dinfo, pawn);
			if (exactPartFromDamageInfo == null)
			{
				return;
			}
			bool flag = true;
			if (dinfo.InstantOldInjury)
			{
				flag = false;
			}
			int damageAmount = dinfo.Amount;
            bool shotAbsorbed = false;
			if (!result.deflected && flag)
			{
				//damageAmount = ArmorUtility.GetAfterArmorDamage(pawn, dinfo.Amount, exactPartFromDamageInfo, dinfo.Def);
                damageAmount = Utility.GetAfterArmorDamage(pawn, dinfo.Amount, exactPartFromDamageInfo, dinfo, true, ref shotAbsorbed);
			}
			if ((double)damageAmount < 0.001)
			{
				result.deflected = true;
				return;
			}

            //Shot absorbed and converted into blunt
            if (shotAbsorbed)
            {
                result.deflected = true;
                if (dinfo.Def != Utility.absorbDamageDef)
                {
                    //Get outer parent of struck part
                    BodyPartRecord parentPart = exactPartFromDamageInfo;
                    while (parentPart != null && parentPart.parent != null && parentPart.depth != BodyPartDepth.Outside)
                    {
                        parentPart = parentPart.parent;
                    }
                    DamageInfo dinfo2 = new DamageInfo(Utility.absorbDamageDef, damageAmount, dinfo.Instigator, new BodyPartDamageInfo(parentPart, false), dinfo.Source);
                    this.ApplyDamagePartial(dinfo2, pawn, ref result);
                    return;
                }
            }

            //Creating the Hediff
			HediffDef hediffDefFromDamage = HealthUtility.GetHediffDefFromDamage(dinfo.Def, pawn, exactPartFromDamageInfo);
			Hediff_Injury hediff_Injury = (Hediff_Injury)HediffMaker.MakeHediff(hediffDefFromDamage, pawn, null);
			hediff_Injury.Part = exactPartFromDamageInfo;
			hediff_Injury.source = dinfo.Source;
			hediff_Injury.sourceBodyPartGroup = dinfo.LinkedBodyPartGroup;
			hediff_Injury.sourceHediffDef = dinfo.LinkedHediffDef;
			hediff_Injury.Severity = (float)damageAmount;
			if (dinfo.InstantOldInjury)
			{
				HediffComp_GetsOld hediffComp_GetsOld = hediff_Injury.TryGetComp<HediffComp_GetsOld>();
				if (hediffComp_GetsOld != null)
				{
					hediffComp_GetsOld.isOld = dinfo.InstantOldInjury;
				}
				else
				{
					Log.Error(string.Concat(new object[]
					{
						"Tried to create instant old injury on Hediff without a GetsOld comp: ",
						hediffDefFromDamage,
						" on ",
						pawn
					}));
				}
			}
			result.wounded = true;
			result.lastHitPart = hediff_Injury.Part;
			if (DamageWorker_AddInjuryCR.IsHeadshot(dinfo, hediff_Injury, pawn))
			{
				result.headshot = true;
			}
			if (dinfo.InstantOldInjury && (hediff_Injury.def.CompPropsFor(typeof(HediffComp_GetsOld)) == null || hediff_Injury.Part.def.oldInjuryBaseChance == 0f || hediff_Injury.Part.def.IsSolid(hediff_Injury.Part, pawn.health.hediffSet.hediffs) || pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(hediff_Injury.Part)))
			{
				return;
			}
			this.FinalizeAndAddInjury(pawn, hediff_Injury, dinfo, ref result);
			this.CheckPropagateDamageToInnerSolidParts(dinfo, pawn, hediff_Injury, flag, ref result);
			this.CheckDuplicateDamageToOuterParts(dinfo, pawn, hediff_Injury, flag, ref result);
		}

		private void FinalizeAndAddInjury(Pawn pawn, Hediff_Injury injury, DamageInfo dinfo, ref DamageWorker_AddInjuryCR.LocalInjuryResult result)
		{
			this.CalculateOldInjuryDamageThreshold(pawn, injury);
			result.totalDamageDealt += injury.Severity;
			pawn.health.AddHediff(injury, null, new DamageInfo?(dinfo));
		}

		private void CalculateOldInjuryDamageThreshold(Pawn pawn, Hediff_Injury injury)
		{
			HediffCompProperties hediffCompProperties = injury.def.CompPropsFor(typeof(HediffComp_GetsOld));
			if (hediffCompProperties == null)
			{
				return;
			}
			if (injury.Part.def.IsSolid(injury.Part, pawn.health.hediffSet.hediffs) || pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(injury.Part) || injury.IsOld() || injury.Part.def.oldInjuryBaseChance < 1E-05f)
			{
				return;
			}
			bool flag = injury.Part.def.oldInjuryBaseChance >= 0.8f;
			if ((Rand.Value <= injury.Part.def.oldInjuryBaseChance * hediffCompProperties.becomeOldChance && injury.Severity >= injury.Part.def.GetMaxHealth(pawn) * 0.25f && injury.Severity >= 7f) || flag)
			{
				HediffComp_GetsOld hediffComp_GetsOld = injury.TryGetComp<HediffComp_GetsOld>();
				float num = 1f;
				float num2 = injury.Severity / 2f;
				if (num <= num2)
				{
					hediffComp_GetsOld.oldDamageThreshold = Rand.Range(num, num2);
				}
				if (flag)
				{
					hediffComp_GetsOld.oldDamageThreshold = injury.Severity;
					hediffComp_GetsOld.isOld = true;
				}
			}
		}
		private void CheckPropagateDamageToInnerSolidParts(DamageInfo dinfo, Pawn pawn, Hediff_Injury injury, bool involveArmor, ref DamageWorker_AddInjuryCR.LocalInjuryResult result)
		{
			if (!dinfo.AllowDamagePropagation)
			{
				return;
			}
			if (Rand.Value >= HealthTunings.ChanceToAdditionallyDamageInnerSolidPart)
			{
				return;
			}
			if (dinfo.Def.hasChanceToAdditionallyDamageInnerSolidParts && !injury.Part.def.IsSolid(injury.Part, pawn.health.hediffSet.hediffs) && injury.Part.depth == BodyPartDepth.Outside)
			{
				IEnumerable<BodyPartRecord> source = 
					from x in pawn.health.hediffSet.GetNotMissingParts(null, null)
					where x.parent == injury.Part && x.def.IsSolid(x, pawn.health.hediffSet.hediffs) && x.depth == BodyPartDepth.Inside
					select x;
				BodyPartRecord part;
				if (source.TryRandomElementByWeight((BodyPartRecord x) => x.absoluteFleshCoverage, out part))
				{
					HediffDef hediffDefFromDamage = HealthUtility.GetHediffDefFromDamage(dinfo.Def, pawn, part);
					Hediff_Injury hediff_Injury = (Hediff_Injury)HediffMaker.MakeHediff(hediffDefFromDamage, pawn, null);
					hediff_Injury.Part = part;
					hediff_Injury.source = injury.source;
					hediff_Injury.sourceBodyPartGroup = injury.sourceBodyPartGroup;
					hediff_Injury.Severity = (float)(dinfo.Amount / 2);
					if (involveArmor)
                    {
						hediff_Injury.Severity = (float)Utility.GetAfterArmorDamage(pawn, dinfo.Amount / 2, part, dinfo);
					}
					if (hediff_Injury.Severity <= 0f)
					{
						return;
					}
					result.lastHitPart = hediff_Injury.Part;
					this.FinalizeAndAddInjury(pawn, hediff_Injury, dinfo, ref result);
				}
			}
		}
		private void CheckDuplicateDamageToOuterParts(DamageInfo dinfo, Pawn pawn, Hediff_Injury injury, bool involveArmor, ref DamageWorker_AddInjuryCR.LocalInjuryResult result)
		{
			if (!dinfo.AllowDamagePropagation)
			{
				return;
			}
			if (dinfo.Def.harmAllLayersUntilOutside && injury.Part.depth == BodyPartDepth.Inside)
			{
				BodyPartRecord parent = injury.Part.parent;
				do
				{
					if (pawn.health.hediffSet.GetPartHealth(parent) != 0f)
					{
						HediffDef hediffDefFromDamage = HealthUtility.GetHediffDefFromDamage(dinfo.Def, pawn, parent);
						Hediff_Injury hediff_Injury = (Hediff_Injury)HediffMaker.MakeHediff(hediffDefFromDamage, pawn, null);
						hediff_Injury.Part = parent;
						hediff_Injury.source = injury.source;
						hediff_Injury.sourceBodyPartGroup = injury.sourceBodyPartGroup;
						hediff_Injury.Severity = (float)dinfo.Amount;
						if (involveArmor)
						{
							hediff_Injury.Severity = (float)Utility.GetAfterArmorDamage(pawn, dinfo.Amount, parent, dinfo);
						}
						if (hediff_Injury.Severity <= 0f)
						{
							hediff_Injury.Severity = 1f;
						}
						result.lastHitPart = hediff_Injury.Part;
						this.FinalizeAndAddInjury(pawn, hediff_Injury, dinfo, ref result);
					}
					if (parent.depth == BodyPartDepth.Outside)
					{
						break;
					}
					parent = parent.parent;
				}
				while (parent != null);
			}
		}
		private static void InformPsychology(DamageInfo dinfo, Pawn pawn)
		{
			if (!pawn.Dead && pawn.needs.mood != null && pawn.thinker != null && dinfo.Def.battleWound)
			{
				pawn.needs.mood.thoughts.TryGainThought(ThoughtDefOf.BattleWounded);
			}
		}
		private static bool IsHeadshot(DamageInfo dinfo, Hediff_Injury injury, Pawn pawn)
		{
			return !dinfo.InstantOldInjury && injury.Part.groups.Contains(BodyPartGroupDefOf.FullHead) && dinfo.Def == DamageDefOf.Bullet;
		}

		private static BodyPartRecord GetExactPartFromDamageInfo(DamageInfo dinfo, Pawn pawn)
		{
			if (dinfo.Part.Value.Part == null)
			{
				BodyPartRecord randomNotMissingPart = pawn.health.hediffSet.GetRandomNotMissingPart(dinfo.Part.Value.Height, dinfo.Part.Value.Depth);
				if (randomNotMissingPart == null)
				{
					Log.Warning("GetRandomNotMissingPart returned null (any part).");
				}
				return randomNotMissingPart;
			}
			if (!dinfo.Part.Value.CanMissBodyPart)
			{
				return (
					from x in pawn.health.hediffSet.GetNotMissingParts(null, null)
					where x == dinfo.Part.Value.Part
					select x).FirstOrDefault<BodyPartRecord>();
			}
			BodyPartRecord randomNotMissingPart2 = pawn.health.hediffSet.GetRandomNotMissingPart(null, null);
			if (randomNotMissingPart2 == null)
			{
				Log.Warning("GetRandomNotMissingPart returned null (specified part).");
			}
			return randomNotMissingPart2;
		}

		private static void PlayWoundedVoiceSound(DamageInfo dinfo, Pawn pawn)
		{
			if (pawn.Dead)
			{
				return;
			}
			if (dinfo.InstantOldInjury)
			{
				return;
			}
			if (dinfo.Def.externalViolence)
			{
				LifeStageUtility.PlayNearestLifestageSound(pawn, (LifeStageAge ls) => ls.soundWounded);
			}
		}
	}
}
