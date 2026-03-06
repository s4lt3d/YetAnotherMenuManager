using UnityEngine;

namespace Core
{
    public static class ServicesBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeSceneLoad()
        {
            Services.PrepareForStartup();
            GlobalServicesInstaller.Install();
        }
    }
}