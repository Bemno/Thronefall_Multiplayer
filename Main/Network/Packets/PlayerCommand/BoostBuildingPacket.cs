using Steamworks;

namespace ThronefallMP.Network.Packets.PlayerCommand;

// Craaghelm building boost (energy cores spent on a finished building). A client sends it to the server, which
// charges the pool and rebroadcasts it to everyone; the server's own boosts are broadcast directly.
public class BoostBuildingPacket : BasePacket
{
    public const PacketId PacketID = PacketId.BoostBuilding;

    public ushort PlayerID;
    public ushort BuildingId;

    public override PacketId TypeID => PacketID;
    public override int DeliveryMask => Constants.k_nSteamNetworkingSend_Reliable;
    public override Channel Channel => Channel.Resources;

    public override void Send(Buffer writer)
    {
        writer.Write(PlayerID);
        writer.Write(BuildingId);
    }

    public override void Receive(Buffer reader)
    {
        PlayerID = reader.ReadUInt16();
        BuildingId = reader.ReadUInt16();
    }
}
