using Content.Shared.Nutrition.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Mood;
using JetBrains.Annotations;
using Content.Shared._White.HochuSrat;
using Content.Shared.Examine;

namespace Content.Shared.Nutrition.EntitySystems
{
    [UsedImplicitly]
    public abstract class SharedCreamPieSystem : EntitySystem
    {
        [Dependency] private SharedStunSystem _stunSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<CreamPieComponent, ThrowDoHitEvent>(OnCreamPieHit);
            SubscribeLocalEvent<CreamPieComponent, LandEvent>(OnCreamPieLand);
            SubscribeLocalEvent<CreamPiedComponent, ThrowHitByEvent>(OnCreamPiedHitBy);
            SubscribeLocalEvent<CreamPiedComponent, ExaminedEvent>(OnCreamPiedExamined);
        }

        public void SplatCreamPie(EntityUid uid, CreamPieComponent creamPie)
        {
            // Already splatted! Do nothing.
            if (creamPie.Splatted)
                return;

            creamPie.Splatted = true;

            SplattedCreamPie(uid, creamPie);
        }

        protected virtual void SplattedCreamPie(EntityUid uid, CreamPieComponent creamPie) {}

        public void SetCreamPied(EntityUid uid, CreamPiedComponent creamPied, bool value)
        {
            if (value == creamPied.CreamPied)
                return;

            creamPied.CreamPied = value;

            if (EntityManager.TryGetComponent(uid, out AppearanceComponent? appearance))
            {
                _appearance.SetData(uid, CreamPiedVisuals.Creamed, value, appearance);
            }

            if (value)
                RaiseLocalEvent(uid, new MoodEffectEvent("Creampied"));
            else
                RaiseLocalEvent(uid, new MoodRemoveEffectEvent("Creampied"));
        }

        public void SetShidded(EntityUid uid, CreamPiedComponent creamPied, bool value)
        {
            if (value == creamPied.Shidded)
                return;

            creamPied.Shidded = value;

            // mood and visual logic is handled in HochuSratSystem
            // i'm just hijacking the creampie system to use pi logic for the shit flinging
            // deadline is 8 hours away
            // i do not look for forgiveness, i ask for understanding
            if (value)
                EnsureComp<ShiddedComponent>(uid);
            else if (TryComp<ShiddedComponent>(uid, out var comp))
                RemComp(uid, comp);
            //if (value)
            //    RaiseLocalEvent(uid, new MoodEffectEvent("Creampied"));
            //else
            //    RaiseLocalEvent(uid, new MoodRemoveEffectEvent("Creampied"));
        }

        private void OnCreamPieLand(EntityUid uid, CreamPieComponent component, ref LandEvent args)
        {
            SplatCreamPie(uid, component);
        }

        private void OnCreamPiedExamined(EntityUid uid, CreamPiedComponent component, ExaminedEvent args)
        {
            if(component.Shidded)
            args.PushMarkup("[color=#BF2A0D]Он весь в говне![/color]",-999);
        }

        private void OnCreamPieHit(EntityUid uid, CreamPieComponent component, ThrowDoHitEvent args)
        {
            SplatCreamPie(uid, component);
        }

        private void OnCreamPiedHitBy(EntityUid uid, CreamPiedComponent creamPied, ThrowHitByEvent args)
        {
            if (!EntityManager.EntityExists(args.Thrown) || !EntityManager.TryGetComponent(args.Thrown, out CreamPieComponent? creamPie)) return;

            if (creamPie.EnhancedComedy)
            {
                SetShidded(uid, creamPied, true);
                CreamedEntity(uid, creamPied, args, true);
            }
            else
            {
                SetCreamPied(uid, creamPied, true);
                CreamedEntity(uid, creamPied, args, false);
            }

            _stunSystem.TryParalyze(uid, TimeSpan.FromSeconds(creamPie.ParalyzeTime), true);
        }

        protected virtual void CreamedEntity(EntityUid uid, CreamPiedComponent creamPied, ThrowHitByEvent args, bool shit = false) {}
    }
}
