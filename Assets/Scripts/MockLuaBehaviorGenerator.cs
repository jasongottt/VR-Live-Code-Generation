using System.Globalization;
using System.Text;
using UnityEngine;

public static class MockLuaBehaviorGenerator
{
    public static string Generate(string userCommand)
    {
        string command = (userCommand ?? string.Empty).ToLowerInvariant();
        StringBuilder script = new StringBuilder();

        script.AppendLine("-- Mock Lua generated from command: " + EscapeLuaComment(userCommand));
        script.AppendLine("function start()");
        AppendStartBody(script, command);
        script.AppendLine("end");
        script.AppendLine();
        script.AppendLine("function update(dt)");
        AppendUpdateBody(script, command);
        script.AppendLine("end");

        return script.ToString();
    }

    private static void AppendStartBody(StringBuilder script, string command)
    {
        bool wroteLine = false;

        if (command.Contains("bounce"))
        {
            script.AppendLine("    start = object:getPosition()");
            wroteLine = true;
        }

        Color? color = TryGetColor(command);

        if (color.HasValue)
        {
            AppendSetColor(script, color.Value, "    ");
            wroteLine = true;
        }

        if (command.Contains("glow"))
        {
            Color glowColor = color ?? Color.cyan;
            AppendSetEmission(script, glowColor, 1.5f, "    ");
            wroteLine = true;
        }

        if (!wroteLine)
        {
            script.AppendLine("    log(\"No mapped start action for this command yet.\")");
        }
    }

    private static void AppendUpdateBody(StringBuilder script, string command)
    {
        bool wroteLine = false;

        if (command.Contains("bounce"))
        {
            script.AppendLine("    local y = start.y + math.sin(time * 3.0) * 0.5");
            script.AppendLine("    object:setPosition(start.x, y, start.z)");
            wroteLine = true;
        }

        if (command.Contains("spin") || command.Contains("rotate"))
        {
            script.AppendLine("    object:rotate(0, 90 * dt, 0)");
            wroteLine = true;
        }

        if (command.Contains("pulse"))
        {
            script.AppendLine("    local scale = 1.0 + math.sin(time * 4.0) * 0.15");
            script.AppendLine("    object:setScale(scale, scale, scale)");
            wroteLine = true;
        }

        if (!wroteLine)
        {
            script.AppendLine("    -- no mapped update action");
        }
    }

    private static Color? TryGetColor(string command)
    {
        if (command.Contains("red"))
        {
            return Color.red;
        }

        if (command.Contains("green"))
        {
            return Color.green;
        }

        if (command.Contains("blue"))
        {
            return Color.blue;
        }

        if (command.Contains("yellow"))
        {
            return Color.yellow;
        }

        if (command.Contains("purple"))
        {
            return new Color(0.6f, 0.2f, 1f, 1f);
        }

        if (command.Contains("cyan"))
        {
            return Color.cyan;
        }

        if (command.Contains("white"))
        {
            return Color.white;
        }

        return null;
    }

    private static void AppendSetColor(StringBuilder script, Color color, string indent)
    {
        script.AppendLine(indent + "object:setColor(" + Format(color.r) + ", " + Format(color.g) + ", " + Format(color.b) + ", " + Format(color.a) + ")");
    }

    private static void AppendSetEmission(StringBuilder script, Color color, float intensity, string indent)
    {
        script.AppendLine(indent + "object:setEmission(" + Format(color.r) + ", " + Format(color.g) + ", " + Format(color.b) + ", " + Format(intensity) + ")");
    }

    private static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string EscapeLuaComment(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", " ").Replace("\n", " ");
    }
}
