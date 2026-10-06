#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
using System;
using TMPro;
using UnityEngine.UI;

namespace UnityEngine.XR.Hands.Samples.Capture
{
    class RecordingItemView : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI m_RecordingNameText;

        [SerializeField]
        TextMeshProUGUI m_DurationTimeText;

        [SerializeField]
        Button m_DeleteButton;

        public Button deleteButton => m_DeleteButton;

        public void UpdateView(string recordingName, string timeInSeconds)
        {
            m_RecordingNameText.text = recordingName;
            m_DurationTimeText.text = timeInSeconds;
        }
    }
}
#endif
