using Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Samples
{
    public class SampleMenuView : UIMenuComponent
    {
        [Header("Buttons")]
        public Button primaryButton;

        public Button secondaryButton;
        public Button tertiaryButton;
        public Button backButton;

        [Header("Additional Close Buttons")]
        public Button[] closeButtons;
    }
}
