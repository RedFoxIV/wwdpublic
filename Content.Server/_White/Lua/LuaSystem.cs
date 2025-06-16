using Content.Server.Speech;
using Content.Shared.Doors;
using JetBrains.Annotations;
using JetBrains.FormatRipper.Pe;
using MoonSharp.Interpreter;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Reflection;
using System.Collections.Frozen;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Content.Server._White.Lua;

public sealed class LuaSystem : EntitySystem
{
    [Dependency] private readonly IEntitySystemManager _entSysMan = default!;
    [Dependency] private readonly IDependencyCollection _dependency = default!;
    [Dependency] private readonly IReflectionManager _refl = default!;

    FrozenDictionary<string, EntitySystem> _exposedSystems = default!;
    FrozenDictionary<string, object> _exposedServices = default!;
    FrozenDictionary<string, object> _exposedEventCtors = default!;
    FrozenDictionary<string, object> _exposedHelperMethods = default!;
    FrozenDictionary<string, Type> _ComponentNames = default!;
    CoreModules _defaultModules = CoreModules.Preset_SoftSandbox;

    Dictionary<string, Script> _environments = new();
    public IReadOnlyDictionary<string, Script> Environments => _environments;

    public override void Initialize()
    {

        InitExposedSystems();
        InitExposedServices();
        InitExposedComponents();
        InitExposedEvents();
        InitExposedHelperMethods();
        InitCustomConversions();

        UserData.RegisterType(typeof(ServerEntityManager));



    }
    public bool NewEnvironment(string envName)
    {
        if (_environments.ContainsKey(envName))
            return false;
        var env = new Script(_defaultModules);
        env.Globals["systems"] = _exposedSystems;
        env.Globals["services"] = _exposedServices;
        env.Globals["CreateEvent"] = _exposedEventCtors;
        env.Globals["EntMan"] = EntityManager;
        foreach(var kvp in _exposedHelperMethods)
        {
            env.Globals[kvp.Key] = kvp.Value;
        }
        _environments.Add(envName, env);
        return true;
    }

    public bool DeleteEnvironment(string envName)
    {
        if (_environments.TryGetValue(envName, out var env))
            return false;

        _environments.Remove(envName);
        return true;
    }

    public override void Update(float frameTime)
    {
        foreach(var instance in _environments.Values)
        {
            DynValue updateVal = instance.Globals.Get("Update");
            if (updateVal.Type == DataType.Function)
                updateVal.Function.Call(frameTime);
        }
    }

    public DynValue? ExecInEnvironment(string envName, string lua)
    {
        if (!_environments.TryGetValue(envName, out var env))
            return null;

        return env.DoString(lua);
    }

    private string timestr(float ms) => ms < 1000 ? $"{ms}ms" : $"{ms / 1000f}s";

    // It gets worse.
    private void InitExposedSystems()
    {

        Type[] ignoredSystems = new[] { typeof(LuaSystem) };

        var sw = new Stopwatch();
        Log.Info("Registering systems for Lua interpreter...");
        Dictionary<string, EntitySystem> systems = new();
        sw.Start();
        foreach (Type systemType in _refl.GetAllChildren<EntitySystem>())
        {
            if (systemType.IsAbstract)
                continue;
            if (systems.ContainsKey(systemType.Name))
            {
                Log.Warning($"Duplicate system name found for {systemType.Name} while doing Lua registrations. One of them will be unavailable for Lua scripting.");
                continue;
            }
            UserData.RegisterType(systemType);
            systems.Add(systemType.Name, (EntitySystem) _entSysMan.GetEntitySystem(systemType));
        }
        float elapsed = sw.ElapsedMilliseconds;
        Log.Info($"Registered {systems.Count} systems in {timestr(elapsed)} ({timestr(elapsed / systems.Count)} s avg.)");
        _exposedSystems = systems.ToFrozenDictionary();
    }

    private void InitExposedServices()
    {
        var sw = new Stopwatch();
        Log.Info("Registering services for Lua interpreter...");
        Dictionary<string, object> services = new();
        sw.Start();
        foreach (var serviceType in _dependency.GetRegisteredTypes())
        {
            if (serviceType.IsAbstract)
                continue;
            if (services.ContainsKey(serviceType.Name))
            {
                Log.Warning($"Duplicate service name found for {serviceType.Name} while doing Lua registrations. One of them will be unavailable for Lua scripting.");
                continue;
            }

            UserData.RegisterType(serviceType);
            services.Add(serviceType.Name, _dependency.ResolveType(serviceType));
        }
        float elapsed = sw.ElapsedMilliseconds;
        Log.Info($"Registered {services.Count} serivces in {timestr(elapsed)} ({timestr(elapsed / services.Count)} avg.)");

    }

