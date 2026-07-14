using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RuntimeScriptGenerator : MonoBehaviour
{
    public GameObject targetObject;
    public string userCommand = "make this object bounce";
    public XRNode controllerNode = XRNode.RightHand;
    public Transform headTransform;
    public Transform leftHandTransform;
    public Transform rightHandTransform;
    public bool generateOnInputPress = true;
    public bool useLocalLLM;
    public bool refineSameChannelBehaviors = true;
    public bool fallBackToMockOnLLMError = true;
    public OllamaLuaBehaviorGenerator localLLMGenerator;
    public BehaviorChannel lastBehaviorChannel;

    [TextArea(8, 24)]
    public string lastGeneratedLuaScript;

    private readonly List<XRInputDevice> controllerDevices = new List<XRInputDevice>();
    private bool wasPrimaryButtonPressed;
    private bool isGeneratingScript;

    private void Awake()
    {
        if (localLLMGenerator == null)
        {
            localLLMGenerator = GetComponent<OllamaLuaBehaviorGenerator>();
        }
    }

    private void Update()
    {
        if (generateOnInputPress && WasGenerateInputPressed())
        {
            GenerateAndAttachSpin();
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
        UnityEngine.XR.InputDevices.GetDevicesAtXRNode(controllerNode, controllerDevices);

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

    [ContextMenu("Generate Lua Behavior")]
    public void GenerateAndAttachSpin()
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object assigned.");
            return;
        }

        string command = userCommand;
        BehaviorChannel behaviorChannel = BehaviorChannelClassifier.Classify(command);
        lastBehaviorChannel = behaviorChannel;
        ScriptedLuaBehavior existingBehavior = FindExistingBehaviorForChannel(behaviorChannel);

        if (useLocalLLM)
        {
            if (isGeneratingScript)
            {
                Debug.LogWarning("Already waiting for a local LLM behavior script.");
                return;
            }

            StartCoroutine(GenerateWithLocalLLM(command, behaviorChannel, existingBehavior));
            return;
        }

        string scriptText = MockLuaBehaviorGenerator.Generate(command);
        AttachOrRefineLuaBehavior(scriptText, command, behaviorChannel, existingBehavior);
    }

    private IEnumerator GenerateWithLocalLLM(string command, BehaviorChannel behaviorChannel, ScriptedLuaBehavior existingBehavior)
    {
        if (localLLMGenerator == null)
        {
            Debug.LogWarning("No local LLM generator assigned.");

            if (fallBackToMockOnLLMError)
            {
                AttachOrRefineLuaBehavior(MockLuaBehaviorGenerator.Generate(command), command, behaviorChannel, existingBehavior);
            }

            yield break;
        }

        isGeneratingScript = true;
        string generatedScript = null;
        string error = null;

        string existingCommand = existingBehavior != null ? existingBehavior.sourceCommand : null;
        string existingScript = existingBehavior != null ? existingBehavior.scriptText : null;

        yield return localLLMGenerator.GenerateScript(
            command,
            behaviorChannel,
            existingCommand,
            existingScript,
            script => generatedScript = script,
            message => error = message);

        isGeneratingScript = false;

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Local LLM script generation failed: " + error);

            if (fallBackToMockOnLLMError)
            {
                AttachOrRefineLuaBehavior(MockLuaBehaviorGenerator.Generate(command), command, behaviorChannel, existingBehavior);
            }

            yield break;
        }

        AttachOrRefineLuaBehavior(generatedScript, command, behaviorChannel, existingBehavior);
    }

    private void AttachOrRefineLuaBehavior(string scriptText, string command, BehaviorChannel behaviorChannel, ScriptedLuaBehavior existingBehavior)
    {
        lastGeneratedLuaScript = scriptText;

        if (existingBehavior != null)
        {
            existingBehavior.revisionCount++;
            existingBehavior.behaviorChannel = behaviorChannel;
            existingBehavior.headTransform = headTransform;
            existingBehavior.leftHandTransform = leftHandTransform;
            existingBehavior.rightHandTransform = rightHandTransform;
            existingBehavior.LoadScript(scriptText, CombineCommandHistory(existingBehavior.sourceCommand, command));

            Debug.Log("Refined " + behaviorChannel + " Lua behavior on " + targetObject.name + " from command: " + command);
            return;
        }

        ScriptedLuaBehavior behavior = targetObject.AddComponent<ScriptedLuaBehavior>();
        behavior.behaviorChannel = behaviorChannel;
        behavior.headTransform = headTransform;
        behavior.leftHandTransform = leftHandTransform;
        behavior.rightHandTransform = rightHandTransform;
        behavior.LoadScript(scriptText, command);

        Debug.Log("Attached " + behaviorChannel + " Lua behavior to " + targetObject.name + " from command: " + command);
    }

    private ScriptedLuaBehavior FindExistingBehaviorForChannel(BehaviorChannel behaviorChannel)
    {
        if (!refineSameChannelBehaviors || behaviorChannel == BehaviorChannel.General)
        {
            return null;
        }

        ScriptedLuaBehavior[] behaviors = targetObject.GetComponents<ScriptedLuaBehavior>();

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            if (behaviors[i].behaviorChannel == behaviorChannel)
            {
                return behaviors[i];
            }
        }

        return null;
    }

    private static string CombineCommandHistory(string existingCommand, string newCommand)
    {
        if (string.IsNullOrWhiteSpace(existingCommand))
        {
            return newCommand;
        }

        if (existingCommand.Contains(" + "))
        {
            return existingCommand + " + " + newCommand;
        }

        return existingCommand + " + " + newCommand;
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
            GenerateAndAttachSpin();
        }
    }
}
