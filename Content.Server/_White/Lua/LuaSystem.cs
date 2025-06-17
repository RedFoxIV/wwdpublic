using Content.Server.Speech;
using Content.Shared.Doors;
using JetBrains.Annotations;
using JetBrains.FormatRipper.Pe;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MoonSharp.Interpreter;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;
using System.Collections.Frozen;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace Content.Server._White.Lua;

public sealed class LuaSystem : EntitySystem
{
    [Dependency] private readonly IEntitySystemManager _entSysMan = default!;
    [Dependency] private readonly IDependencyCollection _dependency = default!;
    [Dependency] private readonly IReflectionManager _refl = default!;
    [Dependency] private readonly ILuaScriptHost _luaHost = default!;

    FrozenDictionary<string, EntitySystem> _exposedSystems = default!;
    FrozenDictionary<string, object> _exposedServices = default!;
    FrozenDictionary<string, object> _exposedEventCtors = default!;
    FrozenDictionary<string, object> _exposedHelperMethods = default!;
    FrozenDictionary<string, Type> _ComponentNames = default!;
    CoreModules _defaultModules = CoreModules.Preset_SoftSandbox;

    Dictionary<string, Script> _environments = new();
    public IReadOnlyDictionary<string, Script> Environments => _environments;

    string[] _baselineKeys = default!;
    public override void Initialize()
    {

        InitExposedSystems();
        InitExposedServices();
        InitExposedComponents();
        InitExposedEvents();
        InitExposedHelperMethods();
        InitCustomConversions();

        UserData.RegisterType(typeof(ServerEntityManager));

        _luaHost.SystemCrutch(this);

        var baseline = CreateScript();
        List<string> keys = new();
        foreach(var dynvalkey in baseline.Globals.Keys)
        {
            string strkey = dynvalkey.CastToString();
            if (strkey is not null)
                keys.Add(strkey);
        }
        _baselineKeys = keys.ToArray();

    }
    public bool CreateEnvironment(string envName)
    {
        if (_environments.ContainsKey(envName))
            return false;
        var env = CreateScript();
        _environments.Add(envName, env);
        return true;
    }

    private Script CreateScript()
    {
        var env = new Script(_defaultModules);
        env.Globals["systems"] = _exposedSystems;
        env.Globals["services"] = _exposedServices;
        env.Globals["createevent"] = _exposedEventCtors;
        env.Globals["entman"] = EntityManager;
        //env.Options.DebugPrint = ()=>{};
        foreach (var kvp in _exposedHelperMethods)
        {
            env.Globals[kvp.Key] = kvp.Value;
        }

        return env;
    }

