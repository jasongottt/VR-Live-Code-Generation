using System;
using System.IO;
using System.Linq;
using UnityEngine;

using UnityEditor;
using UnityEditor.Compilation;

public class RuntimeScriptGenerator : MonoBehaviour
{
    public GameObject targetObject;

[ContextMenu("Generate Fake LLM Behavior")]
public void GenerateAndAttachSpin()
{
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

    // This simulates what the user would say/type.
    string userCommand = "make this object bounce";

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