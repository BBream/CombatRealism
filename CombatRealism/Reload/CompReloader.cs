using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Combat_Realism
{
    public class CompProperties_Reloader : CompProperties
    {
        public int roundPerMag = 1;
        public int reloadTick = 300;
        public bool throwMote = true;
    }

    public class CompReloader : CommunityCoreLibrary.CompRangedGizmoGiver
    {
        public int count;
        public bool needReload;
        public CompProperties_Reloader reloaderProp;

        public CompEquippable CompEquippable
        {
            get { return parent.GetComp< CompEquippable >(); }
        }

        public Pawn Wielder
        {
            get { return CompEquippable.PrimaryVerb.CasterPawn; }
        }

        public override void Initialize( CompProperties vprops )
        {
            base.Initialize( vprops );

            reloaderProp = vprops as CompProperties_Reloader;
            if ( reloaderProp != null )
            {
                count = reloaderProp.roundPerMag;
            }
            else
            {
                Log.Warning( "Could not find a CompProperties_Reloader for CompReloader." );
                count = 9876;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.LookValue( ref count, "count", 1 );
        }

        public void StartReload()
        {
            count = 0;
            needReload = true;
#if DEBUG
            if ( CompEquippable == null )
            {
                Log.ErrorOnce( "CompEquippable of " + parent + " is null!", 7381888 );
                FinishReload();
                return;
            }
            if ( Wielder == null )
            {
                Log.ErrorOnce( "Wielder of " + parent + " is null!", 7381889 );
                FinishReload();
                return;
            }
#endif
            if ( reloaderProp.throwMote )
            {
                MoteThrower.ThrowText( Wielder.Position.ToVector3Shifted(), "CR_ReloadingMote".Translate() );
            }
            var job = new Job( DefDatabase< JobDef >.GetNamed( "ReloadWeapon" ), Wielder, parent )
            {
                playerForced = true
                
            };

            if ( Wielder.drafter != null )
            {
                Wielder.drafter.TakeOrderedJob( job );
            }
            else
            {
                ExternalPawnDrafter.TakeOrderedJob( Wielder, job );
            }
        }
    
        public void FinishReload()
        {
            parent.def.soundInteract.PlayOneShot(SoundInfo.InWorld(Wielder.Position));
            if ( reloaderProp.throwMote )
            {
                MoteThrower.ThrowText(Wielder.Position.ToVector3Shifted(), "CR_ReloadedMote".Translate());
            }
            count = reloaderProp.roundPerMag;
            needReload = false;
        }

        public override void PostDraw()
        {
            // TODO: Reload indicator?
        }

        private class GizmoAmmoStatus : Command
        {
            //Link
            public CompReloader compAmmo;
            
            private static readonly Texture2D FullTex =
                SolidColorMaterials.NewSolidColorTexture( new Color( 0.2f, 0.2f, 0.24f ) );
            private static readonly Texture2D EmptyTex = SolidColorMaterials.NewSolidColorTexture( Color.clear );

            public override float Width
            {
                get
                {
                    return 120;
                }
            }

            public override GizmoResult GizmoOnGUI( Vector2 topLeft )
            {
                var overRect = new Rect( topLeft.x, topLeft.y, Width, Height );
                Widgets.DrawBox( overRect );
                GUI.DrawTexture( overRect, BGTex );

                var inRect = overRect.ContractedBy( 6 );

                //Item label
                var textRect = inRect;
                textRect.height = overRect.height/2;
                Text.Font = GameFont.Tiny;
                Widgets.Label( textRect, compAmmo.parent.def.LabelCap );

                //Bar
                var barRect = inRect;
                barRect.yMin = overRect.y + overRect.height/2f;
                var ePct = (float) compAmmo.count/compAmmo.reloaderProp.roundPerMag;
                Widgets.FillableBar( barRect, ePct, FullTex, EmptyTex, false );
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label( barRect, compAmmo.count + " / " + compAmmo.reloaderProp.roundPerMag );
                Text.Anchor = TextAnchor.UpperLeft;

                return new GizmoResult( GizmoState.Clear );
            }
        }

        public override IEnumerable< Command > CompGetGizmosExtra()
        {
            var ammoStat = new GizmoAmmoStatus
            {
                compAmmo = this
            };

            yield return ammoStat;

            if (this.Wielder != null)
            {
                var com = new Command_Action
                {
                    action = StartReload,
                    defaultLabel = "CR_ReloadLabel".Translate(),
                    defaultDesc = "CR_ReloadDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/Reload", true)
                };

                yield return com;
            }
        }
    }
}
