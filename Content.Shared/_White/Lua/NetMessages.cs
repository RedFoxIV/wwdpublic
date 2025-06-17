using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._White.Lua;



public sealed class MsgLuaScriptExecute : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string EnvName { get; set; } = string.Empty;
    public string LuaScript { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        EnvName = buffer.ReadString();
        LuaScript = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(EnvName);
        buffer.Write(LuaScript);
    }
}

public sealed class MsgLuaScriptState : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string LuaScriptState { get; set; } = string.Empty;
    public string EnvName { get; set; } = string.Empty;
    public string ReturnValue { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        LuaScriptState = buffer.ReadString();
        EnvName = buffer.ReadString();
        ReturnValue = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(LuaScriptState);
        buffer.Write(EnvName);
        buffer.Write(ReturnValue);
    }
}


public sealed class MsgLuaScriptReturn : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string Return { get; set; } = string.Empty;
    public string EnvName { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Return = buffer.ReadString();
        EnvName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(Return);
        buffer.Write(EnvName);
    }
}



public sealed class MsgLuaScriptViewEnvironment : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string EnvName { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        EnvName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(EnvName);
    }
}


public sealed class MsgLuaScriptConfirmView : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string EnvName { get; set; } = string.Empty;
    public string EnvState { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        EnvName = buffer.ReadString();
        EnvState = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(EnvName);
        buffer.Write(EnvState);
    }
}


public sealed class MsgLuaScriptEndViewEnvironment : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string EnvName { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        EnvName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(EnvName);
    }
}

public sealed class MsgLuaScriptListEnvironmentsRequest : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
    }
}

public sealed class MsgLuaScriptListEnvironments : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string[] Environments = default!;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Environments = new string[buffer.ReadByte()];
        for(int i = 0; i < Environments.Length; i++)
        {
            Environments[i] = buffer.ReadString();
        }
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write((byte)Environments.Length);
        foreach (var env in Environments)
            buffer.Write(env);
    }
}


public sealed class MsgLuaScriptRequestCreateEnvironment : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string EnvName { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        EnvName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(EnvName);
    }
}

public sealed class MsgLuaScriptRequestDeleteEnvironment : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string EnvName { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        EnvName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(EnvName);
    }
}


public sealed class MsgLuaScriptEnvironmentDeleted : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string EnvName { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        EnvName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(EnvName);
    }
}
