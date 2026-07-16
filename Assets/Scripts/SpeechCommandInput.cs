using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

public class SpeechCommandInput : MonoBehaviour
{
    public RuntimeScriptGenerator runtimeScriptGenerator;
    public TMP_Text lastMessageText;
    public string lastMessagePrefix = "Last heard: ";
    public bool listenWhileInputHeld = true;
    public bool startListeningOnEnable;
    public bool disableGeneratorInputTrigger = true;
    public bool generateImmediately = true;
    public bool restartAfterDictationStops = true;
    public float restartDelaySeconds = 0.25f;
    public XRNode controllerNode = XRNode.RightHand;

    [TextArea(2, 6)]
    public string lastRecognizedText;

    private readonly List<XRInputDevice> controllerDevices = new List<XRInputDevice>();
    private bool wasPushToTalkHeld;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private DictationRecognizer dictationRecognizer;
    private float restartAtTime = -1f;
#endif

    private void Reset()
    {
        runtimeScriptGenerator = GetComponent<RuntimeScriptGenerator>();
        FindLastMessageText();
    }

    private void Awake()
    {
        if (runtimeScriptGenerator == null)
        {
            runtimeScriptGenerator = GetComponent<RuntimeScriptGenerator>();
        }

        FindLastMessageText();

        if (runtimeScriptGenerator != null)
        {
            controllerNode = runtimeScriptGenerator.controllerNode;

            if (disableGeneratorInputTrigger)
            {
                runtimeScriptGenerator.generateOnInputPress = false;
            }
        }
    }

    private void OnEnable()
    {
        if (!listenWhileInputHeld && startListeningOnEnable)
        {
            StartListening();
        }
    }

    private void OnDisable()
    {
        StopListening();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        DisposeRecognizer();
#endif
    }

    private void Update()
    {
        if (listenWhileInputHeld)
        {
            UpdatePushToTalk();
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (restartAtTime > 0f && Time.unscaledTime >= restartAtTime && ShouldBeListening())
        {
            restartAtTime = -1f;
            StartListening();
        }
#endif
    }

    [ContextMenu("Start Listening")]
    public void StartListening()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        EnsureRecognizer();

        if (dictationRecognizer.Status == SpeechSystemStatus.Stopped)
        {
            dictationRecognizer.Start();
            Debug.Log("Speech command input listening.");
        }
#else
        Debug.LogWarning("SpeechCommandInput uses UnityEngine.Windows.Speech and only listens on Windows Editor/Standalone. Use SubmitRecognizedText from a Quest/cloud STT backend on this platform.");
#endif
    }

    [ContextMenu("Stop Listening")]
    public void StopListening()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        restartAtTime = -1f;
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (dictationRecognizer == null)
        {
            return;
        }

        if (dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            dictationRecognizer.Stop();
        }
#endif
    }

    private void UpdatePushToTalk()
    {
        bool isHeld = IsPushToTalkHeld();

        if (isHeld && !wasPushToTalkHeld)
        {
            StartListening();
        }
        else if (!isHeld && wasPushToTalkHeld)
        {
            StopListening();
        }

        wasPushToTalkHeld = isHeld;
    }

    private bool ShouldBeListening()
    {
        return !listenWhileInputHeld || IsPushToTalkHeld();
    }

    private bool IsPushToTalkHeld()
    {
        return IsSpacebarHeld() || IsControllerPrimaryButtonHeld();
    }

    private bool IsSpacebarHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.Space))
        {
            return true;
        }
#endif

        return false;
    }

    private bool IsControllerPrimaryButtonHeld()
    {
        controllerDevices.Clear();
        InputDevices.GetDevicesAtXRNode(controllerNode, controllerDevices);

        foreach (XRInputDevice device in controllerDevices)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed)
            {
                return true;
            }
        }

        return false;
    }

    public void SubmitRecognizedText(string recognizedText)
    {
        if (string.IsNullOrWhiteSpace(recognizedText))
        {
            return;
        }

        lastRecognizedText = recognizedText.Trim();
        UpdateLastMessageText();
        Debug.Log("Voice command recognized: " + lastRecognizedText);

        if (runtimeScriptGenerator == null)
        {
            Debug.LogError("No RuntimeScriptGenerator assigned for speech command input.");
            return;
        }

        runtimeScriptGenerator.SubmitCommand(lastRecognizedText, generateImmediately);
    }

    private void FindLastMessageText()
    {
        if (lastMessageText != null)
        {
            return;
        }

        GameObject lastMessageObject = GameObject.Find("lastmessage");

        if (lastMessageObject == null)
        {
            lastMessageObject = GameObject.Find("LastMessage");
        }

        if (lastMessageObject != null)
        {
            lastMessageText = lastMessageObject.GetComponent<TMP_Text>();
        }
    }

    private void UpdateLastMessageText()
    {
        FindLastMessageText();

        if (lastMessageText != null)
        {
            lastMessageText.text = lastMessagePrefix + lastRecognizedText;
        }
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private void EnsureRecognizer()
    {
        if (dictationRecognizer != null)
        {
            return;
        }

        dictationRecognizer = new DictationRecognizer();
        dictationRecognizer.DictationResult += OnDictationResult;
        dictationRecognizer.DictationHypothesis += OnDictationHypothesis;
        dictationRecognizer.DictationComplete += OnDictationComplete;
        dictationRecognizer.DictationError += OnDictationError;
    }

    private void OnDictationResult(string text, ConfidenceLevel confidence)
    {
        if (confidence == ConfidenceLevel.Rejected)
        {
            Debug.LogWarning("Rejected voice command: " + text);
            return;
        }

        SubmitRecognizedText(text);
    }

    private void OnDictationHypothesis(string text)
    {
        lastRecognizedText = text;
    }

    private void OnDictationComplete(DictationCompletionCause cause)
    {
        if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.TimeoutExceeded)
        {
            Debug.LogWarning("Speech command dictation stopped: " + cause);
        }

        if (restartAfterDictationStops && isActiveAndEnabled && ShouldBeListening())
        {
            restartAtTime = Time.unscaledTime + Mathf.Max(0f, restartDelaySeconds);
        }
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Speech command dictation error: " + error + " (" + hresult + ")");

        if (restartAfterDictationStops && isActiveAndEnabled && ShouldBeListening())
        {
            restartAtTime = Time.unscaledTime + Mathf.Max(0f, restartDelaySeconds);
        }
    }

    private void DisposeRecognizer()
    {
        if (dictationRecognizer == null)
        {
            return;
        }

        if (dictationRecognizer.Status == SpeechSystemStatus.Running)
        {
            dictationRecognizer.Stop();
        }

        dictationRecognizer.DictationResult -= OnDictationResult;
        dictationRecognizer.DictationHypothesis -= OnDictationHypothesis;
        dictationRecognizer.DictationComplete -= OnDictationComplete;
        dictationRecognizer.DictationError -= OnDictationError;
        dictationRecognizer.Dispose();
        dictationRecognizer = null;
    }
#endif
}
