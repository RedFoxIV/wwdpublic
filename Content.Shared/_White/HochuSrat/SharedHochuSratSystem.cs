using Content.Shared.Mood;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace Content.Shared._White.HochuSrat;

public abstract class SharedHochuSratSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming _timing = default!;
    [Dependency] protected readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ShiddedComponent, ComponentInit>(Shidded);
        SubscribeLocalEvent<ShiddedComponent, ComponentRemove>(Unshidded);
    }

    public virtual void Shidded(EntityUid uid, ShiddedComponent comp, ComponentInit args)
    {
        RaiseLocalEvent(uid, new MoodEffectEvent("shidded"));
        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, CreamPiedVisuals.Shidded, true, appearance);
        }
    }

    public virtual void Unshidded(EntityUid uid, ShiddedComponent comp, ComponentRemove args)
    {
        RaiseLocalEvent(uid, new MoodRemoveEffectEvent("shidded"));
        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, CreamPiedVisuals.Shidded, false, appearance);
        }
    }
}
