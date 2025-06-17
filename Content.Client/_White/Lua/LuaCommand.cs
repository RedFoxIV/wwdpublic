using Content.Client.Administration.Managers;
using Content.Shared._White.Lua;
using Content.Shared.Administration;
using Content.Shared.IdentityManagement;
using MathNet.Numerics.LinearAlgebra.Factorization;
using Robust.Client.Console;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Console;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Robust.Client.Console.Commands;

public sealed class LuaScriptCommand : LocalizedCommands
{
    [Dependency] private readonly ILuaScriptClient _luaScriptClient = default!;

    public override string Command => "lua";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_luaScriptClient.CanScript)
        {
            shell.WriteError("You do not have lua scripting permission.");
            return;
        }

        if(args.Length != 1)
        {
            shell.WriteError($"Usage: {Command} [envname]");
            return;
        }

        _luaScriptClient.StartSession(args[0]);
    }
}

public sealed class LuaListEnvCommand : LocalizedCommands
{
    [Dependency] private readonly ILuaScriptClient _luaScriptClient = default!;

    public override string Command => "lualist";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_luaScriptClient.CanScript)
        {
            shell.WriteError("You do not have lua scripting permission.");
            return;
        }

        _luaScriptClient.RequestEnvironmentList();
    }
}

public sealed class LuaNewEnvCommand : LocalizedCommands
{
    [Dependency] private readonly ILuaScriptClient _luaScriptClient = default!;

    public override string Command => "luanew";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_luaScriptClient.CanScript)
        {
            shell.WriteError("You do not have lua scripting permission.");
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError($"Usage: {Command} [envname]");
            return;
        }

        _luaScriptClient.RequestCreateEnvironment(args[0]);
    }
}

public sealed class LuaDelEnvCommand : LocalizedCommands
{
    [Dependency] private readonly ILuaScriptClient _luaScriptClient = default!;

    public override string Command => "luadel";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_luaScriptClient.CanScript)
        {
            shell.WriteError("You do not have lua scripting permission.");
            return;
        }

        _luaScriptClient.RequestEnvironmentList();
    }
}






public interface ILuaScriptClient
{
    void Initialize();

    bool CanScript { get; }
    void StartSession(string env);
    void SendScript(string env, string lua);
    void RequestEnvironmentList();
    void RequestCreateEnvironment(string envName);
    void RequestDeleteEnvironment(string envName);
}

public sealed partial class LuaScriptClient : ILuaScriptClient
{
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IClientNetManager _netManager = default!;
    [Dependency] private readonly IClientConsoleHost _conhost = default!;

