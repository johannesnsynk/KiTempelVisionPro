using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public static class Easing
{
    public enum Ease { Linear, Clerp, EaseInQuad, EaseOutQuad, EaseInOutQuad, EaseInBack, EaseOutBack, EaseInElastic, EaseOutElastic, EaseInSine, Spring, EaseOutExpo, EaseInExpo };
    public enum AnimationType { Scale, Position, Rotation, LocalPosition, LocalRotation, Float, AnchoredPosition };
    public enum RotationType { Local, World };

    public static float EaseIt(Ease easing, float start, float end, float value)
    {
        switch(easing)
        {
            case Ease.Linear:
                return linear(start, end, value);
            case Ease.Clerp:
                return clerp(start, end, value);
            case Ease.EaseInQuad:
                return easeInQuad(start, end, value);
            case Ease.EaseOutQuad:
                return easeOutQuad(start, end, value);
            case Ease.EaseInOutQuad:
                return easeInOutQuad(start, end, value);
            case Ease.EaseInBack:
                return easeInBack(start, end, value);
            case Ease.EaseInElastic:
                return easeInElastic(start, end, value);
            case Ease.EaseOutElastic:
                return easeOutElastic(start, end, value);
            case Ease.EaseOutBack:
                return easeOutBack(start, end, value);
            case Ease.EaseInSine:
                return easeInSine(start, end, value);
            case Ease.Spring:
                return spring(start, end, value);
            case Ease.EaseInExpo:
                return easeInExpo(start, end, value);
            case Ease.EaseOutExpo:
                return easeOutExpo(start, end, value);
        }

        return 0;
    }

    private static List<MonoCoroutine> runningMonos = new List<MonoCoroutine>();

    /// <summary>
    /// Restart the coroutine
    /// </summary>
    /// <param name="mono">The referenced mono</param>
    /// <param name="iENumType">The IENumType object holding the type and routine</param>
    /// <param name="newIENum">A new ienumerator function cause we cant reuse old ones</param>
    public static void RestartCoroutine(this MonoBehaviour mono, MonoCoroutine.IENumType iENumType, IEnumerator newIENum)
    {
        if(iENumType.routine != null)
            mono.StopCoroutine(iENumType.routine);

        iENumType.routine = newIENum;

        if(mono.gameObject.activeInHierarchy)
            mono.StartCoroutine(iENumType.routine);
    }

    /// <summary>
    /// Custom class to hold a monobehaviour reference and coroutines
    /// </summary>
    public class MonoCoroutine
    {
        public MonoCoroutine(MonoBehaviour _m)
        {
            mono = _m;
        }

        public MonoBehaviour mono;
        public List<IENumType> coroutines = new List<IENumType>();

        public void TryAddRoutine(IENumType iENumType)
        {
            IENumType foundC = coroutines.Find(c => c.animationType == iENumType.animationType && c.target == iENumType.target);

            if (foundC != null)
                mono.StopCoroutine(foundC.routine);
            else
                coroutines.Add(iENumType);
        }

        public class IENumType
        {
            public IENumType(AnimationType aT, IEnumerator r, Transform t)
            {
                target = t;
                animationType = aT;
                routine = r;
            }

            public Transform target;
            public AnimationType animationType;
            public IEnumerator routine;
        }
    }

    /// <summary>
    /// Custom function to 
    /// </summary>
    /// <param name="mono">The referenced mono</param>
    /// <param name="animationType"></param>
    public static void StopCoroutine(this MonoBehaviour mono, AnimationType animationType, Transform target)
    {
        MonoCoroutine monoCoroutine = runningMonos.Find(rM => rM.mono == mono);

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
        MonoCoroutine monoCoroutine = runningMonos.Find(rM => rM.mono == mono);

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

            if(mono.gameObject.activeInHierarchy)
                mono.StartCoroutine(monoCoroutineIENum.routine);

            monoCoroutine.TryAddRoutine(monoCoroutineIENum);

            runningMonos.Add(monoCoroutine);
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
        MonoCoroutine monoCoroutine = runningMonos.Find(rM => rM.mono == mono);

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

            runningMonos.Add(monoCoroutine);
        }
    }

    public static void AnimateFloat(this MonoBehaviour mono, Transform target, Ease easeType, float start, float end, float duration, float delay, Action<float> update, Action callback = null)
    {
        MonoCoroutine monoCoroutine = runningMonos.Find(rM => rM.mono == mono);

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

            runningMonos.Add(monoCoroutine);
        }
    }

    public static void Animate(this MonoBehaviour mono, Transform target, AnimationType animationType, Ease easeType, Vector3 startVector, Vector3 targetVector3, float duration = 0.5f, Action callback = null)
        => Animate(mono, target, animationType, easeType, startVector, targetVector3, duration, 0, callback);

    public static void Animate(this MonoBehaviour mono, Transform target, AnimationType animationType, Ease easeType, Vector3 startVector, Vector3 targetVector3, Action callback)
        => Animate(mono, target, animationType, easeType, startVector, targetVector3, 1, 0, callback);

    /// Overload for rotation without time params
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

    private static IEnumerator Animation(Transform target, AnimationType animationType, Ease easeType, Vector3 startVector, Vector3 targetVector3, float duration, float delay, Action callback, Action<float> update = null)
    {
        yield return new WaitForSeconds(delay);

        float timer = 0;

        while(timer < duration)
        {
            switch(animationType)
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

    public static float linear(float start, float end, float value)
    {
        return Mathf.Lerp(start, end, value);
    }

    public static float clerp(float start, float end, float value)
    {
        float min = 0.0f;
        float max = 360.0f;
        float half = Mathf.Abs((max - min) / 2.0f);
        float retval = 0.0f;
        float diff = 0.0f;
        if ((end - start) < -half)
        {
            diff = ((max - start) + end) * value;
            retval = start + diff;
        }
        else if ((end - start) > half)
        {
            diff = -((max - end) + start) * value;
            retval = start + diff;
        }
        else retval = start + (end - start) * value;
        return retval;
    }

    public static float spring(float start, float end, float value)
    {
        value = Mathf.Clamp01(value);
        value = (Mathf.Sin(value * Mathf.PI * (0.2f + 2.5f * value * value * value)) * Mathf.Pow(1f - value, 2.2f) + value) * (1f + (1.2f * (1f - value)));
        return start + (end - start) * value;
    }

    public static float easeInQuad(float start, float end, float value, bool getPropForValue = false)
    {
        end -= start;
        if (!getPropForValue)
            return end * value * value + start;
        return Mathf.Sqrt((value - start) / end);
    }

    public static float easeOutQuad(float start, float end, float value)
    {
        end -= start;
        return -end * value * (value - 2) + start;
    }

    public static float easeInOutQuad(float start, float end, float value)
    {
        value /= .5f;
        end -= start;
        if (value < 1) return end / 2 * value * value + start;
        value--;
        return -end / 2 * (value * (value - 2) - 1) + start;
    }

    public static float easeInCubic(float start, float end, float value)
    {
        end -= start;
        return end * value * value * value + start;
    }

    public static float easeOutCubic(float start, float end, float value)
    {
        value--;
        end -= start;
        return end * (value * value * value + 1) + start;
    }

    public static float easeInOutCubic(float start, float end, float value)
    {
        value /= .5f;
        end -= start;
        if (value < 1) return end / 2 * value * value * value + start;
        value -= 2;
        return end / 2 * (value * value * value + 2) + start;
    }

    public static float easeInQuart(float start, float end, float value)
    {
        end -= start;
        return end * value * value * value * value + start;
    }

    public static float easeOutQuart(float start, float end, float value)
    {
        value--;
        end -= start;
        return -end * (value * value * value * value - 1) + start;
    }

    public static float easeInOutQuart(float start, float end, float value)
    {
        value /= .5f;
        end -= start;
        if (value < 1) return end / 2 * value * value * value * value + start;
        value -= 2;
        return -end / 2 * (value * value * value * value - 2) + start;
    }

    public static float easeInQuint(float start, float end, float value)
    {
        end -= start;
        return end * value * value * value * value * value + start;
    }

    public static float easeOutQuint(float start, float end, float value)
    {
        value--;
        end -= start;
        return end * (value * value * value * value * value + 1) + start;
    }

    public static float easeInOutQuint(float start, float end, float value)
    {
        value /= .5f;
        end -= start;
        if (value < 1) return end / 2 * value * value * value * value * value + start;
        value -= 2;
        return end / 2 * (value * value * value * value * value + 2) + start;
    }

    public static float easeInSine(float start, float end, float value)
    {
        end -= start;
        return -end * Mathf.Cos(value / 1 * (Mathf.PI / 2)) + end + start;
    }

    public static float easeOutSine(float start, float end, float value)
    {
        end -= start;
        return end * Mathf.Sin(value / 1 * (Mathf.PI / 2)) + start;
    }

    public static float easeInOutSine(float start, float end, float value)
    {
        end -= start;
        return -end / 2 * (Mathf.Cos(Mathf.PI * value / 1) - 1) + start;
    }

    public static float easeInExpo(float start, float end, float value)
    {
        end -= start;
        return end * Mathf.Pow(2, 10 * (value / 1 - 1)) + start;
    }

    public static float easeOutExpo(float start, float end, float value)
    {
        end -= start;
        return end * (-Mathf.Pow(2, -10 * value / 1) + 1) + start;
    }

    public static float easeInOutExpo(float start, float end, float value)
    {
        value /= .5f;
        end -= start;
        if (value < 1) return end / 2 * Mathf.Pow(2, 10 * (value - 1)) + start;
        value--;
        return end / 2 * (-Mathf.Pow(2, -10 * value) + 2) + start;
    }

    public static float easeInCirc(float start, float end, float value)
    {
        end -= start;
        return -end * (Mathf.Sqrt(1 - value * value) - 1) + start;
    }

    public static float easeOutCirc(float start, float end, float value)
    {
        value--;
        end -= start;
        return end * Mathf.Sqrt(1 - value * value) + start;
    }

    public static float easeInOutCirc(float start, float end, float value)
    {
        value /= .5f;
        end -= start;
        if (value < 1) return -end / 2 * (Mathf.Sqrt(1 - value * value) - 1) + start;
        value -= 2;
        return end / 2 * (Mathf.Sqrt(1 - value * value) + 1) + start;
    }

    /* GFX47 MOD START */
    public static float easeInBounce(float start, float end, float value)
    {
        end -= start;
        float d = 1f;
        return end - easeOutBounce(0, end, d - value) + start;
    }
    /* GFX47 MOD END */

    /* GFX47 MOD START */
    //private float bounce(float start, float end, float value){
    public static float easeOutBounce(float start, float end, float value)
    {
        value /= 1f;
        end -= start;
        if (value < (1 / 2.75f))
        {
            return end * (7.5625f * value * value) + start;
        }
        else if (value < (2 / 2.75f))
        {
            value -= (1.5f / 2.75f);
            return end * (7.5625f * (value) * value + .75f) + start;
        }
        else if (value < (2.5 / 2.75))
        {
            value -= (2.25f / 2.75f);
            return end * (7.5625f * (value) * value + .9375f) + start;
        }
        else
        {
            value -= (2.625f / 2.75f);
            return end * (7.5625f * (value) * value + .984375f) + start;
        }
    }
    /* GFX47 MOD END */

    /* GFX47 MOD START */
    public static float easeInOutBounce(float start, float end, float value)
    {
        end -= start;
        float d = 1f;
        if (value < d / 2) return easeInBounce(0, end, value * 2) * 0.5f + start;
        else return easeOutBounce(0, end, value * 2 - d) * 0.5f + end * 0.5f + start;
    }
    /* GFX47 MOD END */

    public static float easeInBack(float start, float end, float value)
    {
        end -= start;
        value /= 1;
        float s = 1.70158f;
        return end * (value) * value * ((s + 1) * value - s) + start;
    }

    public static float easeOutBack(float start, float end, float value)
    {
        float s = 1.70158f;
        end -= start;
        value = (value / 1) - 1;
        return end * ((value) * value * ((s + 1) * value + s) + 1) + start;
    }

    public static float easeInOutBack(float start, float end, float value)
    {
        float s = 1.70158f;
        end -= start;
        value /= .5f;
        if ((value) < 1)
        {
            s *= (1.525f);
            return end / 2 * (value * value * (((s) + 1) * value - s)) + start;
        }
        value -= 2;
        s *= (1.525f);
        return end / 2 * ((value) * value * (((s) + 1) * value + s) + 2) + start;
    }

    public static float punch(float amplitude, float value)
    {
        float s = 9;
        if (value == 0)
        {
            return 0;
        }
        if (value == 1)
        {
            return 0;
        }
        float period = 1 * 0.3f;
        s = period / (2 * Mathf.PI) * Mathf.Asin(0);
        return (amplitude * Mathf.Pow(2, -10 * value) * Mathf.Sin((value * 1 - s) * (2 * Mathf.PI) / period));
    }

    /* GFX47 MOD START */
    public static float easeInElastic(float start, float end, float value)
    {
        end -= start;

        float d = 1f;
        float p = d * .3f;
        float s = 0;
        float a = 0;

        if (value == 0) return start;

        if ((value /= d) == 1) return start + end;

        if (a == 0f || a < Mathf.Abs(end))
        {
            a = end;
            s = p / 4;
        }
        else
        {
            s = p / (2 * Mathf.PI) * Mathf.Asin(end / a);
        }

        return -(a * Mathf.Pow(2, 10 * (value -= 1)) * Mathf.Sin((value * d - s) * (2 * Mathf.PI) / p)) + start;
    }
    /* GFX47 MOD END */

    /* GFX47 MOD START */
    //private float elastic(float start, float end, float value){
    public static float easeOutElastic(float start, float end, float value)
    {
        /* GFX47 MOD END */
        //Thank you to rafael.marteleto for fixing this as a port over from Pedro's UnityTween
        end -= start;

        float d = 1f;
        float p = d * .3f;
        float s = 0;
        float a = 0;

        if (value == 0) return start;

        if ((value /= d) == 1) return start + end;

        if (a == 0f || a < Mathf.Abs(end))
        {
            a = end;
            s = p / 4;
        }
        else
        {
            s = p / (2 * Mathf.PI) * Mathf.Asin(end / a);
        }

        return (a * Mathf.Pow(2, -10 * value) * Mathf.Sin((value * d - s) * (2 * Mathf.PI) / p) + end + start);
    }

    /* GFX47 MOD START */
    public static float easeInOutElastic(float start, float end, float value)
    {
        end -= start;

        float d = 1f;
        float p = d * .3f;
        float s = 0;
        float a = 0;

        if (value == 0) return start;

        if ((value /= d / 2) == 2) return start + end;

        if (a == 0f || a < Mathf.Abs(end))
        {
            a = end;
            s = p / 4;
        }
        else
        {
            s = p / (2 * Mathf.PI) * Mathf.Asin(end / a);
        }

        if (value < 1) return -0.5f * (a * Mathf.Pow(2, 10 * (value -= 1)) * Mathf.Sin((value * d - s) * (2 * Mathf.PI) / p)) + start;
        return a * Mathf.Pow(2, -10 * (value -= 1)) * Mathf.Sin((value * d - s) * (2 * Mathf.PI) / p) * 0.5f + end + start;
    }
    /* GFX47 MOD END */
}