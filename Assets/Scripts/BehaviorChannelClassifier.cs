public static class BehaviorChannelClassifier
{
    public static BehaviorChannel Classify(string command)
    {
        string text = (command ?? string.Empty).ToLowerInvariant();

        if (ContainsAny(text, "follow", "run away", "avoid", "bounce", "float", "hover", "orbit", "circle", "move", "side to side", "sway", "wander", "drift"))
        {
            return BehaviorChannel.Position;
        }

        if (ContainsAny(text, "look at", "face me", "face player", "face hand", "watch me", "stare"))
        {
            return BehaviorChannel.Attention;
        }

        if (ContainsAny(text, "spin", "rotate", "turn around", "twirl"))
        {
            return BehaviorChannel.Rotation;
        }

        if (ContainsAny(text, "grow", "shrink", "scale", "pulse", "breathe"))
        {
            return BehaviorChannel.Scale;
        }

        if (ContainsAny(text, "glow", "color", "red", "green", "blue", "yellow", "purple", "cyan", "white", "transparent", "alpha", "invisible", "visible"))
        {
            return BehaviorChannel.Appearance;
        }

        return BehaviorChannel.General;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (string needle in needles)
        {
            if (text.Contains(needle))
            {
                return true;
            }
        }

        return false;
    }
}
