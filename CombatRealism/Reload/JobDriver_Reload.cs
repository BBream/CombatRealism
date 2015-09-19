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
            var waitToil = new Toil();
            waitToil.initAction = () => waitToil.actor.pather.StopDead();
            waitToil.defaultCompleteMode = ToilCompleteMode.Delay;
            waitToil.defaultDuration = CompReloader.reloaderProp.reloadTick;
            yield return waitToil;

            //Actual reloader
            var reloadToil = new Toil();
            reloadToil.AddFinishAction( CompReloader.FinishReload );

            yield return reloadToil;
        }
    }
}
