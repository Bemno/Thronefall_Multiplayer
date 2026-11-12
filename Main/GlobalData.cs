using ThronefallMP.Network.Packets.Game;
using UnityEngine.PlayerLoop;

namespace ThronefallMP;

public struct InternalGlobalData
{
    public int Balance;
    public int NetWorth;
    public int EnergyCores;
}

public static class GlobalData
{
    public static InternalGlobalData Internal;
    
    // TODO: Add a set from Balance -> Player.balance in
    //       TutorialManager.Update
    //       DebugCoinDisplay.Update
    //       LocalGamestate.WaitThenTriggerEndOfMatchScreen

    public static int LocalBalanceDelta;
    public static int Balance
    {
        get => Plugin.Instance.Network.Server ? Internal.Balance : Internal.Balance + LocalBalanceDelta;
        set
        {
            if (Plugin.Instance.Network.Server)
            {
                Internal.Balance = value;
            }
            else
            {
                LocalBalanceDelta = value - Internal.Balance;
            }
        }
    }
    
    public static int NetWorth => Internal.NetWorth;

    // Craaghelm's second currency, pooled like gold: the server owns the value, clients add a local delta until
    // the server confirms or cancels their spend.
    public static int LocalEnergyCoreDelta;
    public static int EnergyCores
    {
        get => Plugin.Instance.Network.Server ? Internal.EnergyCores : Internal.EnergyCores + LocalEnergyCoreDelta;
        set
        {
            if (Plugin.Instance.Network.Server)
            {
                Internal.EnergyCores = value;
            }
            else
            {
                LocalEnergyCoreDelta = value - Internal.EnergyCores;
            }
        }
    }
}