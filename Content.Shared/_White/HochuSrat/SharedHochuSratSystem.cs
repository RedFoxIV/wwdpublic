using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.HochuSrat;

public abstract class SharedHochuSratSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming _timing = default!;

    public override void Initialize()
    {

    }
}
