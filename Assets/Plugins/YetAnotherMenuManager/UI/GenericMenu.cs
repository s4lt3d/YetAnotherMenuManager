using Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class GenericMenu : UIMenuComponent
    {
        [SerializeField]
        Button closeButton;
        
        void OnEnable()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }
    }
}