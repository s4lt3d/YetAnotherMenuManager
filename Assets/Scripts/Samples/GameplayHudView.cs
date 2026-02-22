using Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Samples
{
    public class GameplayHudView : UIMenuComponent
    {
        [Header("HUD Elements")]
        public Button pauseButton;
        public Slider healthBar;
    }
}
