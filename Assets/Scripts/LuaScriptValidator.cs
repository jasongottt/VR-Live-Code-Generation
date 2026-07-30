using System;
using System.Collections.Generic;
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

    private static readonly Dictionary<string, HashSet<string>> AllowedApiMethods =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            {
                "object",
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "getPosition",
                    "getForward",
                    "getRotationEuler",
                    "setPosition",
                    "translate",
                    "rotate",
                    "setRotationEuler",
                    "lookAt",
                    "moveToward",
                    "getScale",
                    "setScale",
                    "getColor",
                    "setColor",
                    "getEmission",
                    "setEmission",
                    "isVisible",
                    "setVisible"
                }
            },
            {
                "player",
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "getHeadPosition",
                    "getPosition",
                    "getHeadForward",
                    "getForward",
                    "isTracked"
                }
            },
            {
                "world",
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "getHeadPosition",
                    "getPosition",
                    "getHeadForward",
                    "getForward",
                    "isTracked"
                }
            },
            {
                "leftHand",
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "getPosition",
                    "getForward",
                    "getRotationEuler",
                    "getVelocity",
                    "getAngularVelocity",
                    "getGrip",
                    "getTrigger",
                    "isPrimaryPressed",
                    "isSecondaryPressed",
                    "isTracked"
                }
            },
            {
                "rightHand",
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "getPosition",
                    "getForward",
                    "getRotationEuler",
                    "getVelocity",
                    "getAngularVelocity",
                    "getGrip",
                    "getTrigger",
                    "isPrimaryPressed",
                    "isSecondaryPressed",
                    "isTracked"
                }
            }
        };

    private static readonly Regex ApiMethodCallPattern = new Regex(
        @"\b(object|player|world|leftHand|rightHand)\s*[:.]\s*([A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.CultureInvariant);

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

        if (!UsesOnlyAllowedApiMethods(scriptText, out error))
        {
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

    private static bool UsesOnlyAllowedApiMethods(string scriptText, out string error)
    {
        MatchCollection calls = ApiMethodCallPattern.Matches(scriptText);

        foreach (Match call in calls)
        {
            string receiver = call.Groups[1].Value;
            string method = call.Groups[2].Value;
            HashSet<string> allowedMethods = AllowedApiMethods[receiver];

            if (allowedMethods.Contains(method))
            {
                continue;
            }

            error =
                "Generated Lua script called unsupported API method " +
                receiver + ":" + method + "(). Allowed " + receiver +
                " methods: " + string.Join(", ", allowedMethods) + ".";
            return false;
        }

        error = null;
        return true;
    }
}
