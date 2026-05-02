using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Threading.Tasks;

namespace NSYNK.HyperSlides.UI
{
    public class UIPopUp : Singleton<UIPopUp>
    {
        public enum UserConfirmation { INACTIVE, WAITING, CONFIRMED, DISMISSED };
        public UserConfirmation userConfirmation = UserConfirmation.WAITING;
        public GameObject uiContent;
        public TMPro.TextMeshProUGUI promptTextfield;

        private Canvas canvas;

        public override void Awake()
        {
            base.Awake();

            // uiContent.SetActive(false);
            canvas = GetComponent<Canvas>();
            canvas.worldCamera = Camera.main;
        }

        public async Task<bool> WaitForUserInput(string message)
        {
            userConfirmation = UserConfirmation.WAITING;

            // canvas.GetComponent<GraphicRaycaster>().enabled = true;
            promptTextfield.text = message;

            while (userConfirmation == UserConfirmation.WAITING)
                await Task.Yield();

            // canvas.GetComponent<GraphicRaycaster>().enabled = false;
            promptTextfield.text = "";

            bool confirmed = userConfirmation == UserConfirmation.CONFIRMED;
            userConfirmation = UserConfirmation.INACTIVE;

            return confirmed;
        }

        public void Confirm(bool confirm) {
            userConfirmation = confirm ? UserConfirmation.CONFIRMED : UserConfirmation.DISMISSED;
        }
    }
}