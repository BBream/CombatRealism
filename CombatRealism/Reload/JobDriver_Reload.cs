using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Combat_Realism
{
    public class JobDriver_Reload : JobDriver
    {
        private CompReloader CompReloader
        {
            get
            {
                return TargetThingB.TryGetComp<CompReloader>();
            }
        }
        protected override IEnumerable< Toil > MakeNewToils()
        {
            this.FailOnBroken( TargetIndex.A );

            //Toil of do-nothing
            pawn.stances.SetStance(new Stance_Cooldown(CompReloader.reloaderProp.reloadTick, TargetInfo.Invalid));

            //Actual reloader
            var reloadToil = new Toil();
            reloadToil.AddFinishAction( CompReloader.FinishReload );

            yield return reloadToil;
        }
    }
}
