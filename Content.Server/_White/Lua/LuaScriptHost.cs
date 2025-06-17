using Content.Shared._White.Lua;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using MoonSharp.Interpreter;
using Robust.Server.Console;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Reflection;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Content.Shared.Administration.Notes.AdminMessageEuiState;

namespace Content.Server._White.Lua;

class LuaScriptHost : ILuaScriptHost
{
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IConGroupController _conGroupController = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IReflectionManager _reflectionManager = default!;
    [Dependency] private readonly IDependencyCollection _dependencyCollection = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly ISharedAdminManager _admin = default!;


    readonly Dictionary<ICommonSession, List<string>> _scriptSessions = new();
    readonly Dictionary<string, List<ICommonSession>> _scriptSessionsInv = new();

    private ISawmill _sawmill = default!;

    private bool CanLua(ICommonSession session) => _admin.HasAdminFlag(session, AdminFlags.Debug & AdminFlags.Fun);

    public void SystemCrutch(LuaSystem newlua) => lua ??= newlua;

    private LuaSystem? lua { get; set; }

    [MemberNotNullWhen(true, nameof(lua))]
    bool _luaAvailable => lua is not null;

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("script");

        _netManager.RegisterNetMessage<MsgLuaScriptEndViewEnvironment>(ReceiveScriptEndViewEnv);
        _netManager.RegisterNetMessage<MsgLuaScriptExecute>(ReceiveScriptEval);
        _netManager.RegisterNetMessage<MsgLuaScriptViewEnvironment>(ReceiveScriptViewEnv);
        _netManager.RegisterNetMessage<MsgLuaScriptListEnvironmentsRequest>(ReceiveScriptListEnvRequest);
        _netManager.RegisterNetMessage<MsgLuaScriptRequestCreateEnvironment>(ReceiveCreateEnvironmentRequest);
        _netManager.RegisterNetMessage<MsgLuaScriptRequestDeleteEnvironment>(ReceiveDeleteEnvironmentRequest);
        //_netManager.RegisterNetMessage<MsgScriptCompletion>(ReceiveScriptCompletion);
        //_netManager.RegisterNetMessage<MsgScriptCompletionResponse>();
        _netManager.RegisterNetMessage<MsgLuaScriptListEnvironments>();
        _netManager.RegisterNetMessage<MsgLuaScriptState>();
        _netManager.RegisterNetMessage<MsgLuaScriptConfirmView>();


