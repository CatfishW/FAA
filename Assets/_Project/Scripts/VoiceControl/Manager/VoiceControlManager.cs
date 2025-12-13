using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoiceControl.Core;
using VoiceControl.Network;

namespace VoiceControl.Manager
{
    /// <summary>
    /// Main coordinator for voice control system.
    /// Manages audio recording, STT, LLM interpretation, and command execution pipeline.
    /// 
    /// State Machine: Idle -> Recording -> Processing -> Executing -> Idle
    /// </summary>
    [AddComponentMenu("Voice Control/Voice Control Manager")]
    public class VoiceControlManager : MonoBehaviour
    {
        #region Inspector Fields
        
        [Header("Settings")]
        [SerializeField] private VoiceControlSettings settings;
        
        [Header("Dependencies")]
        [SerializeField] private STTClient sttClient;
        [SerializeField] private LLMClient llmClient;
        [SerializeField] private VoiceCommandRegistry registry;
        [SerializeField] private VoiceCommandExecutor executor;
        
        [Header("Microphone")]
        [SerializeField] private string microphoneDeviceName = "";
        
        [Header("Debug")]
        [SerializeField] private bool verboseLogging = true;
        
        #endregion
        
        #region Public Events
        
        /// <summary>
        /// Fired when recording state changes
        /// </summary>
        public event Action<bool> OnRecordingStateChanged;
        
        /// <summary>
        /// Fired with audio level updates during recording (0-1)
        /// </summary>
        public event Action<float> OnAudioLevelUpdate;
        
        /// <summary>
        /// Fired when transcription is received
        /// </summary>
        public event Action<string> OnTranscriptionReceived;
        
        /// <summary>
        /// Fired when LLM response is received
        /// </summary>
        public event Action<string> OnLLMResponseReceived;
        
        /// <summary>
        /// Fired when command execution completes
        /// </summary>
        public event Action<string, bool> OnCommandExecuted;
        
        /// <summary>
        /// Fired when processing state changes
        /// </summary>
        public event Action<VoiceControlState> OnStateChanged;
        
        /// <summary>
        /// Fired on any error
        /// </summary>
        public event Action<string> OnError;
        
        #endregion
        
        #region Public Properties
        
        public VoiceControlState CurrentState { get; private set; } = VoiceControlState.Idle;
        public bool IsRecording => CurrentState == VoiceControlState.Recording;
        public bool IsProcessing => CurrentState == VoiceControlState.ProcessingSTT || 
                                    CurrentState == VoiceControlState.ProcessingLLM || 
                                    CurrentState == VoiceControlState.Executing;
        public float CurrentAudioLevel { get; private set; }
        public string LastTranscription { get; private set; }
        public string LastError { get; private set; }
        
        #endregion
        
        #region Private Fields
        
        private AudioClip _recordingClip;
        private int _lastSamplePosition;
        private float _recordingStartTime;
        private float _lastSoundTime;
        private float[] _sampleBuffer;
        private bool _autoStopEnabled = true;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            AutoFindComponents();
            CreateDefaultSettings();
        }
        
        private void Start()
        {
            InitializeComponents();
        }
        
        private void Update()
        {
            if (CurrentState == VoiceControlState.Recording)
            {
                UpdateRecording();
            }
        }
        
