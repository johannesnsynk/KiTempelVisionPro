using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NSYNK.HyperSlides.Network;
using NSYNK.HyperSlides.Core;
using System;
using UnityEngine.Events;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.UI
{
    public class UIPresentationList : MonoBehaviour
    {
        public bool triggerValueChange = true;

        private TMP_Dropdown dropdown;

        private void Awake() => dropdown = GetComponent<TMP_Dropdown>();

        private void OnEnable()
        {
            XRDataManager.onDataReceived += UpdateDropdownOptions;
            XRNetworkManager.onNetworkSlideUpdate += UpdateDropdownFromNetwork;

            dropdown.onValueChanged.AddListener(delegate
            {
                if (!triggerValueChange)
                    return;

                XRSlideManager.SetPresentation(XRDataManager.allPresentations[dropdown.value]);
                XRNetworkManager.Instance.SendSlideUpdate(XRDataManager.allPresentations[dropdown.value], 0);
            });

            UpdateDropdownFromNetwork(XRNetworkObjects.networkSessionState);
        }

        private void OnDisable()
        {
            XRDataManager.onDataReceived -= UpdateDropdownOptions;
            XRNetworkManager.onNetworkSlideUpdate -= UpdateDropdownFromNetwork;
            dropdown.onValueChanged.RemoveAllListeners();
        }

        private void UpdateDropdownFromNetwork(XRNetworkObjects.XRSessionState networkObject)
        {
            Dispatcher.Enqueue(() =>
            {
                UpdateDropdownOptions();

                XRPresentation foundPresentation = XRDataManager.allPresentations.Find(p => p.id == networkObject.presentationId);
                TMP_Dropdown.OptionData foundOptionData = null;

                if (foundPresentation != null)
                    foundOptionData = dropdown.options.Find(o => o.text == foundPresentation.title);

                if (foundOptionData != null)
                    dropdown.SetValueWithoutNotify(dropdown.options.IndexOf(foundOptionData));
            });
        }

        private void UpdateDropdownOptions()
        {
            if(dropdown)
            {
                dropdown.ClearOptions();

                List<TMP_Dropdown.OptionData> newOptions = new();

                foreach (XRPresentation xRPresentation in XRDataManager.allPresentations)
                    newOptions.Add(new TMP_Dropdown.OptionData(xRPresentation.title));

                dropdown.AddOptions(newOptions);
            }
        }
    }
}