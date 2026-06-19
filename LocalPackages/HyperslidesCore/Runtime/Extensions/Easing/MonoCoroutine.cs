using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace NSYNK
{
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
            public IENumType(Easing.AnimationType aT, IEnumerator r, Transform t)
            {
                target = t;
                animationType = aT;
                routine = r;
            }

            public Transform target;
            public Easing.AnimationType animationType;
            public IEnumerator routine;
        }
    }
}