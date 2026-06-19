using UnityEngine;
using TMPro;

public class ChatMessage : MonoBehaviour
{
    public TextMeshProUGUI messageText;

    public void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }
}
