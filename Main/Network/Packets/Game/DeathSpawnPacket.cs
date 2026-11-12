using Steamworks;
using UnityEngine;

namespace ThronefallMP.Network.Packets.Game;

// An enemy that spawns when another one dies (Hp.enemyToSpawnOnDeath, God of Afterlife, Craaghelm splitters).
// Only the host spawns these; every peer instantiates the prefab named here with the host's enemy id.
public class DeathSpawnPacket : BasePacket
{
    public const PacketId PacketID = PacketId.DeathSpawn;

    public string PrefabName;
    public ushort Id;
    public Vector3 Position;
    public Quaternion Rotation;
    public bool Elite;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool CanHandle(CSteamID sender)
    {
        return Plugin.Instance.Network.IsServer(sender);
    }

    public override void Send(Buffer writer)
    {
        writer.Write(PrefabName);
        writer.Write(Id);
        writer.Write(Position);
        writer.Write(Rotation);
        writer.Write(Elite);
    }

    public override void Receive(Buffer reader)
    {
        PrefabName = reader.ReadString();
        Id = reader.ReadUInt16();
        Position = reader.ReadVector3();
        Rotation = reader.ReadQuaternion();
        Elite = reader.ReadBoolean();
    }
}
