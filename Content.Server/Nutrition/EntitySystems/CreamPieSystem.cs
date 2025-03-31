using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Decals;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Nutrition.Components;
using Content.Server.Popups;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Explosion.Components;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Rejuvenate;
using Content.Shared.Throwing;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Nutrition.EntitySystems
{
    [UsedImplicitly]
    public sealed class CreamPieSystem : SharedCreamPieSystem
    {
        [Dependency] private readonly SolutionContainerSystem _solutions = default!;
        [Dependency] private readonly PuddleSystem _puddle = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
        [Dependency] private readonly TriggerSystem _trigger = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly DecalSystem _decal = default!;
        [Dependency] private readonly IRobustRandom _rng = default!;

        public override void Initialize()
        {
            base.Initialize();

            // activate BEFORE entity is deleted and trash is spawned
            SubscribeLocalEvent<CreamPieComponent, ConsumeDoAfterEvent>(OnConsume, before: [typeof(FoodSystem)]);
            SubscribeLocalEvent<CreamPieComponent, SliceFoodEvent>(OnSlice);

            SubscribeLocalEvent<CreamPiedComponent, RejuvenateEvent>(OnRejuvenate);
        }

        protected override void SplattedCreamPie(EntityUid uid, CreamPieComponent creamPie)
        {
            _audio.PlayPvs(_audio.GetSound(creamPie.Sound), uid, AudioParams.Default.WithVariation(0.125f));

            if (EntityManager.TryGetComponent(uid, out FoodComponent? foodComp))
            {
                if (_solutions.TryGetSolution(uid, foodComp.Solution, out _, out var solution))
                {
                    _puddle.TrySpillAt(uid, solution, out _, false);
                }
                if (foodComp.Trash.Count == 0)
                {
                    foreach (var trash in foodComp.Trash)
                    {
                        EntityManager.SpawnEntity(trash, Transform(uid).Coordinates);
                    }
                }
            }
            ActivatePayload(uid);

            EntityManager.QueueDeleteEntity(uid);
            if (creamPie.EnhancedComedy)
                _decal.TryAddDecal($"shit{_rng.Next(1,9)}", Transform(uid).Coordinates, out var _, cleanable: true);
        }

        private void OnConsume(Entity<CreamPieComponent> entity, ref ConsumeDoAfterEvent args)
        {
            ActivatePayload(entity);
        }

        private void OnSlice(Entity<CreamPieComponent> entity, ref SliceFoodEvent args)
        {
            ActivatePayload(entity);
        }

        private void ActivatePayload(EntityUid uid)
        {
            if (TryComp<ItemSlotsComponent>(uid, out var slots) && _itemSlots.TryGetSlot(uid, CreamPieComponent.PayloadSlotName, out var itemSlot, slots))
            {
                if (_itemSlots.TryEject(uid, itemSlot, user: null, out var item))
                {
                    if (TryComp<OnUseTimerTriggerComponent>(item.Value, out var timerTrigger))
                    {
                        _trigger.HandleTimerTrigger(
                            item.Value,
                            null,
                            timerTrigger.Delay,
                            timerTrigger.BeepInterval,
                            timerTrigger.InitialBeepDelay,
                            timerTrigger.BeepSound);
                    }
                }
            }
        }

        protected override void CreamedEntity(EntityUid uid, CreamPiedComponent creamPied, ThrowHitByEvent args, bool shit = false)
        {
            _popup.PopupEntity(Loc.GetString($"cream-pied-component-on-hit-by-message{(shit?"-shit":"")}", ("thrower", args.Thrown)), uid, args.Target);
            var otherPlayers = Filter.Empty().AddPlayersByPvs(uid);
            if (TryComp<ActorComponent>(args.Target, out var actor))
            {
                otherPlayers.RemovePlayer(actor.PlayerSession);
            }
            _popup.PopupEntity(Loc.GetString($"cream-pied-component-on-hit-by-message-others{(shit?"-shit":"")}", ("owner", uid), ("thrower", args.Thrown)), uid, otherPlayers, false);
        }

        private void OnRejuvenate(Entity<CreamPiedComponent> entity, ref RejuvenateEvent args)
        {
            SetCreamPied(entity, entity.Comp, false);
            SetShidded(entity, entity.Comp, false);
        }
    }
}