    public bool DeleteEnvironment(string envName)
    {
        return _environments.Remove(envName);
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

    public string? GetEnvironmentState(string envName)
    {
        if (!_environments.TryGetValue(envName, out var env))
            return null;


        return env.Globals.TableToString(ignoreKeys: _baselineKeys);
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
        MethodInfo GetLocalPrivateMethod(string methodName)
            => typeof(LuaSystem).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var subComponent = GetLocalPrivateMethod("_subComponent");
        var subBroadcast = GetLocalPrivateMethod("_subBroadcast");
        var handlerComponent = GetLocalPrivateMethod("HandleCompEvent");
        var handlerBroadcast = GetLocalPrivateMethod("HandleBroadcastEvent");
        var subComponentRef = GetLocalPrivateMethod("_subComponentRef");
        var subBroadcastRef = GetLocalPrivateMethod("_subBroadcastRef");
        var handlerComponentRef = GetLocalPrivateMethod("HandleCompEventRef");
        var handlerBroadcastRef = GetLocalPrivateMethod("HandleBroadcastEventRef");
        var eventsTypes = _refl.GetAllChildren<EntityEventArgs>().Union(_refl.FindTypesWithAttribute<ByRefEventAttribute>());
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

            if (eventType.IsDefined(typeof(ByRefEventAttribute)))
            {
                subComponentRef.MakeGenericMethod(eventType).Invoke(this, [ Delegate.CreateDelegate(typeof(ComponentEventRefHandler<,>).MakeGenericType(typeof(LuaListenerComponent), eventType), this, handlerComponentRef.MakeGenericMethod(eventType)) ]);
                subBroadcastRef.MakeGenericMethod(eventType).Invoke(this, [ Delegate.CreateDelegate(typeof(EntityEventRefHandler<>).MakeGenericType(eventType), this, handlerBroadcastRef.MakeGenericMethod(eventType))]);
            }
            else
            {
                subComponent.MakeGenericMethod(eventType).Invoke(this, [ Delegate.CreateDelegate(typeof(ComponentEventHandler<,>).MakeGenericType(typeof(LuaListenerComponent), eventType), this, handlerComponent.MakeGenericMethod(eventType)) ]);
                subBroadcast.MakeGenericMethod(eventType).Invoke(this, [ Delegate.CreateDelegate(typeof(EntityEventHandler<>).MakeGenericType(eventType), this, handlerBroadcast.MakeGenericMethod(eventType)) ]);
            }
            UserData.RegisterType(eventType);

            var thisEventCtors = eventType.GetConstructors();
            if (thisEventCtors.Length > 0)
                for (int i = 0; i < thisEventCtors.Length; i++)
                {
                    var ctor = thisEventCtors[i];
                    var ctorParams = ctor.GetParameters();
                    StringBuilder sb = new();
                    foreach (var param in ctorParams)
                    {
                        sb.Append('_');
                        sb.Append(param.ParameterType.Name.ToLower());
                    }
                    var key = $"{eventType.Name}{sb.ToString()}";
                    if (ctors.Keys.Contains(key))
                    {
                        Log.Warning($"Duplicate event constructor found for {eventType.Name} while doing Lua registrations. ({key}) One of them will be unavailable for Lua scripting.");
                        continue;
                    }
                    ctors.Add(key, (params object[] args) => thisEventCtors[i].Invoke(args));
                }
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

    #region event handling stuff
    [UsedImplicitly]
    private void _subComponent<TEvent>(ComponentEventHandler<LuaListenerComponent, TEvent> handler) where TEvent : notnull
    => SubscribeLocalEvent<LuaListenerComponent, TEvent>(handler);

    [UsedImplicitly]
    private void _subBroadcast<TEvent>(EntityEventHandler<TEvent> handler) where TEvent : notnull
    => SubscribeLocalEvent<TEvent>(handler);


    [UsedImplicitly]
    private void _subComponentRef<TEvent>(ComponentEventRefHandler<LuaListenerComponent, TEvent> handler) where TEvent : notnull
    => SubscribeLocalEvent<LuaListenerComponent, TEvent>(handler);

    [UsedImplicitly]
    private void _subBroadcastRef<TEvent>(EntityEventRefHandler<TEvent> handler) where TEvent : notnull
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

    [UsedImplicitly]
    private void HandleCompEventRef<TEvent>(EntityUid uid, LuaListenerComponent comp, ref TEvent args) where TEvent : notnull
    {
        if (!_environments.TryGetValue(comp.EnvName, out var env))
            return;

        DynValue? ret = null;
        var handler = env.Globals.Get($"{args.GetType().Name}Handler");
        if (handler.Type == DataType.Function)
            ret = handler.Function.Call(uid.Id, args);
        if (ret?.Type != DataType.UserData || ret.UserData.Object.GetType() != args.GetType())
            return;
        args = (TEvent) ret.UserData.Object;
    }

    [UsedImplicitly]
    private void HandleBroadcastEventRef<TEvent>(ref TEvent args) where TEvent : notnull
    {
        if (!_environments.TryGetValue("GlobalHandlers", out var env))
            return;

        TEvent newEv = args;
        DynValue? ret = null;
        var handler = env.Globals.Get($"{args.GetType().Name}BroadcastHandler");
        if (handler.Type == DataType.Function)
            ret = handler.Function.Call(args);
        if (ret?.Type != DataType.UserData || ret.UserData.Object.GetType() != args.GetType())
            return;
        args = (TEvent)ret.UserData.Object;
    }
    #endregion

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
            compNames.Add(componentType.Name.Substring(0, componentType.Name.Length - 9), componentType);
        }
        float elapsed = sw.ElapsedMilliseconds;
        _ComponentNames = compNames.ToFrozenDictionary();
        Log.Info($"Registered {compNames.Count} components in {timestr(elapsed)} ({timestr(elapsed / compNames.Count)} avg.)");
    }

