using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;


namespace NSYNK
{
    public static class UnityHelpers
    {
        public static void ExecuteAfterTime(this MonoBehaviour mono, float time, Action task)
        {
            mono.StartCoroutine(ExecuteAfterTime(time, task));
        }
        public static IEnumerator ExecuteAfterTime(float time, Action task)
        {
            yield return new WaitForSeconds(time);
            task();
        }

        public static void ExecuteNextFrame(this MonoBehaviour mono, Action task)
        {
            mono.StartCoroutine(ExecuteNextFrame(task));
        }
        public static IEnumerator ExecuteNextFrame(Action task)
        {
            yield return new WaitForEndOfFrame();
            task();
        }

        public static void SetLayerRecursively(this GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform)
            {
                child.gameObject.SetLayerRecursively(layer);
            }
        }

        public static bool TrySetFloat(this Material material, string key, float value)
        {
            if (material.HasFloat(key)) {
                material.SetFloat(key, value);
                return true;
            }
            else return false;
        }

        public static void SetShaderPropertyFloat(MonoBehaviour monoBehaviour, MeshRenderer meshRender, string name, float value)
        {
            if (monoBehaviour == null || meshRender == null) return;
#if UNITY_EDITOR


            if (Application.IsPlaying(monoBehaviour))
            {
                if (meshRender == null || meshRender.materials == null)
                    return;

                foreach (Material material in meshRender.materials)
                {
                    // added by Miro on 241106 to remove dependency on NSYNKUtilities.cs because of 2 lines of code..
                    if (material.HasFloat(name))
                        material.SetFloat(name, value);
                                        
                    // original code
                    /*
                    material.TrySetFloat(name, value);                    
                    */
                }

            }
            else
            {
                MaterialPropertyBlock mb = new MaterialPropertyBlock();

                if (meshRender.HasPropertyBlock())
                {
                    meshRender.GetPropertyBlock(mb);
                }

                mb.SetFloat(name, value);
                meshRender.SetPropertyBlock(mb);
            }


#else
        foreach (Material material in meshRender.materials)
        {
            material.SetFloat(name, value);
        }
#endif
        }

        public static void SetShaderPropertyVector(MonoBehaviour monoBehaviour, MeshRenderer meshRender, string name, Vector3 value)
        {
            if (monoBehaviour == null) return;
#if UNITY_EDITOR

            if (Application.IsPlaying(monoBehaviour))
            {
                foreach (Material material in meshRender.materials)
                {
                    material.SetVector(name, value);
                    //material.SetFloat(name, value);
                }

            }
            else
            {

                MaterialPropertyBlock mb = new MaterialPropertyBlock();

                if (meshRender.HasPropertyBlock())
                {
                    meshRender.GetPropertyBlock(mb);
                }

                mb.SetVector(name, value);
                meshRender.SetPropertyBlock(mb);
            }

#else
        foreach (Material material in meshRender.materials)
        {
            material.SetVector(name, value);
        }
#endif
        }

        public static void SetShaderPropertyTexture(MonoBehaviour monoBehaviour, MeshRenderer meshRender, string name, Texture value)
        {
            if (monoBehaviour == null) return;
#if UNITY_EDITOR

            if (Application.IsPlaying(monoBehaviour))
            {
                foreach (Material material in meshRender.sharedMaterials)
                {
                    material.SetTexture(name, value);
                }

            }
            else
            {
                MaterialPropertyBlock mb = new MaterialPropertyBlock();

                if (meshRender.HasPropertyBlock())
                {
                    meshRender.GetPropertyBlock(mb);
                }

                mb.SetTexture(name, value);
                meshRender.SetPropertyBlock(mb);
            }

#else
        foreach (Material material in meshRender.materials)
            {
                material.SetTexture(name, value);
            }
#endif
        }

        public static float RemapRange(float x, float fromMin = 0, float fromMax = 1, float toMin = 0, float toMax = 1)
        {
            return toMin + (x - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        }

    }
    
    public static class AniamtionCurveUtils
    {
        public static float EvaluateNormalizedTime(this AnimationCurve curve, float normalizedTime)
        {
            if(curve.length <= 0)
            {
                Debug.LogError("Given curve has 0 keyframes!");
                return float.NaN;
            }

            // get the time of the first keyframe in the curve
            var start = curve[0].time;

            if(curve.length == 1) 
            {
                // Debug.LogWarning("Given curve has only 1 single keyframe!");
                return start;
            }

            // get the time of the last keyframe in the curve
            var end = curve[curve.length - 1].time;
       
            // get the duration fo the curve
            var duration = end - start;
        
            // get the de-normalized time mapping the input 0 to 1 onto the actual time range 
            // between start and end
            var actualTime = start + Mathf.Clamp(normalizedTime, 0, 1) * duration;

            // finally use that calculated time to actually evaluate the curve
            return curve.Evaluate(actualTime);
        }
    }

    public class LimitedQueue<T> : Queue<T>
    {
        public int Limit { get; set; }

        public LimitedQueue(int limit) : base(limit)
        {
            Limit = limit;
        }

        public new void Enqueue(T item)
        {
            while (Count >= Limit)
            {
                Dequeue();
            }
            base.Enqueue(item);
        }
    }

    public static class CSharpHelpers
    {
        /// <summary>
        /// Converts given string to camel case
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToCamelCase(this string str)
        {
            var words = str.Split(new[] { "_", " " }, StringSplitOptions.RemoveEmptyEntries);
            var leadWord = Regex.Replace(words[0], @"([A-Z])([A-Z]+|[a-z0-9]+)($|[A-Z]\w*)",
                m =>
                {
                    return m.Groups[1].Value.ToLower() + m.Groups[2].Value.ToLower() + m.Groups[3].Value;
                });
            var tailWords = words.Skip(1)
                .Select(word => char.ToUpper(word[0]) + word.Substring(1))
                .ToArray();
            return $"{leadWord}{string.Join(string.Empty, tailWords)}";
        }
        /// <summary>
        /// A list of indices of each occurence of a substring in a string
        /// </summary>
        /// <param name="str">the source string</param>
        /// <param name="value">the substring to find</param>
        /// <returns>List of indices</returns>
        /// <exception cref="ArgumentException"></exception>
        public static List<int> AllIndexesOf(this string str, string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new System.ArgumentException("the string to find may not be empty", "value");
            List<int> indexes = new List<int>();
            for (int index = 0; ; index += value.Length)
            {
                index = str.IndexOf(value, index);
                if (index == -1)
                    return indexes;
                indexes.Add(index);
            }
        }

        private static System.Random rng = new System.Random();

        /// <summary>
        /// Shuffles any collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

}

