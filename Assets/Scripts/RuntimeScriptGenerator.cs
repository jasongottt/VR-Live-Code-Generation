using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
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

    [ContextMenu("Generate Fake LLM Behavior")]
    public void GenerateAndAttachSpin()
    {
#if !UNITY_EDITOR
        Debug.LogError("Runtime script generation uses UnityEditor APIs and only works in the Unity Editor.");
        return;
#else
        if (targetObject == null)
        {
            Debug.LogError("No target object assigned.");
            return;
        }

        string folderPath = "Assets/GeneratedBehaviors";

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // Unique class name every time.
        string className = "GeneratedBehavior_" + DateTime.Now.Ticks;

        // File name must match class name.
        string scriptPath = Path.Combine(folderPath, className + ".cs");

        // This is where the LLM-generated code would go.
        string code = FakeLLMGenerateCode(userCommand, className);

        File.WriteAllText(scriptPath, code);

        string targetId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject).ToString();
        EditorPrefs.SetString("PendingGeneratedBehaviorTarget", targetId);
        EditorPrefs.SetString("PendingGeneratedBehaviorType", className);

        Debug.Log("Generated script: " + className + ". Waiting for Unity to compile...");

        AssetDatabase.ImportAsset(scriptPath);
        AssetDatabase.Refresh();

        GeneratedBehaviorPostCompileAttacher.TryAttachPendingBehavior();
#endif
    }

    private string FakeLLMGenerateCode(string userCommand, string className)
    {
        if (userCommand.ToLower().Contains("bounce"))
        {
            return @"
using UnityEngine;

public class " + className + @" : MonoBehaviour
{
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float yOffset = Mathf.Sin(Time.time * 3f) * 0.5f;
        transform.position = startPosition + new Vector3(0f, yOffset, 0f);
    }
}
";
        }

        if (userCommand.ToLower().Contains("spin"))
        {
            return @"
using UnityEngine;

public class " + className + @" : MonoBehaviour
{
    void Update()
    {
        transform.Rotate(Vector3.up * 90f * Time.deltaTime);
    }
}
";
        }

        return @"
using UnityEngine;

public class " + className + @" : MonoBehaviour
{
    void Update()
    {
    }
}
";
    }
}

#if UNITY_EDITOR
[InitializeOnLoad]
public static class GeneratedBehaviorPostCompileAttacher
{
    static GeneratedBehaviorPostCompileAttacher()
    {
        CompilationPipeline.compilationFinished += OnCompilationFinished;

        // Also try after editor reloads.
        EditorApplication.delayCall += TryAttachPendingBehavior;
    }

    private static void OnCompilationFinished(object obj)
    {
        EditorApplication.delayCall += TryAttachPendingBehavior;
    }

    public static void TryAttachPendingBehavior()
    {
        string targetId = EditorPrefs.GetString("PendingGeneratedBehaviorTarget", "");
        string typeName = EditorPrefs.GetString("PendingGeneratedBehaviorType", "");

        if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(typeName))
        {
            return;
        }

        Type generatedType = AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .FirstOrDefault(type => type.Name == typeName);

        if (generatedType == null)
        {
            Debug.Log("Generated behavior type not found yet. Waiting for compile: " + typeName);
            return;
        }

        GlobalObjectId globalObjectId;

        if (!GlobalObjectId.TryParse(targetId, out globalObjectId))
        {
            Debug.LogError("Could not parse saved target object ID.");
            ClearPending();
            return;
        }

        UnityEngine.Object targetObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
        GameObject targetGameObject = targetObj as GameObject;

        if (targetGameObject == null)
        {
            Debug.LogError("Could not find target GameObject after compile.");
            ClearPending();
            return;
        }

        if (targetGameObject.GetComponent(generatedType) == null)
        {
            targetGameObject.AddComponent(generatedType);
            Debug.Log("Attached " + typeName + " to " + targetGameObject.name);
        }
        else
        {
            Debug.Log(targetGameObject.name + " already has " + typeName);
        }

        ClearPending();
    }

    private static void ClearPending()
    {
        EditorPrefs.DeleteKey("PendingGeneratedBehaviorTarget");
        EditorPrefs.DeleteKey("PendingGeneratedBehaviorType");
    }
}
#endif
