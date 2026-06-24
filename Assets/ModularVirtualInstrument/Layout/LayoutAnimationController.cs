#if MVI_FLEXALON
using UnityEngine;
using Flexalon;
using System.Collections.Generic;

namespace ModularVirtualInstrument
{
    /// <summary>
    /// Adds smooth animation support to stem layout changes using Flexalon animators.
    /// Can be attached to the ModularSynthController to enable animated transitions.
    /// </summary>
    [RequireComponent(typeof(ModularSynthController))]
    public class LayoutAnimationController : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Type of Flexalon animator to use")]
        public AnimatorType animatorType = AnimatorType.Lerp;
        
        [Tooltip("Enable animations")]
        public bool enableAnimations = true;
        
        [Header("Lerp Animator Settings")]
        [Tooltip("Interpolation speed for lerp animator")]
        [Range(1f, 20f)]
        public float lerpSpeed = 5f;
        
        [Tooltip("Animate in world space (disable if parent is moving)")]
        public bool animateInWorldSpace = true;
        
        [Header("Curve Animator Settings")]
        [Tooltip("Animation curve for curve animator")]
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [Tooltip("Duration of curve animation")]
        [Range(0.1f, 5f)]
        public float curveDuration = 1f;
        
        [Header("Animation Channels")]
        [Tooltip("Animate position changes")]
        public bool animatePosition = true;
        
        [Tooltip("Animate rotation changes")]
        public bool animateRotation = true;
        
        [Tooltip("Animate scale changes")]
        public bool animateScale = false;
        
        private ModularSynthController controller;
        private Dictionary<Transform, BaseAnimator> stemAnimators = new Dictionary<Transform, BaseAnimator>();
        
        public enum AnimatorType
        {
            None,
            Lerp,
            Curve
        }
        
        private abstract class BaseAnimator
        {
            protected Transform transform;
            protected LayoutAnimationController parent;
            
            public BaseAnimator(Transform t, LayoutAnimationController p)
            {
                transform = t;
                parent = p;
            }
            
            public abstract void Setup();
            public abstract void Cleanup();
        }
        
        private class LerpAnimatorWrapper : BaseAnimator
        {
            private FlexalonLerpAnimator animator;
            
            public LerpAnimatorWrapper(Transform t, LayoutAnimationController p) : base(t, p) { }
            
            public override void Setup()
            {
                animator = transform.GetComponent<FlexalonLerpAnimator>();
                if (animator == null)
                {
                    animator = transform.gameObject.AddComponent<FlexalonLerpAnimator>();
                }
                
                animator.InterpolationSpeed = parent.lerpSpeed;
                animator.AnimateInWorldSpace = parent.animateInWorldSpace;
                animator.AnimatePosition = parent.animatePosition;
                animator.AnimateRotation = parent.animateRotation;
                animator.AnimateScale = parent.animateScale;
            }
            
            public override void Cleanup()
            {
                if (animator != null)
                {
                    if (Application.isPlaying)
                        Destroy(animator);
                    else
                        DestroyImmediate(animator);
                }
            }
        }
        
        private class CurveAnimatorWrapper : BaseAnimator
        {
            private FlexalonCurveAnimator animator;
            
            public CurveAnimatorWrapper(Transform t, LayoutAnimationController p) : base(t, p) { }
            
            public override void Setup()
            {
                animator = transform.GetComponent<FlexalonCurveAnimator>();
                if (animator == null)
                {
                    animator = transform.gameObject.AddComponent<FlexalonCurveAnimator>();
                }
                
                animator.Curve = parent.animationCurve;
                animator.AnimateInWorldSpace = parent.animateInWorldSpace;
                animator.AnimatePosition = parent.animatePosition;
                animator.AnimateRotation = parent.animateRotation;
                animator.AnimateScale = parent.animateScale;
            }
            
            public override void Cleanup()
            {
                if (animator != null)
                {
                    if (Application.isPlaying)
                        Destroy(animator);
                    else
                        DestroyImmediate(animator);
                }
            }
        }
        
