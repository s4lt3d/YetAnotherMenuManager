using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.UI.Runtime
{
    public enum MenuActionType
    {
        Show = 0,
        Hide = 1,
        Toggle = 2,
        ShowGroup = 3,
        SetPaused = 4,
        PopBack = 5,
        CloseAll = 6,
        CloseAllModals = 7
    }

    public interface IAnimatedMenu
    {
        bool IsTransitioning { get; }
        UniTask OpenAsync(MenuOpenContext context, CancellationToken ct);
        UniTask CloseAsync(MenuCloseContext context, CancellationToken ct);
        void InstantShow(Dictionary<string, object> args = null);
        void InstantHide();
    }

    public interface IGameplayPauseBridge
    {
        void SetGameplayPaused(bool paused);
    }

    public sealed class MenuOpenContext
    {
        public MenuOpenContext(Dictionary<string, object> args = null)
        {
            Args = args;
        }

        public Dictionary<string, object> Args { get; }
    }

    public sealed class MenuCloseContext
    {
    }
}
