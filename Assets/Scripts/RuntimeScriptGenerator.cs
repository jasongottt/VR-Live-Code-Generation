using System;
using System.IO;
using System.Linq;
using UnityEngine;

using UnityEditor;
using UnityEditor.Compilation;

public class RuntimeScriptGenerator : MonoBehaviour
{
    public GameObject targetObject;

    [ContextMenu("Generate And Attach Spin")]
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

        string scriptPath = Path.Combine(folderPath, "GeneratedBehavior.cs");

        string code = @"
using UnityEngine;
//hello
public class GeneratedBehavior : MonoBehaviour
{
void Start()
{
}

void Update()
{
    float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.25f;
    transform.localScale = new Vector3(pulse, pulse, pulse);
}

}
";

        File.WriteAllText(scriptPath, code);

        // Save which object should receive the generated script after Unity recompiles.
        string targetId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject).ToString();
        EditorPrefs.SetString("PendingGeneratedBehaviorTarget", targetId);
        EditorPrefs.SetString("PendingGeneratedBehaviorType", "GeneratedBehavior");

        Debug.Log("Generated script. Waiting for Unity to compile...");

        AssetDatabase.ImportAsset(scriptPath);
        AssetDatabase.Refresh();

        // If the type already exists, attach immediately.
        // If this is the first time generating it, Unity will attach after compile.
        GeneratedBehaviorPostCompileAttacher.TryAttachPendingBehavior();
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