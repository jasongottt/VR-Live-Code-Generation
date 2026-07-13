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

        if (UsesVrContext(command))
        {
            script.AppendLine("    log(\"Using live VR head/controller context.\")");
            wroteLine = true;
        }

        Color? color = TryGetColor(command);

        if (color.HasValue && !UsesConditionalColor(command))
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

        if (command.Contains("follow"))
        {
            script.AppendLine("    if rightHand:isTracked() then");
            script.AppendLine("        local hand = rightHand:getPosition()");
            script.AppendLine("        object:moveToward(hand.x, hand.y, hand.z, 1.25, dt)");
            script.AppendLine("    end");
            wroteLine = true;
        }

        if (command.Contains("run away") || command.Contains("avoid"))
        {
            script.AppendLine("    if rightHand:isTracked() then");
            script.AppendLine("        local hand = rightHand:getPosition()");
            script.AppendLine("        local pos = object:getPosition()");
            script.AppendLine("        local d = distance(hand, pos)");
            script.AppendLine("        if d < 1.0 then");
            script.AppendLine("            local away = direction(hand, pos)");
            script.AppendLine("            object:translate(away.x * 1.5 * dt, away.y * 1.5 * dt, away.z * 1.5 * dt)");
            script.AppendLine("        end");
            script.AppendLine("    end");
            wroteLine = true;
        }

        if (command.Contains("look at me") || command.Contains("look at player") || command.Contains("look at head"))
        {
            script.AppendLine("    local head = player:getHeadPosition()");
            script.AppendLine("    object:lookAt(head.x, head.y, head.z)");
            wroteLine = true;
        }

        if (command.Contains("orbit") || command.Contains("circle me") || command.Contains("circle around me"))
        {
            script.AppendLine("    local head = player:getHeadPosition()");
            script.AppendLine("    local radius = 1.25");
            script.AppendLine("    local orbitSpeed = 1.5");
            script.AppendLine("    object:setPosition(head.x + math.cos(time * orbitSpeed) * radius, head.y, head.z + math.sin(time * orbitSpeed) * radius)");
            script.AppendLine("    object:lookAt(head.x, head.y, head.z)");
            wroteLine = true;
        }

        Color? color = TryGetColor(command);

        if (color.HasValue && UsesConditionalColor(command))
        {
            script.AppendLine("    local head = player:getHeadPosition()");
            script.AppendLine("    local pos = object:getPosition()");
            script.AppendLine("    if distance(head, pos) < 1.25 then");
            AppendSetColor(script, color.Value, "        ");
            script.AppendLine("    else");
            script.AppendLine("        object:setColor(1, 1, 1, 1)");
            script.AppendLine("    end");
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

    private static bool UsesVrContext(string command)
    {
        return command.Contains("follow")
            || command.Contains("run away")
            || command.Contains("avoid")
            || command.Contains("look at")
            || command.Contains("orbit")
            || command.Contains("circle me")
            || command.Contains("circle around me")
            || UsesConditionalColor(command);
    }

    private static bool UsesConditionalColor(string command)
    {
        return command.Contains("close") || command.Contains("near");
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
