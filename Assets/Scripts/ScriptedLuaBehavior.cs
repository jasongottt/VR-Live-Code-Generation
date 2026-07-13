using System;
using MoonSharp.Interpreter;
using UnityEngine;

public class ScriptedLuaBehavior : MonoBehaviour
{
    [TextArea(8, 24)]
    public string scriptText;

    public string sourceCommand;

    private Script script;
    private DynValue startFunction;
    private DynValue updateFunction;
    private bool hasStartedScript;
    private static bool registeredUserData;

    private void Awake()
    {
        RegisterUserData();
    }

    private void Start()
    {
        if (!hasStartedScript && !string.IsNullOrWhiteSpace(scriptText))
        {
            CompileAndStart();
        }
    }

    private void Update()
    {
        if (script == null || updateFunction == null || updateFunction.Type == DataType.Nil)
        {
            return;
        }

        try
        {
            script.Globals["time"] = Time.time;
            script.Globals["dt"] = Time.deltaTime;
            script.Call(updateFunction, Time.deltaTime);
        }
        catch (Exception exception)
        {
            DisableAfterError("Lua update failed", exception);
        }
    }

    public void LoadScript(string newScriptText, string command)
    {
        scriptText = newScriptText;
        sourceCommand = command;
        CompileAndStart();
    }

    private void CompileAndStart()
    {
        try
        {
            RegisterUserData();

            script = new Script(CoreModules.Preset_SoftSandbox);
            script.Options.DebugPrint = message => Debug.Log("[Lua] " + message);
            script.Globals["object"] = UserData.Create(new LuaObjectApi(gameObject));
            script.Globals["time"] = Time.time;
            script.Globals["dt"] = Time.deltaTime;
            script.Globals["log"] = (Action<string>)LogFromLua;

            script.DoString(scriptText);

            startFunction = script.Globals.Get("start");
            updateFunction = script.Globals.Get("update");
            hasStartedScript = true;

            if (startFunction != null && startFunction.Type != DataType.Nil)
            {
                script.Call(startFunction);
            }
        }
        catch (Exception exception)
        {
            DisableAfterError("Lua script failed to compile or start", exception);
        }
    }

    private void LogFromLua(string message)
    {
        Debug.Log("[Lua] " + message);
    }

    private void DisableAfterError(string message, Exception exception)
    {
        Debug.LogError(message + " on " + name + ": " + exception.Message);
        enabled = false;
    }

    private static void RegisterUserData()
    {
        if (registeredUserData)
        {
            return;
        }

        UserData.RegisterType<LuaObjectApi>();
        UserData.RegisterType<LuaVector3>();
        registeredUserData = true;
    }
}
