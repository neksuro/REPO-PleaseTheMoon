using HarmonyLib;

namespace PleaseTheMoon.Patches
{
    [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.GameEventIsHalloween))]
    public static class HalloweenEventPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ref bool __result)
        {
            if (PleaseTheMoonPlugin.TrackedExecutionContext.IsActive)
            {
                __result = PleaseTheMoonPlugin.canSpawn;
            }
        }
    }
}
