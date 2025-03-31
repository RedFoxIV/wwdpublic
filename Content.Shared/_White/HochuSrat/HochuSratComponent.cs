using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.HochuSrat;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HochuSratComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public TimeSpan FirstThrumpet = TimeSpan.FromMinutes(20);

    [DataField]
    [AutoNetworkedField]
    public TimeSpan SecondThrumpet = TimeSpan.FromMinutes(25);

    [DataField]
    [AutoNetworkedField]
    public TimeSpan ThirdThrumpet = TimeSpan.FromMinutes(30);

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan FirstThrumpetTime => LastCommitTime + FirstThrumpet * FastPassMultiplier + FastPassFlat;
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan SecondThrumpetTime => LastCommitTime + SecondThrumpet * FastPassMultiplier + FastPassFlat;
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ThirdThrumpetTime => LastCommitTime + ThirdThrumpet * FastPassMultiplier + FastPassFlat;


    [DataField]
    [AutoNetworkedField]
    public TimeSpan InitialSpread = TimeSpan.FromSeconds(10);

    [AutoNetworkedField]
    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan LastCommitTime = TimeSpan.Zero;

    [DataField]
    [AutoNetworkedField]
    public List<EntProtoId> BazaIDs = new() { "FoodWhiteDream1", "FoodWhiteDream2", "FoodWhiteDream3", "FoodWhiteDream4", "FoodWhiteDream5", "FoodWhiteDream6" };

    [DataField]
    public SoundSpecifier MozartNear = new SoundCollectionSpecifier("shidNear");

    [DataField]
    public SoundSpecifier MozartFar = new SoundCollectionSpecifier("shidFar");

    [AutoNetworkedField]
    public float FastPassMultiplier = 1f;

    [AutoNetworkedField]
    public TimeSpan FastPassFlat = TimeSpan.Zero;

}

[RegisterComponent]
public sealed partial class CanShitInsideComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public DoAfterId? CurrentDoAfter; // tracked on the target to prevent toilet ganking by 5 assistants all at the same time

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float ShitTime = 60f;
}

// it's 1AM cut me some slack
public abstract class SharedCanShitInsideEntitySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doafter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CanShitInsideComponent, GetVerbsEvent<AlternativeVerb>>(GetAltVerb);
        SubscribeLocalEvent<CanShitInsideComponent, ShitDoAfterEvent>(Shieet);
    }

    protected virtual void Shieet(EntityUid uid, CanShitInsideComponent comp, ShitDoAfterEvent args) { }

    private void GetAltVerb(EntityUid uid, CanShitInsideComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract ||
            !TryComp<HochuSratComponent>(uid, out var srat) ||
            _timing.CurTime < srat.FirstThrumpetTime)
            return;

        var verb = new AlternativeVerb()
        {
            Act = () => TryShit(uid, comp, args.User),
            Text = Loc.GetString("take-a-dump-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/shit.shit.192dpi.png")),
            Priority = 1337
        };
        args.Verbs.Add(verb);
    }

    private void TryShit(EntityUid uid, CanShitInsideComponent target, EntityUid user)
    {
        if(!TryComp<HochuSratComponent>(uid, out var srat) ||
            _timing.CurTime < srat.FirstThrumpetTime)
            return;

        if (target.CurrentDoAfter.HasValue)
        {
            _popup.PopupEntity("toilet-occupupied", uid, user);
            return;
        }

        var args = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(target.ShitTime), new ShitDoAfterEvent(), uid, uid, null, user)
        {
            BlockDuplicate = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            NeedHand = false,
            BreakOnDropItem = false,
            BreakOnHandChange = false,
            Hidden = true,
            RequireCanInteract = true,
        };

        _doafter.TryStartDoAfter(args);
    }
}

public sealed partial class ShitDoAfterEvent : SimpleDoAfterEvent { }