        _playerManager.PlayerStatusChanged += PlayerManagerOnPlayerStatusChanged;
    }

    void NoAccessWarning(ICommonSession session) => _sawmill.Warning("Client {0} tried to access Lua scripting without permissions.", session);
    private void ReceiveDeleteEnvironmentRequest(MsgLuaScriptRequestDeleteEnvironment message)
    {
        if (!_luaAvailable || !_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session))
            return;

        if (!CanLua(session))
        {
            NoAccessWarning(session);
            return;
        }

        var env = message.EnvName;
        if (string.IsNullOrWhiteSpace(env))
            return;

        if (!lua.DeleteEnvironment(env))
            return;

        var msg = new MsgLuaScriptEnvironmentDeleted();
        msg.EnvName = env;

        foreach (var loser in _scriptSessionsInv[env])
        {
            _scriptSessions[loser].Remove(env);
            _netManager.ServerSendMessage(msg, loser.Channel);
        }
        _scriptSessionsInv.Remove(env);
    }

    private void ReceiveCreateEnvironmentRequest(MsgLuaScriptRequestCreateEnvironment message)
    {
        if (!_luaAvailable || !_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session))
            return;

        if (!CanLua(session))
        {
            NoAccessWarning(session);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.EnvName))
            return;

        if (!lua.CreateEnvironment(message.EnvName))
            return;

    }

    private void PlayerManagerOnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        // GC it up.
        _scriptSessions.Remove(e.Session);
    }

    private void ReceiveScriptEndViewEnv(MsgLuaScriptEndViewEnvironment message)
    {
        if (!_luaAvailable || !_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session))
            return;

        if (!CanLua(session))
        {
            NoAccessWarning(session);
            return;
        }

        TryRemoveFromCorrespondingList(_scriptSessions, session, message.EnvName);
        TryRemoveFromCorrespondingList(_scriptSessionsInv, message.EnvName, session);
    }

    private static bool TryAddToCorrespondingList<TKey, TVal>(Dictionary<TKey, List<TVal>> dict, TKey key, TVal val) where TKey : notnull
    {
        var list = dict.GetOrNew(key);
        if (list.Contains(val))
            return false;
        list.Add(val);
        return true;
    }
    private static bool TryRemoveFromCorrespondingList<TKey, TVal>(Dictionary<TKey, List<TVal>> dict, TKey key, TVal val) where TKey : notnull
    {
        return dict.GetOrNew(key).Remove(val);
    }

    private void ReceiveScriptListEnvRequest(MsgLuaScriptListEnvironmentsRequest message)
    {
        if (!_luaAvailable || !_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session))
            return;

        if (!CanLua(session))
        {
            NoAccessWarning(session);
            return;
        }

        var reply = new MsgLuaScriptListEnvironments();
        reply.Environments = lua.Environments.Keys.ToArray();
        _netManager.ServerSendMessage(reply, message.MsgChannel);
    }

    private void ReceiveScriptViewEnv(MsgLuaScriptViewEnvironment message)
    {
        if (!_luaAvailable || !_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session))
            return;

        if (!CanLua(session))
        {
            NoAccessWarning(session);
            return;
        }

        if (!lua.Environments.Keys.Contains(message.EnvName))
            return;

        TryAddToCorrespondingList(_scriptSessions, session, message.EnvName);
        TryAddToCorrespondingList(_scriptSessionsInv, message.EnvName, session);
        var confirm = new MsgLuaScriptConfirmView();
        confirm.EnvName = message.EnvName;
        confirm.EnvState = lua.GetEnvironmentState(message.EnvName) ?? "[[No environment data]]";
        _netManager.ServerSendMessage(confirm, message.MsgChannel);
    }

    private async void ReceiveScriptEval(MsgLuaScriptExecute message)
    {
        if (!_luaAvailable || !_playerManager.TryGetSessionByChannel(message.MsgChannel, out var session))
            return;

        if (!CanLua(session))
        {
            NoAccessWarning(session);
            return;
        }
        if (!_scriptSessions.TryGetValue(session, out var envs) ||
            !envs.Contains(message.EnvName))
        {
            _sawmill.Warning("Client {0} attempted to execute lua code without initializing lua session first.", session);
            return;
        }

        if (string.IsNullOrWhiteSpace(message.EnvName) || string.IsNullOrWhiteSpace(message.LuaScript))
            return;

        _sawmill.Info("Client {0} executed the following lua code: {1}", session, message.LuaScript);
        string? ret = null;
        try
        {
            ret = lua.ExecInEnvironment(message.EnvName, message.LuaScript)?.DynValueToString();
        }
        catch(InterpreterException e)
        {
            ret = $"{e.DecoratedMessage}";
        }
        UpdateStateForClients(ret, message.EnvName);
    }

    private void UpdateStateForClients(string? ret, string env)
        => UpdateStateForClients(ret, env, _scriptSessionsInv[env].ToArray());

    private void UpdateStateForClients(string? ret, string env, params ICommonSession[] sessions)
    {
        var replyMessage = new MsgLuaScriptState();
        replyMessage.EnvName = env;
        replyMessage.ReturnValue = ret ?? "[[No return value]]";
        replyMessage.LuaScriptState = lua!.GetEnvironmentState(env) ?? "[[No environment data]]"; // todo caching
        foreach (var viewerSession in sessions)
            _netManager.ServerSendMessage(replyMessage, viewerSession.Channel);
    }

    //private sealed class LuaScriptInstance
    //{
    //    public Workspace HighlightWorkspace { get; } = new AdhocWorkspace();
    //    public StringBuilder InputBuffer { get; } = new();
    //    public FormattedMessage OutputBuffer { get; } = new();
    //    public bool RunningScript { get; set; }
    //
    //    public ScriptGlobals Globals { get; }
    //    public ScriptState? State { get; set; }
    //
    //    public (string[] imports, string code)? AutoImportRepeatBuffer;
    //
    //    public LuaScriptInstance(IReflectionManager reflection, IDependencyCollection dependency)
    //    {
    //        Globals = new ScriptGlobalsImpl(this, reflection, dependency);
    //    }
    //}

    //private sealed class ScriptGlobalsImpl : ScriptGlobals
    //{
    //    private readonly IReflectionManager _reflectionManager;
    //
    //    private readonly LuaScriptInstance _scriptInstance;
    //
    //    public ScriptGlobalsImpl(
    //        LuaScriptInstance scriptInstance,
    //        IReflectionManager refl,
    //        IDependencyCollection dependency)
    //        : base(dependency)
    //    {
    //        _reflectionManager = refl;
    //        _scriptInstance = scriptInstance;
    //    }
    //
    //    protected override void WriteSyntax(object toString)
    //    {
    //        if (_scriptInstance.RunningScript && toString?.ToString() is { } code)
    //        {
    //            var options = ScriptInstanceShared.GetScriptOptions(_reflectionManager);
    //            var script = CSharpScript.Create(code, options, typeof(ScriptGlobals));
    //            script.Compile();
    //
    //            var syntax = new FormattedMessage();
    //            ScriptInstanceShared.AddWithSyntaxHighlighting(script, syntax, code, _scriptInstance.HighlightWorkspace);
    //
    //            _scriptInstance.OutputBuffer.AddMessage(syntax);
    //        }
    //    }
    //
    //    public override void write(object toString)
    //    {
    //        if (_scriptInstance.RunningScript && toString.ToString() is { } value)
    //        {
    //            _scriptInstance.OutputBuffer.AddText(value);
    //        }
    //    }
    //
    //    public override void show(object obj)
    //    {
    //        write(ScriptInstanceShared.SafeFormat(obj));
    //    }
    //}
}


public interface ILuaScriptHost
{
    public void Initialize();
    public void SystemCrutch(LuaSystem ass);
}
