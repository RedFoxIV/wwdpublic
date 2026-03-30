
using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._White.Pods;

/// <summary>
///     Handles player and NPC mob movement.
///     NPCs are handled server-side only.
/// </summary>
public sealed class PodMovementController : VirtualController
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;

    public override void Initialize()
    {
        base.Initialize();


    }
    
    /// <summary>
    ///     Run before any map processing starts.
    /// </summary>
    /// <param name="prediction"></param>
    /// <param name="frameTime"></param>
    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        var query = AllEntityQuery<PodComponent, PhysicsComponent, InputMoverComponent>();
        while (query.MoveNext(out EntityUid uid, out var pod, out var physics, out var input))
        {
            if (prediction && !pod.RunInPrediction)
                continue;
            (var walk, var sprint) = _mover.GetVelocityInput(input);
            bool walking = !input.HeldMoveButtons.HasFlag(MoveButtons.Walk); // interesting
            var moveVec = sprint + walk;



            //if(moveVec.LengthSquared() < 0.001f)
            //    continue;

            moveVec *= frameTime;
            moveVec.X *= pod.SideAcceleration;
            moveVec.Y *= moveVec.Y > 0 ? pod.ForwardAcceleration : pod.BackwardAcceleration;
            moveVec *= pod.AccelerationMultiplier;

            moveVec = (Comp<TransformComponent>(uid).WorldRotation+Math.PI).RotateVec(moveVec);
            var velocity = physics.LinearVelocity;


            var resultVel = velocity + moveVec;
            var resultSpeed = resultVel.Length();

            var maxSpeed = MathF.Max(pod.MaxSpeed, velocity.Length());
            resultVel = resultSpeed > maxSpeed ? resultVel / resultSpeed * maxSpeed : resultVel;

            _physics.SetLinearVelocity(uid, walking ? resultVel * 0.95f : resultVel, body: physics);
        }
    }

    //private Vector2 AddLimited(Vector2 a, Vector2 b, float xmax, float ymax, float xmin, float ymin) 
    //=> new Vector2(
    //        a.X + b.X 
    //    ); 

    ///// <summary>
    /////     Run after all map processing has finished.
    ///// </summary>
    ///// <param name="prediction"></param>
    ///// <param name="frameTime"></param>
    //public override void UpdateAfterSolve(bool prediction, float frameTime)
    //{
//
    //}
//
    ///// <summary>
    /////     Run before a particular map starts.
    ///// </summary>
    ///// <param name="prediction"></param>
    ///// <param name="mapComponent"></param>
    ///// <param name="frameTime"></param>
    //public override void UpdateBeforeMapSolve(bool prediction, PhysicsMapComponent mapComponent, float frameTime)
    //{
//
    //}
//
    ///// <summary>
    /////     Run after a particular map finishes.
    ///// </summary>
    ///// <param name="prediction"></param>
    ///// <param name="mapComponent"></param>
    ///// <param name="frameTime"></param>
    //public override void UpdateAfterMapSolve(bool prediction, PhysicsMapComponent mapComponent, float frameTime)
    //{
    //    
    //}
}



[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PodComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxSpeed = 60f;

    [DataField, AutoNetworkedField]
    public float AccelerationMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float ForwardAcceleration = 5f;

    [DataField, AutoNetworkedField]
    public float SideAcceleration = 3f;

    [DataField, AutoNetworkedField]
    public float BackwardAcceleration = 1f;

    [DataField, AutoNetworkedField]
    public bool RunInPrediction = true;


}