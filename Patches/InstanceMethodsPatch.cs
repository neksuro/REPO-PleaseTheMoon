namespace PleaseTheMoon.Patches
{
    public static class ExtractionPatches
    {
        public static void Prefix(object __instance)
        {
            if (__instance != null)
            {
                PleaseTheMoonPlugin.TrackedExecutionContext.Enter();
            }
        }
        public static void Postfix(object __instance)
        {
            if (__instance != null)
            {
                PleaseTheMoonPlugin.TrackedExecutionContext.Exit();
            }
        }
    }
}
