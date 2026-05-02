using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    [RequireComponent(typeof(TMPro.TextMeshProUGUI))]
    public class UITextFontSize : MonoBehaviour
    {
        public float minFontSize = 18;
        public float maxFontSize = 18;

        private TMPro.TextMeshProUGUI textComp;
        private float fontSize;

        private void OnEnable()
        {
            fontSize = PlayerPrefs.GetFloat(gameObject.name, minFontSize);

            textComp = GetComponent<TMPro.TextMeshProUGUI>();
            textComp.fontSize = fontSize;
        }

        public void IncreaseFontSize()
        {
            fontSize = Mathf.Clamp(fontSize + 4, minFontSize, maxFontSize);
            SaveFontSize();
        }

        public void DecreaseFontSize()
        {
            fontSize = Mathf.Clamp(fontSize - 4, minFontSize, maxFontSize);
            SaveFontSize();
        }

        private void SaveFontSize()
        {
            textComp.fontSize = fontSize;
            PlayerPrefs.SetFloat(gameObject.name, fontSize);
        }
    }
}