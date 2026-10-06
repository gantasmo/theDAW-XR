using System.Collections.Generic;
#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
using TMPro;
#endif
using UnityEngine.UI;
using UnityEngine.XR.Hands.Analytics;
using UnityEngine.XR.Hands.Gestures;
using UnityEngine.XR.Hands.Samples.GestureSample;

namespace UnityEngine.XR.Hands.Samples.Gestures.DebugTools
{
    /// <summary>
    /// Controls the debug UI for <see cref="XRHandShape"/> that shows the target and tolerances on the UI controlled
    /// by a <see cref="XRAllFingerShapesDebugUI"/>.
    /// </summary>
    public class XRHandShapeDebugUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The debug UI that will be used to display the finger states.")]
        XRAllFingerShapesDebugUI m_XRAllFingerShapesDebugUI;

        [SerializeField]
        [Tooltip("The target hand shape to be displayed in the debugger. If an XRHandPose is set, its underlying XRHandShape properties will be displayed.")]
        ScriptableObject m_HandShapeOrPose;

#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
        [SerializeField]
        TextMeshProUGUI SelectedHandShapeName;
#endif
        [SerializeField]
        XRSelectedHandShapeDebugUI m_XRSelectedHandShapeDebugUI;

        [SerializeField]
        [Tooltip("The component used to calculate how closely the current hand shape matches the target hand shape.")]
        HandShapeCompletenessCalculator m_HandShapeCompletenessCalculator;

        [SerializeField]
        [Tooltip("The progress bar UI that displays the completeness of the hand shape.")]
        Slider m_HandShapeCompletenessProgressBar;

        XRHandShape m_HandShape;

        bool m_HandShapeDetected;

        bool m_HandShapeCompletenessEnabled;

        XRHandSubsystem m_Subsystem;

        readonly List<XRFingerShapeDebugBar> m_ReusableBarsToHide = new List<XRFingerShapeDebugBar>();

        readonly List<XRFingerShapeDebugBar> m_Bars = new List<XRFingerShapeDebugBar>();

        static readonly List<XRHandSubsystem> s_SubsystemsReuse = new List<XRHandSubsystem>();

        /// <summary>
        /// The hand shape that will be displayed in the debug UI.
        /// </summary>
        public ScriptableObject handShapeOrPose
        {
            get => m_HandShape;
            set
            {
                var handPose = value as XRHandPose;

                m_HandShape = handPose != null ? handPose.handShape : value as XRHandShape;

                m_HandShapeDetected = m_HandShape != null;
                foreach (var bar in m_Bars)
                {
                    bar.fingerShapeDetected = m_HandShapeDetected;
                }

                if (m_HandShapeDetected)
                {
                    // Hide previously enabled bars
                    foreach (var bar in m_Bars)
                    {
                        bar.HideTargetAndTolerance();
                    }
                }
            }
        }

        void Awake()
        {
#if UNITY_EDITOR && (ENABLE_CLOUD_SERVICES_ANALYTICS || UNITY_2023_2_OR_NEWER)
            XRHandAnalyticsData.xrHandCustomGestureDebugActive = true;
#endif
            m_HandShape = m_HandShapeOrPose as XRHandShape;

            if (m_HandShape == null)
            {
                XRHandPose poseCastTest = m_HandShapeOrPose as XRHandPose;
                if (poseCastTest != null)
                    m_HandShape = poseCastTest.handShape;
            }

            m_HandShapeDetected = m_HandShape != null;

            if (m_HandShapeDetected)
            {
                handShapeOrPose = m_HandShape;

#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
                SelectedHandShapeName.text = m_HandShape.name;
                m_XRSelectedHandShapeDebugUI.UpdateSelectedHandShapeTextUI(m_HandShape);
#endif
            }

            if (m_Bars.Count == 0)
            {
                foreach (var graph in m_XRAllFingerShapesDebugUI.xrFingerShapeDebugGraphs)
                {
                    foreach (var bar in graph.bars)
                    {
                        m_Bars.Add(bar);
                    }
                }
            }

            m_HandShapeCompletenessEnabled =
                m_HandShapeCompletenessCalculator != null && m_HandShapeCompletenessProgressBar != null;
        }

        void Update()
        {
            foreach (var bar in m_Bars)
            {
                bar.HideTargetAndTolerance();
            }

            // Track all the bars that have no target and tolerance so they can be hidden
            m_ReusableBarsToHide.Clear();
            foreach (var graph in m_XRAllFingerShapesDebugUI.xrFingerShapeDebugGraphs)
            {
                m_ReusableBarsToHide.AddRange(graph.bars);
            }

            if (m_HandShapeDetected)
            {
                foreach (var condition in m_HandShape.fingerShapeConditions)
                {
                    foreach (var shapeCondition in condition.targets)
                    {
                        if (shapeCondition.shapeType == XRFingerShapeType.Unspecified)
                            continue;
                        var xrFingerShapeDebugGraph = m_XRAllFingerShapesDebugUI.xrFingerShapeDebugGraphs[(int)condition.fingerID];
                        var bar = xrFingerShapeDebugGraph.bars[(int)shapeCondition.shapeType];
                        bar.SetTargetAndTolerances(shapeCondition.desired, shapeCondition.upperTolerance, shapeCondition.lowerTolerance);
                        m_ReusableBarsToHide.Remove(bar);
                    }
                }
            }

            if (m_HandShapeCompletenessEnabled && m_HandShapeDetected)
            {
                if (!TryGetHandSubsystem(out var subsystem))
                    return;

                var hand = m_XRAllFingerShapesDebugUI.handedness ==
                    Handedness.Left ? subsystem.leftHand : subsystem.rightHand;

                var completenessScore = 0f;
                if (hand.isTracked)
                {
                    m_HandShapeCompletenessCalculator.TryCalculateHandShapeCompletenessScore(
                        hand, m_HandShape, out completenessScore);
                }

                m_HandShapeCompletenessProgressBar.value = completenessScore;
            }
        }

        /// <summary>
        /// Clear the detected hand shape reference in order to stop displaying any corresponding UI
        /// </summary>
        public void ClearDetectedHandShape()
        {
            handShapeOrPose = null;
        }

        bool TryGetHandSubsystem(out XRHandSubsystem system)
        {
            if (m_Subsystem != null && m_Subsystem.running)
            {
                system = m_Subsystem;
                return true;
            }

            SubsystemManager.GetSubsystems(s_SubsystemsReuse);
            foreach (var handSubsystem in s_SubsystemsReuse)
            {
                if (handSubsystem.running)
                {
                    m_Subsystem = handSubsystem;
                    system = m_Subsystem;
                    return true;
                }
            }

            system = null;
            return false;
        }
    }
}