    private void InitExposedHelperMethods()
    {
        Dictionary<string, object> helpers = new();
        helpers["comp"] = LuaGetComp;
        helpers["ensurecomp"] = LuaEnsureComp;
        helpers["hascomp"] = LuaHasComp;
        helpers["remcomp"] = LuaRemComp;
        helpers["dirty"] = LuaDirty;

        _exposedHelperMethods = helpers.ToFrozenDictionary();
    }

    string DoName(string name) => name.EndsWith("Component") ? name.Substring(0, name.Length - 9) : name;

    [UsedImplicitly]
    private Component? LuaGetComp(int id, string compName)
    {
        var uid = ValidateEntUid(id);
        var compType = GetCompType(compName);
        if (!EntityManager.TryGetComponent(uid, compType, out var comp) ||
            comp.LifeStage > ComponentLifeStage.Running )
            return null;

        return (Component) comp;
    }

    [Dependency] private readonly IComponentFactory _factory = default!;

    [UsedImplicitly]
    private bool LuaHasComp(int id, string compName)
    {
        var uid = ValidateEntUid(id);
        var compType = GetCompType(compName);
        return EntityManager.HasComponent(uid, compType);
    }


    [UsedImplicitly]
    private Component LuaEnsureComp(int id, string compName)
    {
        var uid = ValidateEntUid(id);
        var compType = GetCompType(compName);

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
        var uid = ValidateEntUid(id);
        var compType = GetCompType(compName);
        return EntityManager.RemoveComponent(uid, compType);
    }
    
    [UsedImplicitly]
    private bool LuaDirty(int id, string compName)
    {
        var uid = ValidateEntUid(id);
        var compType = GetCompTypeNetworked(compName);
        return EntityManager.RemoveComponent(uid, compType);
    }

    EntityUid ValidateEntUid(int id)
    {
        var uid = new EntityUid(id);
        if (TerminatingOrDeleted(uid))
            throw new ScriptRuntimeException($"EntityUid {id} points to an entity that is deleted, being deleting or never existed.");
        return uid;
    }

    Type GetCompTypeNetworked(string compName)
    {
        var compType = GetCompType(compName);
        if (!_ComponentNames[compName].HasCustomAttribute<NetworkedComponentAttribute>())
            throw new ScriptRuntimeException($"{compName} is not networked.");
        return compType;
    }

    Type GetCompType(string compName)
    {
        if (!_ComponentNames.TryGetValue(DoName(compName), out var compType))
            throw new ScriptRuntimeException($"Unknown component {compName}.");
        return compType;
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
            float? xval = t.Get("x").TryFloat();
            float? yval = t.Get("y").TryFloat();
            if(xval is not null && yval is not null)
                return new Vector2(xval.Value, yval.Value);
            xval = t.Get(1).TryFloat();
            yval = t.Get(2).TryFloat();
            if(xval is not null && yval is not null)
                return new Vector2(xval.Value, yval.Value);
            throw new ScriptRuntimeException("Invalid vector");
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


public static class DynValueExt
{

    public static float? TryFloat(this DynValue val) => val.Type == DataType.Number ? val.Float : null;

    public static string DynValueToString(this DynValue val, int indent = 0)
    {
        switch (val.Type)
        {
            case DataType.UserData:
                return _indent(indent, $"(UD:{val.UserData.Object.GetType().ToString()})");
            case DataType.Table:
                return _indent(indent, val.Table.TableToString(indent+1));
            default:
                return _indent(indent, val.ToString());
        }
    }

    private static string _indent(int i, string s) => $"{new string(' ', i * 2)}{s}";


    public static string TableToString(this Table table, int indent = 0, params string[] ignoreKeys)
    {
        if (indent > 5)
            return _indent(indent, "...");

        var sb = new StringBuilder();
        sb.Append(_indent(indent, "[\n"));
        indent++;
        foreach(var pair in table.Pairs)
        {
            if (pair.Key.CastToString() is string strkey && ignoreKeys.Contains(strkey))
                continue;
            sb.Append(_indent(indent, "["));
            sb.Append(pair.Key.DynValueToString(indent));
            sb.Append(" = ");
            sb.Append(pair.Value.DynValueToString(indent));
            sb.Append("],\n");
        }
        sb.Length-=2;
        indent--;
        sb.Append('\n');
        sb.Append(_indent(indent, "]\n"));
        return sb.ToString();
    }
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
