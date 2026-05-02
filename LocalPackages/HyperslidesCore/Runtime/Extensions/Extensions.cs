using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityMainThreadDispatcher;

namespace NSYNK.HyperSlides
{
    /// <summary>
    /// A simple extension class to add some basic functions to predfined types
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// A clamping extension for other not supported types like integers.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="val"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static float RemapClamped(this float aValue, float aIn1, float aIn2, float aOut1, float aOut2)
        {
            float t = (aValue - aIn1) / (aIn2 - aIn1);
            t = Mathf.Clamp(t, 0f, 1f);
            return aOut1 + (aOut2 - aOut1) * t;
        }
    }
}