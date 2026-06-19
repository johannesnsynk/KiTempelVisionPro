using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace NSYNK
{
    public static class Easing
    {
        public enum Ease { Linear, Clerp, EaseInQuad, EaseOutQuad, EaseInOutQuad, EaseInBack, EaseOutBack, EaseInElastic, EaseOutElastic, EaseInSine, Spring, EaseOutExpo, EaseInExpo };
        public enum AnimationType { Scale, Position, Rotation, LocalPosition, LocalRotation, Float, AnchoredPosition };
        public enum RotationType { Local, World };

        public static float EaseIt(Ease easing, float start, float end, float value)
        {
            switch (easing)
            {
                case Ease.Linear:
                    return EasingMath.Linear(start, end, value);
                case Ease.Clerp:
                    return EasingMath.Clerp(start, end, value);
                case Ease.EaseInQuad:
                    return EasingMath.EaseInQuad(start, end, value);
                case Ease.EaseOutQuad:
                    return EasingMath.EaseOutQuad(start, end, value);
                case Ease.EaseInOutQuad:
                    return EasingMath.EaseInOutQuad(start, end, value);
                case Ease.EaseInBack:
                    return EasingMath.EaseInBack(start, end, value);
                case Ease.EaseInElastic:
                    return EasingMath.EaseInElastic(start, end, value);
                case Ease.EaseOutElastic:
                    return EasingMath.EaseOutElastic(start, end, value);
                case Ease.EaseOutBack:
                    return EasingMath.EaseOutBack(start, end, value);
                case Ease.EaseInSine:
                    return EasingMath.EaseInSine(start, end, value);
                case Ease.Spring:
                    return EasingMath.Spring(start, end, value);
                case Ease.EaseInExpo:
                    return EasingMath.EaseInExpo(start, end, value);
                case Ease.EaseOutExpo:
                    return EasingMath.EaseOutExpo(start, end, value);
            }

            return 0;
        }

        private static Dictionary<string, MonoCoroutine> runningMonos = new Dictionary<string, MonoCoroutine>();
        private static List<FilterContainer> filters = new List<FilterContainer>();
        private static List<string> keysToRemove = new List<string>();

        /// <summary>
        /// Get the MonoCoroutine class reference for a monobehaviour, used to manage coroutines for each monobehaviour separately and avoid stopping unrelated coroutines when restarting an animation
        /// </summary>
        /// <param name="mono"></param>
        /// <returns></returns>
        private static MonoCoroutine GetMonoCoroutine(MonoBehaviour mono)
        {
            CleanUpMonos();

            if (mono == null)
                return null;

            string key = mono.GetInstanceID().ToString();

            if (runningMonos.TryGetValue(key, out MonoCoroutine result))
                return result;

            return null;
        }

        /// <summary>
        /// Clean up the dictionary from destroyed monobehaviours, called when trying to get a reference for a monobehaviour that is not in the dictionary
        /// This way we avoid memory leaks and keep the dictionary clean without having to call this function on destroy of each monobehaviour
        /// </summary>
        private static void CleanUpMonos()
        {
            keysToRemove.Clear();

            foreach (var kvp in runningMonos)
            {
                if (kvp.Value.mono == null)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
                runningMonos.Remove(key);
        }

        /// <summary>
        /// Restart the coroutine
        /// </summary>
        /// <param name="mono">The referenced mono</param>
        /// <param name="iENumType">The IENumType object holding the type and routine</param>
        /// <param name="newIENum">A new ienumerator function cause we cant reuse old ones</param>
        public static void RestartCoroutine(this MonoBehaviour mono, MonoCoroutine.IENumType iENumType, IEnumerator newIENum)
        {
            if (iENumType.routine != null)
                mono.StopCoroutine(iENumType.routine);

            iENumType.routine = newIENum;

            if (mono.gameObject.activeInHierarchy)
                mono.StartCoroutine(iENumType.routine);
        }

        /// <summary>
        /// Custom function to 
        /// </summary>
        /// <param name="mono">The referenced mono</param>
        /// <param name="animationType"></param>
        public static void StopCoroutine(this MonoBehaviour mono, AnimationType animationType, Transform target)
        {
            MonoCoroutine monoCoroutine = GetMonoCoroutine(mono);

            if (monoCoroutine != null)
            {
                MonoCoroutine.IENumType monoCoroutineIENum = monoCoroutine.coroutines.Find(c => c.animationType == animationType && c.target == target);

                if (monoCoroutineIENum != null)
                    mono.StopCoroutine(monoCoroutineIENum.routine);
            }
        }

        /// <summary>
        /// Animate a monobehaviour transform
        /// </summary>
        /// <param name="mono">The referenced mono</param>
        /// <param name="animationType">The animation type, see AnimationType</param>
        /// <param name="easeType">The easing type, see Ease</param>
        /// <param name="startVector">The start value to animate from</param>
        /// <param name="targetVector3">The target value to animate to</param>
        /// <param name="duration">The duration of the animation</param>
        /// <param name="delay">The delay of the animation start</param>
        /// <param name="callback">The callback being called after the animation has finished</param>
        /// <param name="target">The target transform you want to animate from the monobehaviour</param>
        public static void Animate(this MonoBehaviour mono, Transform target, AnimationType animationType, Ease easeType, Vector3 startVector, Vector3 targetVector3, float duration, float delay, Action callback = null)
        {
            MonoCoroutine monoCoroutine = GetMonoCoroutine(mono);

            if (monoCoroutine != null)
            {
                MonoCoroutine.IENumType monoCoroutineIENum = monoCoroutine.coroutines.Find(c => c.animationType == animationType && c.target == target);

                if (monoCoroutineIENum != null)
                {
                    mono.RestartCoroutine(monoCoroutineIENum, Animation(target, animationType, easeType, startVector, targetVector3, duration, delay, callback));
                }
                else
                {
                    monoCoroutineIENum = new MonoCoroutine.IENumType(animationType, Animation(target, animationType, easeType, startVector, targetVector3, duration, delay, callback), target);

                    if (mono.gameObject.activeInHierarchy)
                        mono.StartCoroutine(monoCoroutineIENum.routine);

                    monoCoroutine.TryAddRoutine(monoCoroutineIENum);
                }
            }
            else
            {
                monoCoroutine = new MonoCoroutine(mono);
                MonoCoroutine.IENumType monoCoroutineIENum = new MonoCoroutine.IENumType(animationType, Animation(target, animationType, easeType, startVector, targetVector3, duration, delay, callback), target);

                if (mono.gameObject.activeInHierarchy)
                    mono.StartCoroutine(monoCoroutineIENum.routine);

                monoCoroutine.TryAddRoutine(monoCoroutineIENum);

                runningMonos.Add(mono.GetInstanceID().ToString(), monoCoroutine);
            }
        }

        /// <summary>
        /// Quaternion rotation overload
        /// </summary>
        /// <param name="mono"></param>
        /// <param name="target"></param>
        /// <param name="animationType"></param>
        /// <param name="easeType"></param>
        /// <param name="startRotation"></param>
        /// <param name="targetRotation"></param>
        /// <param name="duration"></param>
        /// <param name="delay"></param>
        /// <param name="callback"></param>
        public static void Animate(this MonoBehaviour mono, Transform target, RotationType rotationType, Ease easeType, Quaternion startRotation, Quaternion targetRotation, float duration, float delay, Action callback = null)
        {
            MonoCoroutine monoCoroutine = GetMonoCoroutine(mono);

            if (monoCoroutine != null)
            {
                MonoCoroutine.IENumType monoCoroutineIENum = monoCoroutine.coroutines.Find(c => c.animationType == (rotationType == RotationType.Local ? AnimationType.LocalRotation : AnimationType.Rotation) && c.target == target);

                if (monoCoroutineIENum != null)
                {
                    mono.RestartCoroutine(monoCoroutineIENum, Animation(target, (rotationType == RotationType.Local ? AnimationType.LocalRotation : AnimationType.Rotation), easeType, startRotation, targetRotation, duration, delay, callback));
                }
                else
                {
                    monoCoroutineIENum = new MonoCoroutine.IENumType((rotationType == RotationType.Local ? AnimationType.LocalRotation : AnimationType.Rotation), Animation(target, (rotationType == RotationType.Local ? AnimationType.LocalRotation : AnimationType.Rotation), easeType, startRotation, targetRotation, duration, delay, callback), target);

                    if (mono.gameObject.activeInHierarchy)
                        mono.StartCoroutine(monoCoroutineIENum.routine);

                    monoCoroutine.TryAddRoutine(monoCoroutineIENum);
                }
            }
            else
            {
                monoCoroutine = new MonoCoroutine(mono);
                MonoCoroutine.IENumType monoCoroutineIENum = new MonoCoroutine.IENumType((rotationType == RotationType.Local ? AnimationType.LocalRotation : AnimationType.Rotation), Animation(target, (rotationType == RotationType.Local ? AnimationType.LocalRotation : AnimationType.Rotation), easeType, startRotation, targetRotation, duration, delay, callback), target);

                if (mono.gameObject.activeInHierarchy)
                    mono.StartCoroutine(monoCoroutineIENum.routine);

                monoCoroutine.TryAddRoutine(monoCoroutineIENum);

                runningMonos.Add(mono.GetInstanceID().ToString(), monoCoroutine);
            }
        }

        /// <summary>
        /// Animate a float value with a callback to update the value, useful for material properties or other non transform related values
        /// </summary>
        /// <param name="mono"></param>
        /// <param name="target"></param>
        /// <param name="easeType"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="duration"></param>
        /// <param name="delay"></param>
        /// <param name="update"></param>
        /// <param name="callback"></param>
        public static void AnimateFloat(this MonoBehaviour mono, Transform target, Ease easeType, float start, float end, float duration, float delay, Action<float> update, Action callback = null)
        {
            MonoCoroutine monoCoroutine = GetMonoCoroutine(mono);

            if (monoCoroutine != null)
            {
                MonoCoroutine.IENumType monoCoroutineIENum = monoCoroutine.coroutines.Find(c => c.animationType == AnimationType.Float && c.target == target);

                if (monoCoroutineIENum != null)
                {
                    mono.RestartCoroutine(monoCoroutineIENum, Animation(target, AnimationType.Float, easeType, start * Vector3.one, end * Vector3.one, duration, delay, callback, update));
                }
                else
                {
                    monoCoroutineIENum = new MonoCoroutine.IENumType(AnimationType.Float, Animation(target, AnimationType.Float, easeType, start * Vector3.one, end * Vector3.one, duration, delay, callback, update), target);

                    if (mono.gameObject.activeInHierarchy)
                        mono.StartCoroutine(monoCoroutineIENum.routine);

                    monoCoroutine.TryAddRoutine(monoCoroutineIENum);
                }
            }
            else
            {
                monoCoroutine = new MonoCoroutine(mono);

                MonoCoroutine.IENumType monoCoroutineIENum = new MonoCoroutine.IENumType(AnimationType.Float, Animation(target, AnimationType.Float, easeType, start * Vector3.one, end * Vector3.one, duration, delay, callback, update), target);

                if (mono.gameObject.activeInHierarchy)
                    mono.StartCoroutine(monoCoroutineIENum.routine);

                monoCoroutine.TryAddRoutine(monoCoroutineIENum);

                runningMonos.Add(mono.GetInstanceID().ToString(), monoCoroutine);
            }
        }

        /// <summary>
        /// Overload without time params for Vector3 animations
        /// </summary>
        /// <param name="mono"></param>
        /// <param name="target"></param>
        /// <param name="animationType"></param>
        /// <param name="easeType"></param>
        /// <param name="startVector"></param>
        /// <param name="targetVector3"></param>
        /// <param name="duration"></param>
        /// <param name="callback"></param>
        public static void Animate(this MonoBehaviour mono, Transform target, AnimationType animationType, Ease easeType, Vector3 startVector, Vector3 targetVector3, float duration = 0.5f, Action callback = null)
            => Animate(mono, target, animationType, easeType, startVector, targetVector3, duration, 0, callback);

        /// <summary>
        /// Overload without time params for Quaternion animations
        /// </summary>
        /// <param name="mono"></param>
        /// <param name="target"></param>
        /// <param name="animationType"></param>
        /// <param name="easeType"></param>
        /// <param name="startVector"></param>
        /// <param name="targetVector3"></param>
        /// <param name="callback"></param>
        public static void Animate(this MonoBehaviour mono, Transform target, AnimationType animationType, Ease easeType, Vector3 startVector, Vector3 targetVector3, Action callback)
            => Animate(mono, target, animationType, easeType, startVector, targetVector3, 1, 0, callback);

        /// <summary>
        /// Overload without time params for Quaternion animations
        /// </summary>
        /// <param name="mono"></param>
        /// <param name="target"></param>
        /// <param name="rotationType"></param>
        /// <param name="easeType"></param>
        /// <param name="startRotation"></param>
        /// <param name="targetRotation"></param>
        /// <param name="callback"></param>
        public static void Animate(this MonoBehaviour mono, Transform target, RotationType rotationType, Ease easeType, Quaternion startRotation, Quaternion targetRotation, Action callback = null)
            => Animate(mono, target, rotationType, easeType, startRotation, targetRotation, 0.5f, 0, callback);

        /// <summary>
        /// Quaternion overload
        /// </summary>
        /// <param name="target"></param>
        /// <param name="animationType"></param>
        /// <param name="easeType"></param>
        /// <param name="startRotation"></param>
        /// <param name="targetRotation"></param>
        /// <param name="duration"></param>
        /// <param name="delay"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        private static IEnumerator Animation(Transform target, AnimationType animationType, Ease easeType, Quaternion startRotation, Quaternion targetRotation, float duration, float delay, Action callback)
        {
            yield return new WaitForSeconds(delay);

            float timer = 0;

            while (timer < duration)
            {
                switch (animationType)
                {
                    case AnimationType.Rotation:
                        target.rotation = Quaternion.LerpUnclamped(startRotation, targetRotation, EaseIt(easeType, 0, 1, timer / duration));
                        break;
                    case AnimationType.LocalRotation:
                        target.localRotation = Quaternion.LerpUnclamped(startRotation, targetRotation, EaseIt(easeType, 0, 1, timer / duration));
                        break;
                }

                timer += Time.deltaTime;

                yield return null;
            }

            switch (animationType)
            {
                case AnimationType.Rotation:
                    target.rotation = targetRotation;
                    break;
                case AnimationType.LocalRotation:
                    target.localRotation = targetRotation;
                    break;
            }

            callback?.Invoke();
        }

        /// <summary>
        /// The main animation coroutine for Vector3 and float animations, all the other overloads eventually call this one
        /// </summary>
        /// <param name="target"></param>
        /// <param name="animationType"></param>
        /// <param name="easeType"></param>
        /// <param name="startVector"></param>
        /// <param name="targetVector3"></param>
        /// <param name="duration"></param>
        /// <param name="delay"></param>
        /// <param name="callback"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        private static IEnumerator Animation(Transform target, AnimationType animationType, Ease easeType, Vector3 startVector, Vector3 targetVector3, float duration, float delay, Action callback, Action<float> update = null)
        {
            yield return new WaitForSeconds(delay);

            float timer = 0;

            while (timer < duration)
            {
                switch (animationType)
                {
                    case AnimationType.Position:
                        target.position = Vector3.LerpUnclamped(startVector, targetVector3, EaseIt(easeType, 0, 1, timer / duration));
                        break;
                    case AnimationType.LocalPosition:
                        target.localPosition = Vector3.LerpUnclamped(startVector, targetVector3, EaseIt(easeType, 0, 1, timer / duration));
                        break;
                    case AnimationType.Rotation:
                        target.rotation = Quaternion.Euler(Vector3.LerpUnclamped(startVector, targetVector3, EaseIt(easeType, 0, 1, timer / duration)));
                        break;
                    case AnimationType.LocalRotation:
                        target.localRotation = Quaternion.Euler(Vector3.LerpUnclamped(startVector, targetVector3, EaseIt(easeType, 0, 1, timer / duration)));
                        break;
                    case AnimationType.Scale:
                        target.localScale = Vector3.LerpUnclamped(startVector, targetVector3, EaseIt(easeType, 0, 1, timer / duration));
                        break;
                    case AnimationType.Float:
                        update?.Invoke(Vector3.LerpUnclamped(startVector, targetVector3, EaseIt(easeType, 0, 1, timer / duration)).x);
                        break;
                    case AnimationType.AnchoredPosition:
                        ((RectTransform)target).anchoredPosition = Vector3.LerpUnclamped(startVector, targetVector3, EaseIt(easeType, 0, 1, timer / duration));
                        break;
                }

                timer += Time.deltaTime;

                yield return null;
            }

            switch (animationType)
            {
                case AnimationType.Position:
                    target.position = targetVector3;
                    break;
                case AnimationType.LocalPosition:
                    target.localPosition = targetVector3;
                    break;
                case AnimationType.Rotation:
                    target.rotation = Quaternion.Euler(targetVector3);
                    break;
                case AnimationType.LocalRotation:
                    target.localRotation = Quaternion.Euler(targetVector3);
                    break;
                case AnimationType.Scale:
                    target.localScale = targetVector3;
                    break;
                case AnimationType.Float:
                    update?.Invoke(targetVector3.x);
                    break;
            }

            callback?.Invoke();
        }
    }
}