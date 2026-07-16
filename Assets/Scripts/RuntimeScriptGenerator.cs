using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RuntimeScriptGenerator : MonoBehaviour
{
    public GameObject targetObject;

    [TextArea(2, 6)]
    public string userCommand;

    public XRNode controllerNode = XRNode.RightHand;
    public Transform headTransform;
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public bool generateOnInputPress = true;
    public bool refineSameChannelBehaviors = true;
    public OllamaLuaBehaviorGenerator localLLMGenerator;
    public BehaviorManager behaviorManager;
    public BehaviorAction lastBehaviorAction;
    public BehaviorChannel lastBehaviorChannel;

    [TextArea(2, 6)]
    public string lastDecisionStatus;

    [TextArea(8, 24)]
    public string lastGeneratedLuaScript;

    private readonly List<XRInputDevice> controllerDevices = new List<XRInputDevice>();
    private bool wasPrimaryButtonPressed;
    private bool isGeneratingDecision;
    private int generationVersion;

    private void Awake()
    {
        if (localLLMGenerator == null)
        {
            localLLMGenerator = GetComponent<OllamaLuaBehaviorGenerator>();
        }

        EnsureBehaviorManager();
    }

    private void Update()
    {
        if (generateOnInputPress && WasGenerateInputPressed())
        {
            GenerateAndApplyBehavior();
        }
    }

    private bool WasGenerateInputPressed()
    {
        return WasSpacebarPressed() || WasControllerPrimaryButtonPressed();
    }

    private bool WasSpacebarPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space))
        {
            return true;
        }
#endif

        return false;
    }

    private bool WasControllerPrimaryButtonPressed()
    {
        bool isPressed = false;

        controllerDevices.Clear();
        InputDevices.GetDevicesAtXRNode(controllerNode, controllerDevices);

        foreach (XRInputDevice device in controllerDevices)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed) && pressed)
            {
                isPressed = true;
                break;
            }
        }

        bool wasPressedThisFrame = isPressed && !wasPrimaryButtonPressed;
        wasPrimaryButtonPressed = isPressed;
        return wasPressedThisFrame;
    }

    [ContextMenu("Generate LLM Behavior Decision")]
    public void GenerateAndApplyBehavior()
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object assigned.");
            return;
        }

        if (string.IsNullOrWhiteSpace(userCommand))
        {
            Debug.LogWarning("Ignored empty runtime command.");
            return;
        }

        if (localLLMGenerator == null)
        {
            Debug.LogError("No OllamaLuaBehaviorGenerator assigned. All behavior decisions require the LLM.");
            return;
        }

        if (isGeneratingDecision)
        {
            Debug.LogWarning("Already waiting for a local LLM behavior decision.");
            return;
        }

        EnsureBehaviorManager();
        ScriptedLuaBehavior[] activeBehaviors = behaviorManager.GetActiveBehaviors(targetObject);
        int requestVersion = ++generationVersion;
        isGeneratingDecision = true;
        StartCoroutine(GenerateWithLocalLLM(userCommand.Trim(), activeBehaviors, requestVersion));
    }

    private IEnumerator GenerateWithLocalLLM(
        string command,
        ScriptedLuaBehavior[] activeBehaviors,
        int requestVersion)
    {
        BehaviorDecision decision = null;
        string error = null;

        yield return localLLMGenerator.GenerateDecision(
            command,
            activeBehaviors,
            result => decision = result,
            message => error = message);

        isGeneratingDecision = false;

        if (requestVersion != generationVersion)
        {
            Debug.Log("Discarded an outdated local LLM behavior decision.");
            yield break;
        }

        if (!string.IsNullOrEmpty(error))
        {
            lastDecisionStatus = "LLM decision failed: " + error;
            Debug.LogError(lastDecisionStatus);
            yield break;
        }

        ApplyDecision(decision, command);
    }

    private void ApplyDecision(BehaviorDecision decision, string command)
    {
        if (decision == null)
        {
            lastDecisionStatus = "The LLM returned no behavior decision.";
            Debug.LogError(lastDecisionStatus);
            return;
        }

        lastBehaviorAction = decision.action;
        lastBehaviorChannel = decision.channel;

        if (decision.action != BehaviorAction.Apply)
        {
            behaviorManager.ExecuteManagementDecision(decision, targetObject);
            lastDecisionStatus = behaviorManager.lastStatus;
            return;
        }

        if (!behaviorManager.TryApplyBehavior(
                targetObject,
                decision,
                command,
                refineSameChannelBehaviors,
                headTransform,
                leftHandTransform,
                rightHandTransform,
                out ScriptedLuaBehavior behavior,
                out string error))
        {
            lastDecisionStatus = error;
            Debug.LogError(error);
            return;
        }

        lastGeneratedLuaScript = decision.scriptText;
        lastDecisionStatus = behaviorManager.lastStatus;
        Debug.Log(lastDecisionStatus + " Target: " + behavior.name + ". Command: " + command);
    }

    private void EnsureBehaviorManager()
    {
        if (behaviorManager == null)
        {
            behaviorManager = GetComponent<BehaviorManager>();
        }

        if (behaviorManager == null)
        {
            behaviorManager = gameObject.AddComponent<BehaviorManager>();
        }
    }

    [ContextMenu("Undo Last Behavior Change")]
    public void UndoLastBehaviorChange()
    {
        EnsureBehaviorManager();
        generationVersion++;
        behaviorManager.UndoLastChange(targetObject);
        lastDecisionStatus = behaviorManager.lastStatus;
    }

    [ContextMenu("Clear All Behaviors")]
    public void ClearAllBehaviors()
    {
        EnsureBehaviorManager();
        generationVersion++;
        behaviorManager.ClearAll(targetObject);
        lastDecisionStatus = behaviorManager.lastStatus;
    }

    public void SubmitCommand(string command, bool generateImmediately = true)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            Debug.LogWarning("Ignored empty runtime command.");
            return;
        }

        userCommand = command.Trim();

        if (generateImmediately)
        {
            GenerateAndApplyBehavior();
        }
    }
}
