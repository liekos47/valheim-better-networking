using System;

using HarmonyLib;
using static CW_Jesse.BetterNetworking.BN_Patch_Compression;

namespace CW_Jesse.BetterNetworking {

    [HarmonyPatch]
    public static class BN_Patch_Compression_Steamworks {

        private const int k_nSteamNetworkingSend_Reliable = 8;                       // https://partner.steamgames.com/doc/api/steamnetworkingtypes
        private const int k_cbMaxSteamNetworkingSocketsMessageSizeSend = 512 * 1024; // https://partner.steamgames.com/doc/api/steamnetworkingtypes

        // compress as the package is queued, not in SendQueuedPackages: a package Steam fails to send stays in the queue and would be compressed again on the next attempt
        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Send), new Type[] { typeof(ZPackage) })]
        [HarmonyPrefix]
        private static void Steamworks_SendCompressedPackage(ref ZSteamSocket __instance, ref ZPackage pkg) {
            if (pkg == null || pkg.Size() == 0) return;
            if (!__instance.IsConnected()) return;
            if (!CompressionStatus.GetSendCompressionStarted(__instance)) return;

            pkg = new ZPackage(Compress(pkg.GetArray()));
        }

        [HarmonyPatch(typeof(ZSteamSocket), nameof(ZSteamSocket.Recv))]
        [HarmonyPostfix]
        private static void Steamworks_ReceiveCompressedPackages(ref ZPackage __result, ref ZSteamSocket __instance) {
            if (!__instance.IsConnected()) return;

            if (__result == null) return;

            byte[] decompressedResult;
            try {
                decompressedResult = Decompress(__result.GetArray());
                __result = new ZPackage(decompressedResult);
                if (!CompressionStatus.GetReceiveCompressionStarted(__instance)) {
                    BN_Logger.LogMessage($"Compression (Steamworks): Received unexpected compressed message from {BN_Utils.GetPeerName(__instance)}");
                    CompressionStatus.SetReceiveCompressionStarted(__instance, true);
                }
            } catch {
                if (CompressionStatus.GetReceiveCompressionStarted(__instance)) {
                    BN_Logger.LogMessage($"Compression (Steamworks): Received unexpected uncompressed message from {BN_Utils.GetPeerName(__instance)}");
                    CompressionStatus.SetReceiveCompressionStarted(__instance, false);
                }
            }
        }
    }
}
