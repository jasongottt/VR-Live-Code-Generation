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
    public string userCommand = "make this object bounce";
    public XRNode controllerNode = XRNode.RightHand;

    private readonly List<XRInputDevice> controllerDevices = new List<XRInputDevice>();
    private bool wasPrimaryButtonPressed;

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

    [ContextMenu("Generate Mock Lua Behavior")]
    public void GenerateAndAttachSpin()
    {
        if (targetObject == null)
        {
            Debug.LogError("No target object assigned.");
            return;
        }

        string scriptText = MockLuaBehaviorGenerator.Generate(userCommand);
        ScriptedLuaBehavior behavior = targetObject.AddComponent<ScriptedLuaBehavior>();
        behavior.LoadScript(scriptText, userCommand);

        Debug.Log("Attached scripted Lua behavior to " + targetObject.name + " from command: " + userCommand);
    }
}