        private void OnDestroy()
        {
            if (IsRecording)
            {
                StopRecording(false);
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Start recording voice input
        /// </summary>
        public void StartRecording()
        {
            if (CurrentState != VoiceControlState.Idle)
            {
                Log("Cannot start recording - not in idle state");
                return;
            }
            
            if (!Microphone.IsRecording(microphoneDeviceName))
            {
                int sampleRate = settings != null ? settings.sampleRate : 16000;
                float maxDuration = settings != null ? settings.maxRecordingDuration : 30f;
                
                _recordingClip = Microphone.Start(microphoneDeviceName, false, (int)maxDuration + 1, sampleRate);
                
                if (_recordingClip == null)
                {
                    SetError("Failed to start microphone recording");
                    return;
                }
                
                _lastSamplePosition = 0;
                _recordingStartTime = Time.time;
                _lastSoundTime = Time.time;
                _sampleBuffer = new float[sampleRate / 10]; // 100ms buffer
                
                SetState(VoiceControlState.Recording);
                OnRecordingStateChanged?.Invoke(true);
                
                Log("Recording started");
            }
        }
        
        /// <summary>
        /// Stop recording and process the audio
        /// </summary>
        /// <param name="process">Whether to process the audio or discard it</param>
        public void StopRecording(bool process = true)
        {
            if (!Microphone.IsRecording(microphoneDeviceName))
            {
                return;
            }
            
            int samplePosition = Microphone.GetPosition(microphoneDeviceName);
            Microphone.End(microphoneDeviceName);
            
            OnRecordingStateChanged?.Invoke(false);
            
            float recordingDuration = Time.time - _recordingStartTime;
            float minDuration = settings != null ? settings.minRecordingDuration : 0.5f;
            
            if (recordingDuration < minDuration)
            {
                Log($"Recording too short ({recordingDuration:F2}s < {minDuration}s), discarding");
                SetState(VoiceControlState.Idle);
                return;
            }
            
            if (process)
            {
                Log($"Recording stopped after {recordingDuration:F2}s, processing...");
                ProcessRecording(samplePosition);
            }
            else
            {
                Log("Recording discarded");
                SetState(VoiceControlState.Idle);
            }
        }
        
        /// <summary>
        /// Toggle recording state
        /// </summary>
        public void ToggleRecording()
        {
            if (IsRecording)
            {
                StopRecording(true);
            }
            else if (CurrentState == VoiceControlState.Idle)
            {
                StartRecording();
            }
        }
        
        /// <summary>
        /// Cancel any current operation
        /// </summary>
        public void Cancel()
        {
            if (IsRecording)
            {
                StopRecording(false);
            }
            
            StopAllCoroutines();
            SetState(VoiceControlState.Idle);
        }
        
        /// <summary>
        /// Test connectivity to both STT and LLM servers
        /// </summary>
        public void TestConnectivity(Action<bool, string, bool, string> callback)
        {
            StartCoroutine(TestConnectivityCoroutine(callback));
        }
        
        #endregion
        
        #region Private Methods
        
        private void AutoFindComponents()
        {
            if (sttClient == null)
                sttClient = GetComponent<STTClient>() ?? gameObject.AddComponent<STTClient>();
            
            if (llmClient == null)
                llmClient = GetComponent<LLMClient>() ?? gameObject.AddComponent<LLMClient>();
            
            if (registry == null)
                registry = VoiceCommandRegistry.Instance ?? FindObjectOfType<VoiceCommandRegistry>();
            
            if (executor == null)
                executor = GetComponent<VoiceCommandExecutor>() ?? gameObject.AddComponent<VoiceCommandExecutor>();
        }
        
        private void CreateDefaultSettings()
        {
            if (settings == null)
            {
                settings = VoiceControlSettings.CreateDefault();
            }
        }
        
        private void InitializeComponents()
        {
            // Configure STT client
            if (sttClient != null && settings != null)
            {
                sttClient.SetServerUrl(settings.sttServerUrl);
            }
            
            // Configure LLM client
            if (llmClient != null && settings != null)
            {
                llmClient.SetServerUrl(settings.llmServerUrl);
                if (!string.IsNullOrEmpty(settings.llmApiKey))
                {
                    llmClient.SetApiKey(settings.llmApiKey);
                }
            }
            
            // Get default microphone
            if (string.IsNullOrEmpty(microphoneDeviceName) && Microphone.devices.Length > 0)
            {
                microphoneDeviceName = Microphone.devices[0];
                Log($"Using microphone: {microphoneDeviceName}");
            }
            else if (Microphone.devices.Length == 0)
            {
                Log("Warning: No microphone devices found");
            }
        }
        
        private void UpdateRecording()
        {
            if (_recordingClip == null) return;
            
            int currentPosition = Microphone.GetPosition(microphoneDeviceName);
            
            // Calculate audio level
            if (currentPosition > _lastSamplePosition)
            {
                int sampleCount = Mathf.Min(currentPosition - _lastSamplePosition, _sampleBuffer.Length);
                _recordingClip.GetData(_sampleBuffer, currentPosition - sampleCount);
                
                float sum = 0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    sum += Mathf.Abs(_sampleBuffer[i]);
                }
                CurrentAudioLevel = sum / sampleCount;
                OnAudioLevelUpdate?.Invoke(CurrentAudioLevel);
                
                // Check for silence (auto-stop)
                float threshold = settings != null ? settings.silenceThreshold : 0.02f;
                if (CurrentAudioLevel > threshold)
                {
                    _lastSoundTime = Time.time;
                }
                
                _lastSamplePosition = currentPosition;
            }
            
            // Auto-stop on extended silence
            if (_autoStopEnabled)
            {
                float silenceDuration = settings != null ? settings.silenceDuration : 1.5f;
                float minDuration = settings != null ? settings.minRecordingDuration : 0.5f;
                
                if (Time.time - _recordingStartTime > minDuration && 
                    Time.time - _lastSoundTime > silenceDuration)
                {
                    Log("Auto-stopping due to silence");
                    StopRecording(true);
                }
            }
            
            // Check max duration
            float maxDuration = settings != null ? settings.maxRecordingDuration : 30f;
            if (Time.time - _recordingStartTime > maxDuration)
            {
                Log("Max recording duration reached");
                StopRecording(true);
            }
        }
        
        private void ProcessRecording(int samplePosition)
        {
            SetState(VoiceControlState.ProcessingSTT);
            
            // Extract audio data
            byte[] audioData = ExtractWavData(samplePosition);
            
            if (audioData == null || audioData.Length == 0)
            {
                SetError("Failed to extract audio data");
                SetState(VoiceControlState.Idle);
                return;
            }
            
            Log($"Sending {audioData.Length} bytes to STT server");
            
            // Send to STT
            sttClient.Transcribe(audioData, OnSTTComplete);
        }
        
        private byte[] ExtractWavData(int samplePosition)
        {
            if (_recordingClip == null || samplePosition <= 0)
                return null;
            
            float[] samples = new float[samplePosition];
            _recordingClip.GetData(samples, 0);
            
            // Convert to WAV format
            int sampleRate = _recordingClip.frequency;
            int channels = _recordingClip.channels;
            
            return ConvertToWav(samples, sampleRate, channels);
        }
        
        private byte[] ConvertToWav(float[] samples, int sampleRate, int channels)
        {
            int sampleCount = samples.Length;
            int byteCount = sampleCount * 2; // 16-bit PCM
            
            byte[] wav = new byte[44 + byteCount];
            
            // RIFF header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            BitConverter.GetBytes(36 + byteCount).CopyTo(wav, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
            
            // fmt chunk
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
            BitConverter.GetBytes(16).CopyTo(wav, 16); // chunk size
            BitConverter.GetBytes((short)1).CopyTo(wav, 20); // PCM format
            BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
            BitConverter.GetBytes(sampleRate * channels * 2).CopyTo(wav, 28); // byte rate
            BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32); // block align
            BitConverter.GetBytes((short)16).CopyTo(wav, 34); // bits per sample
            
            // data chunk
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
            BitConverter.GetBytes(byteCount).CopyTo(wav, 40);
            
            // Convert samples
            int offset = 44;
            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                BitConverter.GetBytes(s).CopyTo(wav, offset);
                offset += 2;
            }
            
