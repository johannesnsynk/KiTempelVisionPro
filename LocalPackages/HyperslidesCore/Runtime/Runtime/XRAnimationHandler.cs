using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NSYNK.HyperSlides.Runtime
{
    [RequireComponent(typeof(Animation))]
    public class XRAnimationHandler : XRSlideDissolveComponent
    {
        public bool useDissolveNormalized = true;
        public Easing.Ease easingType = Easing.Ease.EaseInOutQuad;
        public List<AnimationStatePair> animationStatePairs;

        private Animation animComp;

        protected override void Awake()
        {
            base.Awake();

            animComp = GetComponent<Animation>();

            animationStatePairs.ForEach(e => {
                animComp.AddClip(e.clip, e.clip.name);
            });
        }

        /// <summary>
        /// Animate the clip triggered by the current visbility state of dissolving
        /// </summary>
        /// <param name="dissolveNormalized">The dissolve value pingponging between 0 and 1</param>
        /// <param name="dissolveInOut">The dissolve value combined with a range from 0 over 1 to 2</param>
        public override void OnDissolveChanged(float dissolveNormalized, float dissolveInOut)
        {
            animationStatePairs.ForEach(e => {
                if (e.state == visibilityState && e.clip)
                {
                    animComp.clip = e.clip;
                    animComp[e.clip.name].normalizedTime = Easing.EaseIt(easingType, 0, 1, useDissolveNormalized ? dissolveNormalized : dissolveInOut); ;
                    animComp[e.clip.name].speed = 0.0f;
                    animComp.Play(e.clip.name);
                }
            });
        }


        public override void OnVisibilityStateChanged(XRSlideElement.VisibilityState state) { }

        /// <summary>
        /// An animation state pair combining a state and a clip
        /// </summary>
        [Serializable]
        public class AnimationStatePair
        {
            public XRSlideElement.VisibilityState state;
            public AnimationClip clip;
        }
    }
}