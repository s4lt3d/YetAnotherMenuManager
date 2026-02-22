using Managers;

namespace Core
{
    public static class GlobalServicesInstaller
    {
        public static void Install()
        {
            Services.TryAdd(new EventManager());
            Services.AddComponent<GameInputManager>();
            Services.AddComponent<SceneLoaderService>();
        }
    }
}