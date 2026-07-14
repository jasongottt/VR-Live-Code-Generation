using System;
using System.Text.RegularExpressions;
using MoonSharp.Interpreter;

public static class LuaScriptValidator
{
    private static readonly string[] ForbiddenTokens =
    {
        "require",
        "io",
        "os",
        "debug",
        "load",
        "loadstring",
        "dofile",
        "collectgarbage",
        "package",
        "coroutine",
        "setmetatable",
        "getmetatable",
        "rawget",
        "rawset"
    };

    public static bool IsSafe(string scriptText, out string error)
    {
        if (string.IsNullOrWhiteSpace(scriptText))
        {
            error = "Generated Lua script was empty.";
            return false;
        }

        string lowerScript = scriptText.ToLowerInvariant();

        foreach (string token in ForbiddenTokens)
        {
            if (Regex.IsMatch(lowerScript, @"\b" + Regex.Escape(token) + @"\b"))
            {
                error = "Generated Lua script used forbidden token: " + token;
                return false;
            }
        }

        if (Regex.IsMatch(lowerScript, @"\bwhile\b"))
        {
            error = "Generated Lua script used while; update(dt) should express frame behavior without loops.";
            return false;
        }

        try
        {
            Script script = new Script(CoreModules.Preset_SoftSandbox);
            script.LoadString(scriptText);
        }
        catch (Exception exception)
        {
            error = "Generated Lua script did not compile: " + exception.Message;
            return false;
        }

        error = null;
        return true;
    }
}
