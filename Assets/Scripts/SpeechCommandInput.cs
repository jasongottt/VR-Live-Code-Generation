using UnityEngine;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using UnityEngine.Windows.Speech;
#endif

public class SpeechCommandInput : MonoBehaviour
{
    public RuntimeScriptGenerator runtimeScriptGenerator;
    public bool startListeningOnEnable = true;
    public bool generateImmediately = true;
    public bool restartAfterDictationStops = true;
    public float restartDelaySeconds = 0.25f;

    [TextArea(2, 6)]
    public string lastRecognizedText;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private DictationRecognizer dictationRecognizer;
    private float restartAtTime = -1f;
#endif

    private void Reset()
    {
        runtimeScriptGenerator = GetComponent<RuntimeScriptGenerator>();
    }

    private void Awake()
    {
        if (runtimeScriptGenerator == null)
        {
            runtimeScriptGenerator = GetComponent<RuntimeScriptGenerator>();
        }
    }

    private void OnEnable()
    {
        if (startListeningOnEnable)
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
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (restartAtTime > 0f && Time.unscaledTime >= restartAtTime)
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

    public void SubmitRecognizedText(string recognizedText)
    {
        if (string.IsNullOrWhiteSpace(recognizedText))
        {
            return;
        }

        lastRecognizedText = recognizedText.Trim();
        Debug.Log("Voice command recognized: " + lastRecognizedText);

        if (runtimeScriptGenerator == null)
        {
            Debug.LogError("No RuntimeScriptGenerator assigned for speech command input.");
            return;
        }

        runtimeScriptGenerator.SubmitCommand(lastRecognizedText, generateImmediately);
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

        if (restartAfterDictationStops && isActiveAndEnabled)
        {
            restartAtTime = Time.unscaledTime + Mathf.Max(0f, restartDelaySeconds);
        }
    }

    private void OnDictationError(string error, int hresult)
    {
        Debug.LogError("Speech command dictation error: " + error + " (" + hresult + ")");

        if (restartAfterDictationStops && isActiveAndEnabled)
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
