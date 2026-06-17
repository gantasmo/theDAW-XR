#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands.Capture.Recording;

namespace UnityEngine.XR.Hands.Samples.Capture
{
    public class CaptureSessionManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField]
        [Tooltip("The UI panel that shows starting scene for the user")]
        GameObject m_InstructionUI;

        [SerializeField]
        [Tooltip("The detailed instruction steps UI")]
        GameObject m_InstructionStepsUI;

        [SerializeField]
        [Tooltip("The UI panel that shows the recording ready state")]
        GameObject m_ReadyToRecordUI;

        [SerializeField]
        [Tooltip("The UI panel that shows the recording in progress")]
        GameObject m_RecordingUI;

        [SerializeField]
        [Tooltip("The UI panel shown after recording stops with file naming and save/discard options")]
        GameObject m_RecordingStoppedUI;

        [SerializeField]
        [Tooltip("The UI panel that shows the saved recording state and next steps instruction")]
        GameObject m_RecordingSavedUI;

        [SerializeField]
        [Tooltip("The UI that shows a warning when all recording slots are full")]
        GameObject m_RecordingFullWarningUI;

        [Header("UI Buttons")]
        [SerializeField]
        [Tooltip("The button to toggle the visibility of the instruction steps")]
        Button m_ShowInstructionStepsButton;

        [SerializeField]
        [Tooltip("The button to skip the instruction steps and go to ReadyToRecord panel")]
        Button m_SkipInstructionButton;

        [SerializeField]
        [Tooltip("The button to start recording on the ReadyToRecord panel")]
        Button m_StartRecordingButton;

        [SerializeField]
        [Tooltip("The button to stop recording on the Recording panel")]
        Button m_StopRecordingButton;

        [SerializeField]
        [Tooltip("The button to save the recording on the RecordingStopped panel")]
        Button m_SaveButton;

        [SerializeField]
        [Tooltip("The button to discard the recording on the RecordingStopped panel")]
        Button m_DiscardButton;

        [SerializeField]
        [Tooltip("The button to start a new recording on the RecordingSaved panel")]
        Button m_RecordAnotherButton;

        [SerializeField]
        [Tooltip("The list of existing recordings")]
        RecordingItemView[] m_RecordingItemViews;

        [Header("UI Text")]
        [SerializeField]
        [Tooltip("The text on the button that toggles detailed instruction steps visibility")]
        TextMeshProUGUI m_InstructionStepsButtonText;

        [SerializeField]
        [Tooltip("The text on the Ready To Record panel that gives instructions")]
        TextMeshProUGUI m_ReadyInstructionText;

        [SerializeField]
        [Tooltip("The text on the Saved Recording panel that gives instructions for next steps")]
        TextMeshProUGUI m_NextStepsInstructionText;

        [SerializeField]
        [Tooltip("The text showing the saved recording name on the RecordingSaved panel")]
        TextMeshProUGUI m_SavedRecordingText;

        [Header("Time Text")]
        [SerializeField]
        [Tooltip("The text showing the live recording time")]
        TextMeshProUGUI m_RecordingTimeText;

        [SerializeField]
        [Tooltip("The text showing the final time after stopping")]
        TextMeshProUGUI m_RecordingStoppedTimeText;

        [SerializeField]
        [Tooltip("The text showing the maximum recording time on the ReadyToRecord panel")]
        TextMeshProUGUI m_ReadyMaxTimeText;

        [SerializeField]
        [Tooltip("The text showing the maximum recording time on the Recording panel")]
        TextMeshProUGUI m_RecordingMaxTimeText;

        [Header("Recording Index Text Fields")]
        [SerializeField]
        [Tooltip("The recording index text on the ReadyToRecord panel")]
        TextMeshProUGUI m_ReadyIndexText;

        [Tooltip("The recording index text on the RecordingStopped panel")]
        [SerializeField]
        TextMeshProUGUI m_StoppedIndexText;

        [Header("Input Field")]
        [SerializeField]
        [Tooltip("The input field for the recording file name")]
        TMP_InputField m_UserInputField;

        [Header("Recording Progress Bar")]
        [SerializeField]
        [Tooltip("The progress bar slider that fills up during recording")]
        Slider m_RecordingProgressSlider;

        RecorderUserJourneyManager m_UserJourneyManager;
        RecordingController m_RecordingController;

        const string k_RecordingFileDefaultPrefix = "Recording_";
        int m_CurrentRecordingNameIndexSuffix;

        void Awake()
        {
            m_UserJourneyManager = new RecorderUserJourneyManager();

            m_RecordingController = new RecordingController();

            CheckSerializedFields();
        }

        void OnEnable()
        {
            m_UserJourneyManager.onJourneyStateChanged += HandleJourneyStateChanged;

            m_RecordingController.recordingStatusChanged += HandleRecordingStatusChanged;
            m_RecordingController.recordingFrameCaptured += HandleRecordingFrameCaptured;
            m_RecordingController.recordingDeleted += HandleRecordingDeleted;

            SetupButtonListeners();
            SetupInputFieldListener();
        }

        void Start()
        {
            InitializeUI();
        }

        void SetupButtonListeners()
        {
            m_StartRecordingButton.onClick.AddListener(m_RecordingController.StartRecording);
            m_StopRecordingButton.onClick.AddListener(m_RecordingController.StopRecording);
            m_SaveButton.onClick.AddListener(m_RecordingController.SaveRecording);
            m_DiscardButton.onClick.AddListener(DiscardCurrentRecording);
            m_RecordAnotherButton.onClick.AddListener(MakeAnotherRecording);
            m_SkipInstructionButton.onClick.AddListener(StartUserJourney);
            m_ShowInstructionStepsButton.onClick.AddListener(ToggleInstructionSteps);

            for (var i = 0; i < m_RecordingController.maxRecordingCount; i++)
            {
                int recordingIdx = i;
                m_RecordingItemViews[i].deleteButton.onClick.AddListener(
                    () => m_RecordingController.DeleteRecording(recordingIdx));
            }
        }

        void RemoveButtonListeners()
        {
            m_StartRecordingButton.onClick.RemoveListener(m_RecordingController.StartRecording);
            m_StopRecordingButton.onClick.RemoveListener(m_RecordingController.StopRecording);
            m_SaveButton.onClick.RemoveListener(m_RecordingController.SaveRecording);
            m_DiscardButton.onClick.RemoveListener(DiscardCurrentRecording);
            m_RecordAnotherButton.onClick.RemoveListener(MakeAnotherRecording);
            m_SkipInstructionButton.onClick.RemoveListener(StartUserJourney);
            m_ShowInstructionStepsButton.onClick.RemoveListener(ToggleInstructionSteps);

            for (var i = 0; i < m_RecordingController.maxRecordingCount; i++)
            {
                int recordingIdx = i;
                m_RecordingItemViews[i].deleteButton.onClick.RemoveListener(
                    () => m_RecordingController.DeleteRecording(recordingIdx));
            }
        }

        void OnDisable()
        {
            m_UserJourneyManager.onJourneyStateChanged -= HandleJourneyStateChanged;

            m_RecordingController.recordingStatusChanged -= HandleRecordingStatusChanged;
            m_RecordingController.recordingFrameCaptured -= HandleRecordingFrameCaptured;
            m_RecordingController.recordingDeleted -= HandleRecordingDeleted;

            RemoveButtonListeners();

            if (m_UserInputField != null)
            {
                m_UserInputField.onValueChanged.RemoveListener(HandleUserInput);
            }
        }

        void Update()
        {
            m_RecordingController.Tick();
        }

        void HandleRecordingFrameCaptured(XRHandRecordingFrameCapturedEventArgs args)
        {
            m_RecordingTimeText.text = FormatTime(args.elapsedTime);
            m_RecordingProgressSlider.value =
                Mathf.Clamp01(args.elapsedTime / m_RecordingController.recordingTimeLimitInSeconds);
        }

        void HandleRecordingStatusChanged(XRHandRecordingStatusChangedEventArgs args)
        {
            switch (args.status)
            {
                case XRHandRecordingStatus.Ready:
                    m_UserJourneyManager.SetUserJourneyState(
                        RecorderUserJourneyManager.UserJourneyState.Recording);
                    break;
                case XRHandRecordingStatus.StoppedManually:
                case XRHandRecordingStatus.StoppedAtTimeLimit:
                case XRHandRecordingStatus.StoppedWithError:
                    m_RecordingStoppedTimeText.text = FormatTime(args.elapsedTime);
                    m_UserJourneyManager.SetUserJourneyState(
                        RecorderUserJourneyManager.UserJourneyState.RecordingStopped);
                    break;
                case XRHandRecordingStatus.Saved:
                    var assetName = m_RecordingController.currentRecording.assetName;
                    var slotIdx = m_RecordingController.currentRecordingSlotIdx;

                    m_RecordingItemViews[slotIdx].gameObject.SetActive(true);
                    m_RecordingItemViews[slotIdx].UpdateView(assetName, FormatTime(args.elapsedTime));
                    m_SavedRecordingText.text = $"'{assetName}' saved";

                    m_UserJourneyManager.SetUserJourneyState(
                        RecorderUserJourneyManager.UserJourneyState.RecordingSaved);
                    break;
            }
        }

        void HandleRecordingDeleted(int recordingSlotDeleted)
        {
            // Hide and reset the recording item
            if (recordingSlotDeleted >= 0 && recordingSlotDeleted < m_RecordingItemViews.Length)
                m_RecordingItemViews[recordingSlotDeleted].gameObject.SetActive(false);

            RefreshRecordingCapacityUI();
        }

        void SetupInputFieldListener()
        {
            if (m_UserInputField != null)
            {
                m_UserInputField.onValueChanged.AddListener(HandleUserInput);
            }
        }

        void InitializeUI()
        {
            HideAllUIPanels();

            InitializeRecordingItems();
            ParseExistingRecordings();

            m_InstructionUI.SetActive(true);

            // Populate the instruction text
            var instructionText = $"Record upto {m_RecordingController.maxRecordingCount} recordings in the scene. " +
                $"Export and save old recordings on the device to make space for new ones.";
            m_ReadyInstructionText.text = instructionText;

            // Populate the maximum recording time text
            var maxTimeText = $"{m_RecordingController.recordingTimeLimitInSeconds} secs max";
            m_ReadyMaxTimeText.text = maxTimeText;
            m_RecordingMaxTimeText.text = maxTimeText;

            // Initialize the instruction steps UI
            m_InstructionStepsUI.SetActive(false);
            if (m_ShowInstructionStepsButton.GetComponentInChildren<TextMeshProUGUI>() != null)
            {
                TextMeshProUGUI buttonText = m_ShowInstructionStepsButton.GetComponentInChildren<TextMeshProUGUI>();
                buttonText.text = m_InstructionStepsUI.activeSelf ? "Hide Steps" : "See Steps";
            }

            m_NextStepsInstructionText.text =
                "1. Connect headset to PC\n" +
                "2. Open HandCapture scene\n" +
                "3. Go to Window > XR > XR Hand Capture\n" +
                "4. Import recordings & create XRHandShape assets";

            InitializePlayModeOnlyUI();
        }

        void InitializePlayModeOnlyUI()
        {
            var isPlayMode = Application.isEditor && Application.isPlaying;
            if (isPlayMode)
            {
                m_NextStepsInstructionText.text =
                    "Exit PlayMode and go to Window > XR > XR Hand Capture. Follow the steps to create XRHandShape assets.";
            }
        }

        void HideAllUIPanels()
        {
            m_InstructionUI.SetActive(false);
            m_ReadyToRecordUI.SetActive(false);
            m_RecordingUI.SetActive(false);
            m_RecordingStoppedUI.SetActive(false);
            m_RecordingSavedUI.SetActive(false);
        }

        void InitializeRecordingItems()
        {
            if (m_RecordingItemViews == null ||
                m_RecordingItemViews.Length != m_RecordingController.maxRecordingCount)
            {
                Debug.LogError("Incorrect number of Recording Items");
                return;
            }
            for (var i = 0; i < m_RecordingItemViews.Length; i++)
            {
                // Set active state based on whether the slot is occupied
                bool isOccupied = m_RecordingController.IsSlotOccupied(i);
                m_RecordingItemViews[i].gameObject.SetActive(isOccupied);

                // If pre-occupied, populate the recording item UI
                if (isOccupied)
                {
                    var existingRecording = m_RecordingController.GetRecordingAtSlot(i);
                    m_RecordingItemViews[i].UpdateView(
                        existingRecording.assetName, FormatTime(existingRecording.durationInSeconds));
                }
            }
        }

        void ParseExistingRecordings()
        {
            int maxSuffix = 0;

            for (var i = 0; i < m_RecordingController.maxRecordingCount; ++i)
            {
                if (!m_RecordingController.IsSlotOccupied(i))
                    break;

                // Parse the suffix from the asset name if it matches _{number}
                if (TryGetSuffix(m_RecordingController.GetRecordingAtSlot(i).assetName, out int suffix))
                {
                    maxSuffix = Mathf.Max(maxSuffix, suffix);
                }
            }

            m_CurrentRecordingNameIndexSuffix = maxSuffix;
            m_SavedRecordingText.text = $"";
        }

        void ToggleInstructionSteps()
        {
            if (m_InstructionStepsUI != null)
            {
                // Toggle the visibility state
                bool currentState = m_InstructionStepsUI.activeSelf;
                m_InstructionStepsUI.SetActive(!currentState);

                // Update button text based on the new state
                if (m_InstructionStepsButtonText != null)
                {
                    m_InstructionStepsButtonText.text = m_InstructionStepsUI.activeSelf ? "Hide Steps" : "See Steps";
                }
            }
        }

        void UpdatePrefabVisibility(RecorderUserJourneyManager.UserJourneyState newState)
        {
            HideAllUIPanels();

            switch (newState)
            {
                case RecorderUserJourneyManager.UserJourneyState.Instruction:
                    m_InstructionUI.SetActive(true);
                    break;
                case RecorderUserJourneyManager.UserJourneyState.ReadyToRecord:
                    m_ReadyToRecordUI.SetActive(true);
                    break;
                case RecorderUserJourneyManager.UserJourneyState.Recording:
                    m_RecordingUI.SetActive(true);
                    break;
                case RecorderUserJourneyManager.UserJourneyState.RecordingStopped:
                    m_RecordingStoppedUI.SetActive(true);
                    break;
                case RecorderUserJourneyManager.UserJourneyState.RecordingSaved:
                    m_RecordingSavedUI.SetActive(true);
                    break;
                default:
                    Debug.LogWarning($"Unhandled user journey state: {newState}");
                    break;
            }
        }

        void HandleJourneyStateChanged(RecorderUserJourneyManager.JourneyStateChangeEventArgs args)
        {
            // Update UI visibility based on the new user journey state
            UpdatePrefabVisibility(args.currentState);

            if (args.currentState == RecorderUserJourneyManager.UserJourneyState.RecordingSaved)
            {
                RefreshRecordingCapacityUI();
            }
        }

        void RefreshRecordingCapacityUI()
        {
            // Disable the "Record Another" button and show the "Recording Full" warning if no slots are available
            bool hasAvailableSlot = m_RecordingController.TryFindNextFreeSlot(out var _);
            m_RecordAnotherButton.interactable = hasAvailableSlot;
            m_RecordingFullWarningUI.SetActive(!hasAvailableSlot);
        }

        void DiscardCurrentRecording()
        {
            m_RecordingController.DiscardRecording();

            MakeAnotherRecording();
        }

        void MakeAnotherRecording()
        {
            SetupNewRecordingSession();

            m_UserJourneyManager.SetUserJourneyState(RecorderUserJourneyManager.UserJourneyState.ReadyToRecord);
        }

        static string GetDefaultRecordingAssetName(int recordingIndex)
        {
            return $"{k_RecordingFileDefaultPrefix}{recordingIndex}";
        }

        void HandleUserInput(string fileName)
        {
            // Strip any existing extension
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            m_RecordingController.currentRecordingName = fileNameWithoutExtension;
        }

        void StartUserJourney()
        {
            // If all recording slots are full,
            // transition to RecordingSaved where the user can delete recordings to make space
            if (!m_RecordingController.TryFindNextFreeSlot(out var _))
            {
                m_UserJourneyManager.SetUserJourneyState(RecorderUserJourneyManager.UserJourneyState.RecordingSaved);
            }
            else
            {
                SetupNewRecordingSession();
                m_UserJourneyManager.SetUserJourneyState(RecorderUserJourneyManager.UserJourneyState.ReadyToRecord);
            }
        }

        static string FormatTime(float timeInSeconds)
        {
            var timeSpan = TimeSpan.FromSeconds(timeInSeconds);
            var seconds = (int)timeSpan.TotalSeconds;
            // Get the millisecond part and divide by 10 to get hundredths of a second
            var hundredths = timeSpan.Milliseconds / 10;

            return $"{seconds:00}:{hundredths:00}";
        }

        static bool TryGetSuffix(string fileName, out int suffix)
        {
            suffix = 0;

            // Parse the suffix from the file name if it matches _{number}
            var match = System.Text.RegularExpressions.Regex.Match(fileName, @"_(\d+)$");

            return match.Success && int.TryParse(match.Groups[1].Value, out suffix);
        }

        void SetupNewRecordingSession()
        {
            if (!m_RecordingController.TryActivateNextSlotForRecording())
            {
                // If no slot is available, show the Recording Full warning
                RefreshRecordingCapacityUI();
                return;
            }

            // Set the new recording name with incremented suffix
            m_CurrentRecordingNameIndexSuffix++;
            var newRecordingName = GetDefaultRecordingAssetName(m_CurrentRecordingNameIndexSuffix);
            m_RecordingController.currentRecordingName = newRecordingName;

            // Update the input field with the new recording file name
            m_UserInputField.text = newRecordingName;

            // Update the recording slot text in all relevant UI panels
            var displaySlotNumber = m_RecordingController.currentRecordingSlotIdx + 1;
            m_ReadyIndexText.text = $"Recording Slot {displaySlotNumber}/{m_RecordingController.maxRecordingCount}";
            m_StoppedIndexText.text = $"Recording Slot {displaySlotNumber}";
        }

        void CheckField(Object field, string fieldName)
        {
            if (field == null)
                Debug.LogError($"{fieldName} is not assigned in the Inspector.", this);
        }

        void CheckSerializedFields()
        {
            CheckField(m_InstructionUI, nameof(m_InstructionUI));
            CheckField(m_InstructionStepsUI, nameof(m_InstructionStepsUI));
            CheckField(m_ReadyToRecordUI, nameof(m_ReadyToRecordUI));
            CheckField(m_RecordingUI, nameof(m_RecordingUI));
            CheckField(m_RecordingStoppedUI, nameof(m_RecordingStoppedUI));
            CheckField(m_RecordingSavedUI, nameof(m_RecordingSavedUI));
            CheckField(m_RecordingFullWarningUI, nameof(m_RecordingFullWarningUI));
            CheckField(m_ShowInstructionStepsButton, nameof(m_ShowInstructionStepsButton));
            CheckField(m_SkipInstructionButton, nameof(m_SkipInstructionButton));
            CheckField(m_StartRecordingButton, nameof(m_StartRecordingButton));
            CheckField(m_StopRecordingButton, nameof(m_StopRecordingButton));
            CheckField(m_SaveButton, nameof(m_SaveButton));
            CheckField(m_DiscardButton, nameof(m_DiscardButton));
            CheckField(m_RecordAnotherButton, nameof(m_RecordAnotherButton));
            CheckField(m_InstructionStepsButtonText, nameof(m_InstructionStepsButtonText));
            CheckField(m_ReadyInstructionText, nameof(m_ReadyInstructionText));
            CheckField(m_NextStepsInstructionText, nameof(m_NextStepsInstructionText));
            CheckField(m_RecordingTimeText, nameof(m_RecordingTimeText));
            CheckField(m_RecordingStoppedTimeText, nameof(m_RecordingStoppedTimeText));
            CheckField(m_ReadyMaxTimeText, nameof(m_ReadyMaxTimeText));
            CheckField(m_RecordingMaxTimeText, nameof(m_RecordingMaxTimeText));
            CheckField(m_ReadyIndexText, nameof(m_ReadyIndexText));
            CheckField(m_StoppedIndexText, nameof(m_StoppedIndexText));
            CheckField(m_SavedRecordingText, nameof(m_SavedRecordingText));
            CheckField(m_UserInputField, nameof(m_UserInputField));
            CheckField(m_RecordingProgressSlider, nameof(m_RecordingProgressSlider));

            if (m_RecordingItemViews == null)
                Debug.LogError($"{nameof(m_RecordingItemViews)} array is not assigned in the Inspector.", this);
            else if (m_RecordingItemViews.Length != m_RecordingController.maxRecordingCount)
                Debug.LogError($"{nameof(m_RecordingItemViews)} array length ({m_RecordingItemViews.Length}) " +
                    $"doesn't match the required recording count ({m_RecordingController.maxRecordingCount}).");
        }
    }
}
#endif