        private void OnEnable()
        {
            controller = GetComponent<ModularSynthController>();
            
            if (controller != null)
            {
                controller.OnStemsLoaded += OnStemsLoaded;
                controller.OnLayoutRegenerated += OnLayoutRegenerated;
            }
            
            SetupAnimators();
        }
        
        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnStemsLoaded -= OnStemsLoaded;
                controller.OnLayoutRegenerated -= OnLayoutRegenerated;
            }
            
            CleanupAnimators();
        }
        
        private void OnValidate()
        {
            if (enableAnimations && Application.isPlaying)
            {
                UpdateAnimatorSettings();
            }
        }
        
        private void OnStemsLoaded(int stemCount)
        {
            if (enableAnimations)
            {
                SetupAnimators();
            }
        }
        
        private void OnLayoutRegenerated()
        {
            if (enableAnimations)
            {
                SetupAnimators();
            }
        }
        
        /// <summary>
        /// Setup animators for all stems
        /// </summary>
        public void SetupAnimators()
        {
            if (!enableAnimations || controller == null || animatorType == AnimatorType.None)
            {
                CleanupAnimators();
                return;
            }
            
            stemAnimators.Clear();
            
            foreach (Transform child in controller.transform)
            {
                BaseAnimator animator = CreateAnimator(child);
                if (animator != null)
                {
                    animator.Setup();
                    stemAnimators[child] = animator;
                }
            }
        }
        
        /// <summary>
        /// Remove all animators from stems
        /// </summary>
        public void CleanupAnimators()
        {
            foreach (var kvp in stemAnimators)
            {
                kvp.Value?.Cleanup();
            }
            
            stemAnimators.Clear();
        }
        
        /// <summary>
        /// Update settings on existing animators
        /// </summary>
        public void UpdateAnimatorSettings()
        {
            for (int i = 0; i < controller.transform.childCount; i++)
            {
                Transform childTransform = controller.transform.GetChild(i);
                
                // Update Lerp animator
                var lerpAnimator = childTransform.GetComponent<FlexalonLerpAnimator>();
                if (lerpAnimator != null)
                {
                    lerpAnimator.InterpolationSpeed = lerpSpeed;
                    lerpAnimator.AnimateInWorldSpace = animateInWorldSpace;
                    lerpAnimator.AnimatePosition = animatePosition;
                    lerpAnimator.AnimateRotation = animateRotation;
                    lerpAnimator.AnimateScale = animateScale;
                }
                
                // Update Curve animator
                var curveAnimator = childTransform.GetComponent<FlexalonCurveAnimator>();
                if (curveAnimator != null)
                {
                    curveAnimator.Curve = animationCurve;
                    curveAnimator.AnimateInWorldSpace = animateInWorldSpace;
                    curveAnimator.AnimatePosition = animatePosition;
                    curveAnimator.AnimateRotation = animateRotation;
                    curveAnimator.AnimateScale = animateScale;
                }
            }
        }
        
        private BaseAnimator CreateAnimator(Transform child)
        {
            switch (animatorType)
            {
                case AnimatorType.Lerp:
                    return new LerpAnimatorWrapper(child, this);
                    
                case AnimatorType.Curve:
                    return new CurveAnimatorWrapper(child, this);
                    
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Temporarily disable animations
        /// </summary>
        public void DisableAnimations()
        {
            enableAnimations = false;
            CleanupAnimators();
        }
        
        /// <summary>
        /// Re-enable animations
        /// </summary>
        public void EnableAnimations()
        {
            enableAnimations = true;
            SetupAnimators();
        }
        
        /// <summary>
        /// Apply animator to a specific stem
        /// </summary>
        public void ApplyAnimatorToStem(Transform stem)
        {
            if (!enableAnimations || animatorType == AnimatorType.None)
                return;
            
            BaseAnimator animator = CreateAnimator(stem);
            if (animator != null)
            {
                animator.Setup();
                stemAnimators[stem] = animator;
            }
        }
    }
}
#endif