    private readonly Dictionary<string, List<LuaScriptingWindow>> _activeConsoles = new();

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgLuaScriptEndViewEnvironment>();
        _netManager.RegisterNetMessage<MsgLuaScriptListEnvironmentsRequest>();
        _netManager.RegisterNetMessage<MsgLuaScriptViewEnvironment>();
        _netManager.RegisterNetMessage<MsgLuaScriptExecute>();
        _netManager.RegisterNetMessage<MsgLuaScriptRequestCreateEnvironment>();
        _netManager.RegisterNetMessage<MsgLuaScriptRequestDeleteEnvironment>();
        _netManager.RegisterNetMessage<MsgLuaScriptState>(ReceiveScriptState);
        _netManager.RegisterNetMessage<MsgLuaScriptEnvironmentDeleted>(ReceiveEnvironmentDeleted);
        _netManager.RegisterNetMessage<MsgLuaScriptListEnvironments>(ReceiveEnvironmentList);
        _netManager.RegisterNetMessage<MsgLuaScriptConfirmView>(ReceiveScriptConfirmView);
    }

    void AddWindow(string env, LuaScriptingWindow window) => _activeConsoles.GetOrNew(env).Add(window); 

    public void RequestEnvironmentList()
    {
        if (!CanScript)
            throw new InvalidOperationException("We do not have scripting permission.");

        _netManager.ClientSendMessage(new MsgLuaScriptListEnvironmentsRequest());
    }

    public void RequestCreateEnvironment(string envName)
    {
        if (!CanScript)
            throw new InvalidOperationException("We do not have scripting permission.");

        if (string.IsNullOrWhiteSpace(envName))
            return;

        var req = new MsgLuaScriptRequestCreateEnvironment();
        req.EnvName = envName;
        _netManager.ClientSendMessage(req);
    }

    public void RequestDeleteEnvironment(string envName)
    {
        if (!CanScript)
            throw new InvalidOperationException("We do not have scripting permission.");

        if (string.IsNullOrWhiteSpace(envName))
            return;

        var req = new MsgLuaScriptRequestDeleteEnvironment();
        req.EnvName = envName;
        _netManager.ClientSendMessage(req);
    }

    private void ReceiveEnvironmentDeleted(MsgLuaScriptEnvironmentDeleted message)
    {
        if (!_activeConsoles.TryGetValue(message.EnvName, out var windows))
            return;
        _activeConsoles.Remove(message.EnvName);
        foreach (var window in windows)
            window.Close();
    }

    private void ReceiveScriptConfirmView(MsgLuaScriptConfirmView message)
    {
        var env = message.EnvName;
        var state = message.EnvState;
        var console = new LuaScriptingWindow();
        console.Title = $"Lua interpreter - {env}";
        console.OnClose += () => ConsoleClosed(message.EnvName, console);
        console.OnSubmit += (string str) => SendScript(message.EnvName, str);
        AddWindow(env, console);
        console.OpenCentered();
        console.UpdateState(message.EnvState);
    }

    private void ReceiveScriptState(MsgLuaScriptState message)
    {
        if (!_activeConsoles.TryGetValue(message.EnvName, out var windows))
            return;

        foreach (var window in windows)
        {
            window.UpdateState(message.LuaScriptState);
            if(!string.IsNullOrEmpty(message.ReturnValue))
                window.ReceiveReturn(message.ReturnValue);
        }
    }

    private void ReceiveEnvironmentList(MsgLuaScriptListEnvironments message)
    {
        _conhost.WriteLine(null, $"Available lua environments: {string.Join(", ", message.Environments)}.");
    }

    public void SendScript(string env, string lua)
    {
        if (!CanScript)
        {
            throw new InvalidOperationException("We do not have scripting permission.");
        }

        if (string.IsNullOrWhiteSpace(env) || string.IsNullOrWhiteSpace(lua))
            return;

        var msg = new MsgLuaScriptExecute();
        msg.EnvName = env;
        msg.LuaScript = lua;
        _netManager.ClientSendMessage(msg);
    }


    public bool CanScript => _admin.HasFlag(AdminFlags.Debug & AdminFlags.Fun);

    public void StartSession(string envName)
    {
        if (!CanScript)
        {
            throw new InvalidOperationException("We do not have scripting permission.");
        }

        if (string.IsNullOrWhiteSpace(envName))
            return;

        var msg = new MsgLuaScriptViewEnvironment();
        msg.EnvName = envName;
        _netManager.ClientSendMessage(msg);
    }

    private void ConsoleClosed(string env, LuaScriptingWindow window)
    {
        if (!_activeConsoles.TryGetValue(env, out var windows))
            return;
        windows.Remove(window);
        if (windows.Count > 0)
            return;

        _activeConsoles.Remove(env);
        var msg = new MsgLuaScriptEndViewEnvironment();
        msg.EnvName = env;
        _netManager.ClientSendMessage(msg);
    }


    private sealed partial class LuaScriptingWindow : DefaultWindow
    {
        public event Action<string>? OnSubmit;

        TextEdit Input = default!;
        OutputPanel ScriptEnvPanel = default!;
        OutputPanel ScriptOutputPanel = default!;

        public LuaScriptingWindow()
        {
            MinSize = new(800, 600);

            Contents.AddChild(new BoxContainer()
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = 8,
                HorizontalExpand = true,

                Children =
                {
                    new BoxContainer()
                    {
                        Orientation = BoxContainer.LayoutOrientation.Horizontal,
                        SeparationOverride = 8,
                        HorizontalExpand = true,

                        Children =
                        {
                            WrapPanel((Input = new()
                            {
                               MinWidth = 500,
                               MinHeight = 400,
                               Margin = new(0,0,8,0),

                            })),
                            WrapPanel((ScriptEnvPanel = new()
                            {
                                MinWidth = 300,
                                MinHeight = 400,
                            })),
                        }
                    },
                    WrapPanel((ScriptOutputPanel = new()
                    {
                        MinHeight = 200,
                        HorizontalExpand = true
                    })),
                }
            });

            Input.OnKeyBindUp += (ev) =>
            {
                var lua = Rope.Collapse(Input.TextRope);
                if (ev.Function == EngineKeyFunctions.MultilineTextSubmit && !string.IsNullOrWhiteSpace(lua))
                    OnSubmit?.Invoke(lua);
            };
        }

        PanelContainer WrapPanel(Control ctrl) => new PanelContainer()
        {
            PanelOverride = new StyleBoxFlat(Color.FromHex("#1E1E1E")),
            Children = { ctrl },
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        public void UpdateState(string newState)
        {
            ScriptEnvPanel.Clear();
            foreach (string line in newState.Split('\n'))
                ScriptEnvPanel.AddText(line);
        }

        public void ReceiveReturn(string returnValue)
        {
            foreach (string line in returnValue.Split('\n'))
                ScriptOutputPanel.AddText(line);
        }
    }
}
