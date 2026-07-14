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
    public bool useLocalLLM;
    public bool fallBackToMockOnLLMError = true;
    public OllamaLuaBehaviorGenerator localLLMGenerator;

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
        if (WasGenerateInputPressed())
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

        if (useLocalLLM)
        {
            if (isGeneratingScript)
            {
                Debug.LogWarning("Already waiting for a local LLM behavior script.");
                return;
            }

            StartCoroutine(GenerateWithLocalLLM());
            return;
        }

        string scriptText = MockLuaBehaviorGenerator.Generate(userCommand);
        AttachLuaBehavior(scriptText);
    }

    private IEnumerator GenerateWithLocalLLM()
    {
        if (localLLMGenerator == null)
        {
            Debug.LogWarning("No local LLM generator assigned.");

            if (fallBackToMockOnLLMError)
            {
                AttachLuaBehavior(MockLuaBehaviorGenerator.Generate(userCommand));
            }

            yield break;
        }

        isGeneratingScript = true;
        string generatedScript = null;
        string error = null;

        yield return localLLMGenerator.GenerateScript(userCommand, script => generatedScript = script, message => error = message);

        isGeneratingScript = false;

        if (!string.IsNullOrEmpty(error))
        {
            Debug.LogError("Local LLM script generation failed: " + error);

            if (fallBackToMockOnLLMError)
            {
                AttachLuaBehavior(MockLuaBehaviorGenerator.Generate(userCommand));
            }

            yield break;
        }

        AttachLuaBehavior(generatedScript);
    }

    private void AttachLuaBehavior(string scriptText)
    {
        lastGeneratedLuaScript = scriptText;
        ScriptedLuaBehavior behavior = targetObject.AddComponent<ScriptedLuaBehavior>();
        behavior.headTransform = headTransform;
        behavior.leftHandTransform = leftHandTransform;
        behavior.rightHandTransform = rightHandTransform;
        behavior.LoadScript(scriptText, userCommand);

        Debug.Log("Attached scripted Lua behavior to " + targetObject.name + " from command: " + userCommand);
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
