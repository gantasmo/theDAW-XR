using System;
using System.Collections.Generic;
using UnityEngine.XR.Hands.Capture;
using UnityEngine.XR.Hands.Capture.Recording;

namespace UnityEngine.XR.Hands.Samples.Capture
{
    public class RecordingController
    {
        const int k_MaxRecordingCount = 5;
        const float k_RecordingTimeLimitInSeconds = 60.0f;

        XRHandRecordingBase[] m_RecordingSlots;
        bool[] m_SlotsOccupied;
        string m_CurrentRecordingName;
        int m_CurrentRecordingSlotIdx;
        static readonly List<XRHandSubsystem> s_SubsystemsReuse = new List<XRHandSubsystem>();
        XRHandSubsystem m_Subsystem;
        XRHandRecordingInitializeArgs m_RecordingInitArgs;
        XRHandRecordingSaveArgs m_RecordingSaveArgs;
        CaptureSessionManager m_CaptureSessionManager;

        public event Action<XRHandRecordingStatusChangedEventArgs> recordingStatusChanged;
        public event Action<XRHandRecordingFrameCapturedEventArgs> recordingFrameCaptured;
        public event Action<int> recordingDeleted;
        public int maxRecordingCount => k_MaxRecordingCount;
        public float recordingTimeLimitInSeconds => k_RecordingTimeLimitInSeconds;
        public XRHandRecordingBase GetCurrentRecording() => m_RecordingSlots[m_CurrentRecordingSlotIdx];
        public int currentRecordingSlotIdx => m_CurrentRecordingSlotIdx;

        public string currentRecordingName
        {
            get => m_CurrentRecordingName;
            set => m_CurrentRecordingName = value;
        }

        public RecordingController(CaptureSessionManager captureSessionManager)
        {
            m_CaptureSessionManager = captureSessionManager;
            XRHandRecordingSettings.timeLimitInSeconds = k_RecordingTimeLimitInSeconds;

            m_CurrentRecordingSlotIdx = 0;

            InitializeRecordingSlots();
        }

        public void Tick()
        {
            if (m_Subsystem != null && m_Subsystem.running)
                return;

            SubsystemManager.GetSubsystems(s_SubsystemsReuse);
            for (var i = 0; i < s_SubsystemsReuse.Count; ++i)
            {
                var handSubsystem = s_SubsystemsReuse[i];
                if (handSubsystem.running)
                {
                    m_Subsystem = handSubsystem;
                    break;
                }
            }
        }

        void InitializeRecordingSlots()
        {
            m_RecordingSlots = new XRHandRecordingBase[k_MaxRecordingCount];
            m_SlotsOccupied = new bool[m_RecordingSlots.Length];

            // Load previously saved recordings' metadata from the device's persistent data path
            XRHandRecordingMetadata.GetSavedRecordingMetadata(s_ExistingRecordingsReuse);

            // Populate recording slots with existing data or new blobs.
            int recordingSlotIndex = 0;
            for (; recordingSlotIndex < s_ExistingRecordingsReuse.Count; ++recordingSlotIndex)
            {
                m_RecordingSlots[recordingSlotIndex] = s_ExistingRecordingsReuse[recordingSlotIndex];
                m_SlotsOccupied[recordingSlotIndex] = true;
            }

            for (; recordingSlotIndex < m_RecordingSlots.Length; ++recordingSlotIndex)
            {
                m_RecordingSlots[recordingSlotIndex] = new XRHandRecordingBlob();
                m_SlotsOccupied[recordingSlotIndex] = false;
            }

            s_ExistingRecordingsReuse.Clear();
        }

        static readonly List<XRHandRecordingMetadata> s_ExistingRecordingsReuse = new List<XRHandRecordingMetadata>();

        public XRHandRecordingBase GetRecordingAtSlot(int idx)
        {
            if (!IsSlotIndexValid(idx))
                return null;

            return m_RecordingSlots[idx];
        }

        public void StartRecording(bool alsoCaptureBeforeRender = false)
        {
            if (m_Subsystem == null)
            {
                Debug.LogError("No XRHandSubsystem found.");
                return;
            }

            var recordingOptions = XRHandRecordingOptions.None;
            if (alsoCaptureBeforeRender)
                recordingOptions |= XRHandRecordingOptions.AlsoCaptureBeforeRender;

            m_RecordingInitArgs = new XRHandRecordingInitializeArgs
            {
                subsystem = m_Subsystem,
                recordingOptions = recordingOptions,
            };

            if (GetCurrentRecording() is XRHandRecordingBlob recordingBlob)
            {
                recordingBlob.TryInitialize(m_RecordingInitArgs);
            }
        }

