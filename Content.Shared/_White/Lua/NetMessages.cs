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

    public string LuaScript { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        LuaScript = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(LuaScript);
    }
}

public sealed class MsgLuaScriptState : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public string LuaScriptState { get; set; } = string.Empty;
    public string EnvName { get; set; } = string.Empty;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        LuaScriptState = buffer.ReadString();
        EnvName = buffer.ReadString();
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.Write(LuaScriptState);
        buffer.Write(EnvName);
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
