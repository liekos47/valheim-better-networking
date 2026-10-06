using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CW_Jesse.BetterNetworking {

    // Vanilla holds a player on the loading screen for 8 seconds after a portal, however quickly the
    // destination arrives, because a client cannot tell what the server has not sent it yet.
    // With Better Networking on both ends it can:
    //   1. on arrival the client sends its new position at once (vanilla waits up to 2 s) and then a marker;
    //   2. the server echoes the marker, so every ZDOData package after the echo was built for the destination;
    //   3. ZDOMan.SendZDOs only cuts a package short once it is larger than its budget, and the budget is
    //      never under 2048 bytes, so a package smaller than that carried everything the server had to send.
    // Once that is seen (or the server goes quiet) the 8 second floor is dropped. Vanilla's own checks that the
    // destination is loaded still apply. Against a server without this the echo never comes and nothing changes.
    [HarmonyPatch]
    public class BN_Patch_PortalTravel {

        public enum Options_FastPortalTravel {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled [Valheim default]")]
            @false
        }

        private const string RPC_AREA_SYNC = "CW_Jesse.BetterNetworking.PortalAreaSync";

        private const float VANILLA_ARRIVAL_TIME = 2f;         // Player.UpdateTeleport moves the player after this long
        private const float VANILLA_DISTANT_TELEPORT_WAIT = 8f;
        private const int MIN_SEND_BUDGET = 2048;              // ZDOMan.SendZDOs
        private const float QUIET_TIME = 1f;                   // no ZDOData for this long after the echo: nothing left to send

        private static readonly MethodInfo sendServerSyncPlayerData = AccessTools.Method(typeof(ZNet), "SendServerSyncPlayerData");

        // the local player's current teleport
        private static bool arrived = false;
        private static bool serverAnswered = false;
        private static bool destinationSynced = false;
        private static int marker = 0;
        private static float arrivalTime;
        private static float lastDataTime;

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configFastPortalTravel = config.Bind(
                "Networking",
                "Fast Portal Travel",
                Options_FastPortalTravel.@true,
                new ConfigDescription(
                    "Leave the portal loading screen as soon as the destination has arrived instead of always waiting 8 seconds.\n" +
                    "Needs Better Networking 2.3.5+ on the server as well; otherwise portals behave as in vanilla."
                ));
        }

        [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
        [HarmonyPostfix]
        private static void RegisterRPC(ZNetPeer peer) {
            peer.m_rpc.Register<int>(RPC_AREA_SYNC, RPC_AreaSync);
        }

        private static void RPC_AreaSync(ZRpc rpc, int peerMarker) {
            if (ZNet.instance.IsServer()) {
                rpc.Invoke(RPC_AREA_SYNC, new object[] { peerMarker });
                return;
            }

            if (!arrived || peerMarker != marker) return;
            serverAnswered = true;
            lastDataTime = Time.time;
        }

        [HarmonyPatch(typeof(Player), "UpdateTeleport")]
        [HarmonyPostfix]
        private static void OnTeleportUpdate(Player __instance, bool ___m_teleporting, bool ___m_distantTeleport, float ___m_teleportTimer, Vector3 ___m_teleportTargetPos) {
            if (__instance != Player.m_localPlayer) return;
            if (!___m_teleporting) {
                arrived = false;
                return;
            }
            if (arrived || !___m_distantTeleport || ___m_teleportTimer <= VANILLA_ARRIVAL_TIME) return;
            if (BetterNetworking.configFastPortalTravel.Value != Options_FastPortalTravel.@true) return;
            if (ZNet.instance == null) return;

            arrived = true;
            serverAnswered = false;
            destinationSynced = false;
            arrivalTime = Time.time;
            marker++;

            if (ZNet.instance.IsServer()) {
                destinationSynced = true; // a host already holds the whole world
                return;
            }

            ZNetPeer server = ZNet.instance.GetServerPeer();
            if (server == null || sendServerSyncPlayerData == null) return;

            ZNet.instance.SetReferencePosition(___m_teleportTargetPos);
            sendServerSyncPlayerData.Invoke(ZNet.instance, new object[] { server });
            server.m_rpc.Invoke(RPC_AREA_SYNC, new object[] { marker }); // must follow the position so the echo follows it too
        }

        [HarmonyPatch(typeof(ZDOMan), "RPC_ZDOData")]
        [HarmonyPrefix]
        private static void OnZDOData(ZPackage pkg) {
            if (!serverAnswered || destinationSynced) return;

            lastDataTime = Time.time;
            if (pkg.Size() < MIN_SEND_BUDGET) SetDestinationSynced();
        }

        private static void SetDestinationSynced() {
            destinationSynced = true;
            BN_Logger.LogInfo($"Portal travel: destination synced {(Time.time - arrivalTime).ToString("0.0")} s after arrival");
        }

        private static float DistantTeleportWait() {
            if (!arrived) return VANILLA_DISTANT_TELEPORT_WAIT;

            if (!destinationSynced && serverAnswered && Time.time - lastDataTime > QUIET_TIME) SetDestinationSynced();
            return destinationSynced ? 0f : VANILLA_DISTANT_TELEPORT_WAIT;
        }

        [HarmonyPatch(typeof(Player), "UpdateTeleport")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ReplaceDistantTeleportWait(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> code = instructions.ToList();
            List<CodeInstruction> waits = code.FindAll(i => i.Is(OpCodes.Ldc_R4, VANILLA_DISTANT_TELEPORT_WAIT));
            if (waits.Count != 1) {
                BN_Logger.LogWarning($"Portal travel: expected 1 distant teleport wait in Player.UpdateTeleport, found {waits.Count}; portals left as in Valheim");
                return code;
            }

            waits[0].opcode = OpCodes.Call;
            waits[0].operand = AccessTools.Method(typeof(BN_Patch_PortalTravel), nameof(DistantTeleportWait));
            return code;
        }
    }
}