        public void StopRecording()
        {
            if (GetCurrentRecording() is XRHandRecordingBlob recordingBlob)
                recordingBlob.Stop();
        }

        public void SaveRecording()
        {
            if (GetCurrentRecording() is XRHandRecordingBlob recordingBlob)
            {
                m_RecordingSaveArgs = new XRHandRecordingSaveArgs
                {
                    recordingName = m_CurrentRecordingName
                };
                recordingBlob.TrySave(m_RecordingSaveArgs);
            }
        }

        public void DiscardRecording()
        {
            if (GetCurrentRecording() is XRHandRecordingBlob recordingBlob)
            {
                recordingBlob.Dispose();
            }

            ClearRecordingSlot(m_CurrentRecordingSlotIdx);
        }

        public void DeleteRecording(int slotIdx)
        {
            if (!IsSlotIndexValid(slotIdx))
                return;

            m_RecordingSlots[slotIdx].Delete();

            ClearRecordingSlot(slotIdx);
        }

        bool IsSlotIndexValid(int recordingSlot)
        {
            bool isValid = recordingSlot >= 0 && recordingSlot < m_RecordingSlots.Length;
            if (!isValid)
            {
                Debug.LogError($"Invalid recording slot: {recordingSlot}");
            }
            return isValid;
        }

        public bool IsSlotOccupied(int slotIdx)
        {
            if (!IsSlotIndexValid(slotIdx))
                return false;

            return m_SlotsOccupied[slotIdx];
        }

        public bool TryFindNextFreeSlot(out int nextAvailableSlot)
        {
            nextAvailableSlot = -1;
            for (int i = 0; i < m_SlotsOccupied.Length; i++)
            {
                if (!m_SlotsOccupied[i])
                {
                    nextAvailableSlot = i;
                    return true;
                }
            }
            return false;
        }

        void OnRecordingStatusChanged(XRHandRecordingStatusChangedEventArgs args)
        {
            recordingStatusChanged?.Invoke(args);
        }

        void OnRecordingFrameCaptured(XRHandRecordingFrameCapturedEventArgs args)
        {
            recordingFrameCaptured?.Invoke(args);
        }

        void UpdateCurrentRecordingSlot(int newSlotIdx)
        {
            if (!IsSlotIndexValid(newSlotIdx))
                return;

            m_CurrentRecordingSlotIdx = newSlotIdx;

            // If the current slot is occupied by an existing recording blob, reset it
            if (m_RecordingSlots[m_CurrentRecordingSlotIdx] is XRHandRecordingBlob recordingBlob)
            {
                recordingBlob.Reset();
                recordingBlob.statusChanged += OnRecordingStatusChanged;
                recordingBlob.frameCaptured += OnRecordingFrameCaptured;
            }
            // If the current slot is occupied by metadata, create a new recording blob
            else if (m_RecordingSlots[m_CurrentRecordingSlotIdx] is XRHandRecordingMetadata)
            {
                var newRecordingBlob = new XRHandRecordingBlob();
                newRecordingBlob.statusChanged += OnRecordingStatusChanged;
                newRecordingBlob.frameCaptured += OnRecordingFrameCaptured;
                m_RecordingSlots[m_CurrentRecordingSlotIdx] = newRecordingBlob;
            }

            m_SlotsOccupied[m_CurrentRecordingSlotIdx] = true;
        }

        public bool TryActivateNextSlotForRecording()
        {
            // Find the next available slot for recording
            if (!TryFindNextFreeSlot(out var nextAvailableSlot))
                return false;

            // Update the current recording slot to the next available one
            UpdateCurrentRecordingSlot(nextAvailableSlot);

            return true;
        }

        void ClearRecordingSlot(int recordingIdx)
        {
            if (!IsSlotIndexValid(recordingIdx))
                return;

            if (m_RecordingSlots[recordingIdx] is XRHandRecordingBlob recordingBlob)
            {
                recordingBlob.statusChanged -= OnRecordingStatusChanged;
                recordingBlob.frameCaptured -= OnRecordingFrameCaptured;
                recordingBlob.Clear();
            }

            m_SlotsOccupied[recordingIdx] = false;
            recordingDeleted?.Invoke(recordingIdx);
        }
    }
}
