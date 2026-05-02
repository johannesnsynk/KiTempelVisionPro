using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using NSYNK.HyperSlides.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides.UI
{
    /// <summary>
    /// A UI button that extends the default one by adding the confirmation popup as an option
    /// </summary>
    public class UIButton : Button
    {
        public delegate void UpdateUI();
        public static UpdateUI onUpdateUI;

        [Header("Needs confirmation before running the event?")]
        public bool useGlobalTimeout = false;
        public bool needsConfirmation = false;
        public string confirmationPrompt = "Do you want to continue?";

        protected override void OnEnable()
        {
            base.OnEnable();

            XRUIManager.timer.Elapsed += EnableUI;
            XRUIManager.dissolveTimer.Elapsed += EnableUI;

            if (useGlobalTimeout)
            {
                onUpdateUI += EnableInteraction;
                EnableInteraction();
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            XRUIManager.timer.Elapsed -= EnableUI;
            XRUIManager.dissolveTimer.Elapsed -= EnableUI;

            if (useGlobalTimeout)
                onUpdateUI -= EnableInteraction;
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!interactable)
                return;

            if (needsConfirmation)
                WaitForConfirmation();
            else
            {
                onClick?.Invoke();

                if (!useGlobalTimeout)
                    return;

                XRUIManager.StartTimer(XRUIManager.timer);
                onUpdateUI?.Invoke();
                // Dispatcher.Enqueue(() =>
                // {
                //     if(this.gameObject.name == "XRUIButton_Next")
                //         Debug.Log($"[UIButton][{this.gameObject.name}] Enable interaction|PointerClick: TimerRunning: {XRUIManager.TimerRunning()}; LoadingActionInProgress: {XRUIManager.Instance.LoadingActionInProgress}");
                // });
            }
        }

        private void EnableUI(object sender, ElapsedEventArgs e)
        {
            onUpdateUI?.Invoke();
            // Dispatcher.Enqueue(() =>
            // {
            //     if(this.gameObject.name == "XRUIButton_Next")
            //         Debug.Log($"[UIButton][{this.gameObject.name}] Enable interaction|EnableUI: TimerRunning: {XRUIManager.TimerRunning()}; LoadingActionInProgress: {XRUIManager.Instance.LoadingActionInProgress}");
            // });
        }

        public virtual void EnableInteraction()
        {
            Dispatcher.Enqueue(() => {
                interactable = !XRUIManager.TimerRunning() && !XRUIManager.Instance.LoadingActionInProgress;
                // interactable = !XRUIManager.TimerRunning();
            });
        }

        private async void WaitForConfirmation()
        {
            Task<bool> userConfirmation = XRUIManager.RequestConfirmation(confirmationPrompt);
            await userConfirmation;

            if (userConfirmation.Result)
            {
                onClick?.Invoke();

                if (!useGlobalTimeout)
                    return;

                XRUIManager.StartTimer(XRUIManager.timer);
                onUpdateUI?.Invoke();
            }
        }
    }
}