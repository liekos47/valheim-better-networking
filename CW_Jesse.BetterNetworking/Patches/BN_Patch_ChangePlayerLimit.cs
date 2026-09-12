using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;

namespace CW_Jesse.BetterNetworking {
    
    [HarmonyPatch]
    public class BN_Patch_ChangePlayerLimit {
        
        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configPlayerLimit = config.Bind(
                "Dedicated Server",
                "Player Limit",
                10,
                new ConfigDescription(
                    "Requires restart. Changes player limit for dedicated servers."
                , new AcceptableValueRange<int>(1, 127)));
        }

        // Only the 10 compared against GetNrOfPlayers() is the player limit. RPC_PeerInfo loads other
        // ldc.i4.s 10 constants too (a ConnectionStatus value; a second one since Valheim 1.0), which must stay.
        [HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SetPlayerLimit(IEnumerable<CodeInstruction> instructions) {
            MethodInfo getNrOfPlayers = AccessTools.Method(typeof(ZNet), nameof(ZNet.GetNrOfPlayers));
            bool afterGetNrOfPlayers = false;
            int replaced = 0;
            foreach (CodeInstruction i in instructions) {
                if (BN_Utils.isDedicated && afterGetNrOfPlayers && i.Is(OpCodes.Ldc_I4_S, (sbyte)10)) {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)BetterNetworking.configPlayerLimit.Value).MoveLabelsFrom(i);
                } else {
                    yield return i;
                }
                afterGetNrOfPlayers = i.Calls(getNrOfPlayers);
            }
            if (BN_Utils.isDedicated && replaced != 1) {
                BN_Logger.LogWarning($"Player limit: expected 1 limit check in ZNet.RPC_PeerInfo, patched {replaced}; limit may be the Valheim default");
            }
        }
        
    }
}