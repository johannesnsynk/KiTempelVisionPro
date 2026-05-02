using System;
using TMPro;
using UnityEngine;

namespace NSYNK.HyperSlides.UI
{
    public class UISystemClock : MonoBehaviour
    {
        private TextMeshProUGUI clockText;

        void Awake()
        {
            clockText = GetComponent<TextMeshProUGUI>();
        }

        void OnEnable()
        {
            RuntimeHandler.tick += UpdateTime;
        }

        void OnDisable()
        {
            RuntimeHandler.tick -= UpdateTime;
        }

        private void UpdateTime()
        {
            if (clockText)
                clockText.text = DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss");
        }
    }
}