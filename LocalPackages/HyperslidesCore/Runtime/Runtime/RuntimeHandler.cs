using System;
using Nakama;
using Newtonsoft.Json.Linq;
using NSYNK.HyperSlides.Network;
using UnityEngine;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A custom runtime update, that updates only on the given amount (hz) in <see cref="Core.Settings.updateRate"/>
    /// </summary>
    public class RuntimeHandler : Singleton<RuntimeHandler>
    {
        public static bool editModeEnabled = false;
        public event Action Tick;
        public event Action SlowTick;
        public event Action SocketTick;

        /// <summary>Time accumulator for tick updates</summary>
        [ReadOnly]
        public float TickTime;
        /// <summary>Time accumulator for slow tick updates like battery updates</summary>
        [ReadOnly]
        public float SlowTickTime;
        /// <summary>Time accumulator for socket check updates</summary>
        [ReadOnly]
        public float SocketCheckTime;

        private void LateUpdate()
        {
            TickTime += Time.deltaTime;
            SlowTickTime += Time.deltaTime;
            SocketCheckTime += Time.deltaTime;

            if (TickTime * 1000 > 1000 / HyperSlidesStateManager.Instance.Settings.updateRate)
            {
                //Debug.Log("Tick");
                Tick?.Invoke();
                TickTime = 0;
            }

            if (SlowTickTime * 1000 > 1000 / HyperSlidesStateManager.Instance.Settings.slowUpdateRate)
            {
                //Debug.Log("SlowTick");
                SlowTick?.Invoke();
                SlowTickTime = 0;
            }

            if (SocketCheckTime * 1000 > 1000 / HyperSlidesStateManager.Instance.Settings.socketCheckRate)
            {
                //Debug.Log("SocketTick");
                SocketTick?.Invoke();
                SocketCheckTime = 0;
            }
        }
    }
}