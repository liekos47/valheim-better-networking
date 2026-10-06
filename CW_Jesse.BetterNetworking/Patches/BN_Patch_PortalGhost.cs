using System.ComponentModel;

using BepInEx.Configuration;
using HarmonyLib;

namespace CW_Jesse.BetterNetworking {

    // Since Valheim 1.0, ZDO.InternalSetPosition calls SetSector before it stores the new position, and
    // ZDOPeer.ZDOSectorInvalidated tests ZDO.GetPosition(). The server therefore asks whether the place an
    // object just left is outside a player's area, not the place it went to. Someone standing at a portal is
    // never told that the player who walked through it is gone, and keeps a ghost of them.
    // (0.217 stored the position first and tested the sector, so it did not have this.)
    // Holding the invalidation back until the position is stored makes the vanilla check see the right place.
    [HarmonyPatch]
    public class BN_Patch_PortalGhost {

        public enum Options_PortalGhostFix {
            [Description("Enabled <b>[default]</b>")]
            @true,
            [Description("Disabled")]
            @false
        }

        private static bool settingPosition = false;
        private static ZDO heldBack = null;

        public static void InitConfig(ConfigFile config) {
            BetterNetworking.configPortalGhostFix = config.Bind(
                "Networking",
                "Portal Ghost Fix",
                Options_PortalGhostFix.@true,
                new ConfigDescription(
                    "Server/host only. Stops players (and effects attached to them) staying visible where they left through a portal.\n" +
                    "Works for players without Better Networking."
                ));
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.InternalSetPosition))]
        [HarmonyPrefix]
        private static void SetPosition_Start() {
            settingPosition = BetterNetworking.configPortalGhostFix.Value == Options_PortalGhostFix.@true;
        }

        [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.ZDOSectorInvalidated))]
        [HarmonyPrefix]
        private static bool HoldBackSectorInvalidated(ZDO zdo) {
            if (!settingPosition) return true;

            heldBack = zdo;
            return false;
        }

        // finalizer, not postfix: the flag must not stay set if InternalSetPosition throws
        [HarmonyPatch(typeof(ZDO), nameof(ZDO.InternalSetPosition))]
        [HarmonyFinalizer]
        private static void SetPosition_End() {
            settingPosition = false;
            if (heldBack == null) return;

            ZDO zdo = heldBack;
            heldBack = null;
            if (ZDOMan.instance != null) ZDOMan.instance.ZDOSectorInvalidated(zdo);
        }
    }
}