    // This is where it gets worse.
    private void InitExposedEvents()
    {
        var sw = new Stopwatch();
        Log.Info("Registering events for Lua interpreter...");
        sw.Start();
        int count = 0;
        // the voices
        var subComponent = typeof(LuaSystem).GetMethod("_subComponent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var handlerComponent = typeof(LuaSystem).GetMethod("HandleCompEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var subBroadcast = typeof(LuaSystem).GetMethod("_subBroadcast", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var handlerBroadcast = typeof(LuaSystem).GetMethod("HandleBroadcastEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        // no ByRef event support for now, sadge
        var eventsTypes = _refl.GetAllChildren<EntityEventArgs>().Except(_refl.FindTypesWithAttribute<ByRefEventAttribute>());
        Dictionary<string, object> ctors = new();
        foreach (var eventType in eventsTypes)
        {
            if (eventType.IsAbstract)
                continue;

            if (eventType.ContainsGenericParameters)
                continue; // unsupported, sadge

            if (ctors.ContainsKey(eventType.Name))
            {
                Log.Warning($"Duplicate event name found for {eventType.Name} while doing Lua registrations. One of them will be unavailable for Lua scripting.");
                continue;
            }
        
            subComponent.MakeGenericMethod(eventType).Invoke(this, new[] { Delegate.CreateDelegate(typeof(ComponentEventHandler<,>).MakeGenericType(typeof(LuaListenerComponent), eventType), this, handlerComponent.MakeGenericMethod(eventType)) });
            subBroadcast.MakeGenericMethod(eventType).Invoke(this, new[] { Delegate.CreateDelegate(typeof(EntityEventHandler<>).MakeGenericType(eventType), this, handlerBroadcast.MakeGenericMethod(eventType)) });
            UserData.RegisterType(eventType);


            var thisEventCtors = eventType.GetConstructors();
            if(thisEventCtors.Length > 1)
                for(int i = 1; i <= thisEventCtors.Length; i++)
                    ctors.Add($"{eventType.Name}{i}", (params object[] args) => thisEventCtors[i].Invoke(args));

            //foreach (var ctor in eventType.GetConstructors())
            //{
            //    var pr = ctor.GetParameters();
            //    //var ctorParams = pr.Select(pinfo => Expression.Parameter(pinfo.ParameterType, pinfo.Name));
            //    //ctors.Add(eventType.Name, Expression.Lambda(Expression.New(ctor, ctorParams), ctorParams).Compile());
            //    ctors.Add(eventType.Name, (params object[] args) => ctor.Invoke(args));
            //}
            count++;
        }
        float elapsed = sw.ElapsedMilliseconds;
        Log.Info($"Registered {count} events, their handlers and {ctors.Count} ctors in {timestr(elapsed)} ({timestr(elapsed / count)} avg.)");
        _exposedEventCtors = ctors.ToFrozenDictionary();
    }


    private delegate void HandleCompEventDelegate<TEvent>(EntityUid uid, LuaListenerComponent comp, TEvent args) where TEvent : notnull;
    private delegate void HandleBroadcastEventDelegate<TEvent>(TEvent args) where TEvent : notnull;
    //[UsedImplicitly]
    //private void _subComponentRef<TEvent>(ComponentEventRefHandler<LuaListenerComponent, TEvent> handler) where TEvent : notnull
    //    => SubscribeLocalEvent<LuaListenerComponent, TEvent>(handler);
    //
    //[UsedImplicitly]
    //private void _subBroadcastRef<TEvent>(EntityEventRefHandler<TEvent> handler) where TEvent : notnull
    //    => SubscribeLocalEvent<TEvent>(handler);

    [UsedImplicitly]
    private void _subComponent<TEvent>(ComponentEventHandler<LuaListenerComponent, TEvent> handler) where TEvent : notnull
    => SubscribeLocalEvent<LuaListenerComponent, TEvent>(handler);

    [UsedImplicitly]
    private void _subBroadcast<TEvent>(EntityEventHandler<TEvent> handler) where TEvent : notnull
    => SubscribeLocalEvent<TEvent>(handler);


    [UsedImplicitly]
    private void HandleCompEvent<TEvent>(EntityUid uid, LuaListenerComponent comp, TEvent args) where TEvent : notnull
    {
        if (!_environments.TryGetValue(comp.EnvName, out var env))
            return;

        var handler = env.Globals.Get($"{args.GetType().Name}Handler");
        if (handler.Type == DataType.Function)
            handler.Function.Call(uid.Id, args);
    }

    [UsedImplicitly]
    private void HandleBroadcastEvent<TEvent>(TEvent args) where TEvent : notnull
    {
        if (!_environments.TryGetValue("GlobalHandlers", out var env))
            return;

        var handler = env.Globals.Get($"{args.GetType().Name}BroadcastHandler");
        if (handler.Type == DataType.Function)
            handler.Function.Call(args);
    }

    private void InitExposedComponents()
    {
        var sw = new Stopwatch();
        Log.Info("Registering components for Lua interpreter...");
        sw.Start();
        Dictionary<string, Type> compNames = new();
        foreach (var componentType in _refl.FindTypesWithAttribute<RegisterComponentAttribute>())
        {
            if (componentType.IsAbstract)
                continue;

            UserData.RegisterType(componentType);
            compNames.Add(componentType.Name, componentType);
        }
        float elapsed = sw.ElapsedMilliseconds;
        _ComponentNames = compNames.ToFrozenDictionary();
        Log.Info($"Registered {compNames.Count} components in {timestr(elapsed)} ({timestr(elapsed / compNames.Count)} avg.)");
    }

    private void InitExposedHelperMethods()
    {
        Dictionary<string, object> helpers = new();
        helpers["Comp"] = LuaGetComp;
        helpers["EnsureComp"] = LuaEnsureComp;
        helpers["HasComp"] = LuaHasComp;
        helpers["RemComp"] = LuaRemComp;

        _exposedHelperMethods = helpers.ToFrozenDictionary();
    }

    [UsedImplicitly]
    private Component? LuaGetComp(int id, string compName)
    {
        var uid = new EntityUid(id);
        if (TerminatingOrDeleted(uid) ||
            !_ComponentNames.TryGetValue(compName, out var compType) ||
            !EntityManager.TryGetComponent(uid, compType, out var comp) ||
            comp.LifeStage > ComponentLifeStage.Running )
            return null;

        return (Component) comp;
    }

    [Dependency] private readonly IComponentFactory _factory = default!;

    [UsedImplicitly]
    private bool LuaHasComp(int id, string compName)
    {
        var uid = new EntityUid(id);
        if (TerminatingOrDeleted(uid) ||
            !_ComponentNames.TryGetValue(compName, out var compType))
            return false;

        return EntityManager.HasComponent(uid, compType);
    }


    [UsedImplicitly]
    private Component? LuaEnsureComp(int id, string compName)
    {
        var uid = new EntityUid(id);
        if (TerminatingOrDeleted(uid) ||
            !_ComponentNames.TryGetValue(compName, out var compType))
            return null;

        if (!EntityManager.TryGetComponent(uid, compType, out var comp))
        {
            comp = _factory.GetComponent(compType);
            EntityManager.AddComponent(uid, comp);
            return (Component) comp;
        }

        if (comp.LifeStage > ComponentLifeStage.Running)
        {
            EntityManager.RemoveComponent(uid, comp);
            comp = _factory.GetComponent(compType);
            EntityManager.AddComponent(uid, comp);
        }

        return (Component) comp;
    }

    [UsedImplicitly]
    private bool LuaRemComp(int id, string compName)
    {
        var uid = new EntityUid(id);
        if (TerminatingOrDeleted(uid) ||
            !_ComponentNames.TryGetValue(compName, out var compType))
            return false;

        return EntityManager.RemoveComponent(uid, compType);
    }

    private void InitCustomConversions()
    {
        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<EntityUid>(uid => Num(uid.Id));
        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<Vector2>(Vector2ToLua);
        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<EntityCoordinates>(EntCoordinatesToLua);
        Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<MapCoordinates>(MapCoordinatesToLua);

        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Number, typeof(EntityUid), EntityUidToClr);
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Vector2), Vector2ToClr);
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(EntityCoordinates), EntityCoordinatesToClr);
        Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(MapCoordinates), MapCoordinatesToClr);

        object EntityUidToClr(DynValue v)
        {
            return new EntityUid(v.Integer);
        }

        object Vector2ToClr(DynValue v)
        {
            var t = v.Table;
            return new Vector2(t.Get("x").Float, t.Get("y").Float);
        }

        object EntityCoordinatesToClr(DynValue v)
        {
            var t = v.Table;
            return new EntityCoordinates(new EntityUid(t.Get("entity").Integer), t.Get("x").Float, t.Get("y").Float);
        }

        object MapCoordinatesToClr(DynValue v)
        {
            var t = v.Table;
            return new MapCoordinates(t.Get("x").Float, t.Get("y").Float, new MapId(t.Get("mapid").Integer));
        }


        DynValue Vector2ToLua(Script s, Vector2 vec)
        {
            var table = DynValue.NewTable(s);
            table.Table.Set("x", Num(vec.X));
            table.Table.Set("y", Num(vec.Y));
            return table;
        }

        DynValue EntCoordinatesToLua(Script s, EntityCoordinates pos)
        {
            var table = DynValue.NewTable(s);
            table.Table.Set("entity", Num(pos.EntityId.Id));
            table.Table.Set("x", Num(pos.X));
            table.Table.Set("y", Num(pos.Y));
            return table;
        }

        DynValue MapCoordinatesToLua(Script s, MapCoordinates pos)
        {
            var table = DynValue.NewTable(s);
            int mapId = (int) typeof(MapId).GetField("Value")!.GetValue(pos.MapId)!; // mapid value is INTERNAL what the FUCK
            table.Table.Set("mapid", Num(mapId));
            table.Table.Set("x", Num(pos.X));
            table.Table.Set("y", Num(pos.Y));
            return table;
        }
    }
    DynValue Num(double num) => DynValue.NewNumber(num);
}





[RegisterComponent]
public sealed partial class LuaListenerComponent : Component
{
    //[DataField] // Hahaha, no.
    [ViewVariables(VVAccess.ReadWrite)]
    public string EnvName = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = true;
}