            return wav;
        }
        
        private void OnSTTComplete(string transcription)
        {
            if (string.IsNullOrEmpty(transcription))
            {
                SetError("No transcription received");
                SetState(VoiceControlState.Idle);
                return;
            }
            
            LastTranscription = transcription;
            OnTranscriptionReceived?.Invoke(transcription);
            
            Log($"Transcription: {transcription}");
            
            // Send to LLM
            SetState(VoiceControlState.ProcessingLLM);
            llmClient.SendVoiceCommand(transcription, OnLLMComplete);
        }
        
        private void OnLLMComplete(LLMResponse response)
        {
            if (response == null || response.HasError)
            {
                SetError(response?.Error ?? "No LLM response received");
                SetState(VoiceControlState.Idle);
                return;
            }
            
            if (!string.IsNullOrEmpty(response.TextContent))
            {
                OnLLMResponseReceived?.Invoke(response.TextContent);
            }
            
            if (response.HasToolCalls)
            {
                SetState(VoiceControlState.Executing);
                
                var results = executor.ExecuteToolCalls(response.ToolCalls);
                
                foreach (var result in results)
                {
                    string cmdDisplay = $"{result.TargetId}.{result.CommandName}";
                    OnCommandExecuted?.Invoke(cmdDisplay, result.Success);
                    Log($"Command {cmdDisplay}: {(result.Success ? "Success" : "Failed")}");
                }
            }
            else
            {
                Log("No tool calls in response");
            }
            
            SetState(VoiceControlState.Idle);
        }
        
        private IEnumerator TestConnectivityCoroutine(Action<bool, string, bool, string> callback)
        {
            bool sttOk = false;
            string sttMessage = "";
            bool llmOk = false;
            string llmMessage = "";
            
            bool sttDone = false;
            bool llmDone = false;
            
            sttClient.TestConnection((ok, msg) => {
                sttOk = ok;
                sttMessage = msg;
                sttDone = true;
            });
            
            llmClient.TestConnection((ok, msg) => {
                llmOk = ok;
                llmMessage = msg;
                llmDone = true;
            });
            
            while (!sttDone || !llmDone)
            {
                yield return null;
            }
            
            callback?.Invoke(sttOk, sttMessage, llmOk, llmMessage);
        }
        
        private void SetState(VoiceControlState newState)
        {
            if (CurrentState != newState)
            {
                CurrentState = newState;
                OnStateChanged?.Invoke(newState);
                Log($"State changed to: {newState}");
            }
        }
        
        private void SetError(string error)
        {
            LastError = error;
            Debug.LogError($"[VoiceControlManager] {error}");
            OnError?.Invoke(error);
        }
        
        private void Log(string message)
        {
            if (verboseLogging || (settings != null && settings.verboseLogging))
            {
                Debug.Log($"[VoiceControlManager] {message}");
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Voice control state machine states
    /// </summary>
    public enum VoiceControlState
    {
        Idle,
        Recording,
        ProcessingSTT,
        ProcessingLLM,
        Executing
    }
}
