using System;
using System.Collections.Generic;
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
        XRHandRecordingBase m_CurrentRecording => m_RecordingSlots[m_CurrentRecordingSlotIdx];
        static readonly List<XRHandSubsystem> s_SubsystemsReuse = new List<XRHandSubsystem>();
        XRHandSubsystem m_Subsystem;
        XRHandRecordingInitializeArgs m_RecordingInitArgs;
        XRHandRecordingSaveArgs m_RecordingSaveArgs;

        public event Action<XRHandRecordingStatusChangedEventArgs> recordingStatusChanged;
        public event Action<XRHandRecordingFrameCapturedEventArgs> recordingFrameCaptured;
        public event Action<int> recordingDeleted;
        public int maxRecordingCount => k_MaxRecordingCount;
        public float recordingTimeLimitInSeconds => k_RecordingTimeLimitInSeconds;
        public XRHandRecordingBase currentRecording => m_CurrentRecording;
        public int currentRecordingSlotIdx => m_CurrentRecordingSlotIdx;

        public string currentRecordingName
        {
            get => m_CurrentRecordingName;
            set => m_CurrentRecordingName = value;
        }

        public RecordingController()
        {
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
            List<XRHandRecordingMetadata> existingRecordings = new List<XRHandRecordingMetadata>();
            XRHandRecordingMetadata.GetSavedRecordingMetadata(existingRecordings);

            // Populate recording slots with existing data or new blobs.
            for (var i = 0; i < m_RecordingSlots.Length; ++i)
            {
                if (i < existingRecordings.Count)
                {
                    m_RecordingSlots[i] = existingRecordings[i];
                    m_SlotsOccupied[i] = true;
                }
                else
                {
                    m_RecordingSlots[i] = new XRHandRecordingBlob();
                    m_SlotsOccupied[i] = false;
                }
            }
        }

        public XRHandRecordingBase GetRecordingAtSlot(int idx)
        {
            if (!IsSlotIndexValid(idx))
                return null;

            return m_RecordingSlots[idx];
        }

        public void StartRecording()
        {
            if (m_Subsystem == null)
            {
                Debug.LogError("No XRHandSubsystem found.");
                return;
            }

            m_RecordingInitArgs = new XRHandRecordingInitializeArgs
            {
                subsystem = m_Subsystem
            };

            if (m_CurrentRecording is XRHandRecordingBlob recordingBlob)
            {
                recordingBlob.TryInitialize(m_RecordingInitArgs);
            }
        }

        public void StopRecording()
        {
            if (m_CurrentRecording is XRHandRecordingBlob recordingBlob)
                recordingBlob.Stop();
        }

        public void SaveRecording()
        {
            if (m_CurrentRecording is XRHandRecordingBlob recordingBlob)
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
            if (m_CurrentRecording is XRHandRecordingBlob recordingBlob)
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
