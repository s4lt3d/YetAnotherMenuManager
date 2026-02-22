using TMPro;
using UnityEngine;

public class MenuHintController : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI onScreenHintText;

    public void SetHint(string text)
    {
        if (onScreenHintText != null)
            onScreenHintText.text = text;
    }

    public void ClearHint()
    {
        if (onScreenHintText != null)
            onScreenHintText.text = string.Empty;
    }
}