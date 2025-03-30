using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public TimeSpan ActualFirstThrumpet => FirstThrumpet * FastPassMultiplier + FastPassFlat;
    public TimeSpan ActualSecondThrumpet => SecondThrumpet * FastPassMultiplier + FastPassFlat;
    public TimeSpan ActualThirdThrumpet => ThirdThrumpet * FastPassMultiplier + FastPassFlat;


    [DataField]
    [AutoNetworkedField]
    public TimeSpan InitialSpread = TimeSpan.FromSeconds(10);

    [AutoNetworkedField]
    public TimeSpan LastCommitTime = TimeSpan.Zero;

    [DataField]
    [AutoNetworkedField]
    public EntProtoId BazaID = "FoodWhiteDream";

    [DataField]
    public SoundSpecifier MozartNear = new SoundCollectionSpecifier("shidNear");

    [DataField]
    public SoundSpecifier MozartFar = new SoundCollectionSpecifier("shidFar");

    [AutoNetworkedField]
    public float FastPassMultiplier = 1f;

    [AutoNetworkedField]
    public TimeSpan FastPassFlat = TimeSpan.Zero;

}
