using System;

public enum BehaviorAction
{
    Apply,
    Undo,
    ClearChannel,
    ClearAll
}

[Serializable]
public sealed class BehaviorDecision
{
    public BehaviorAction action;
    public BehaviorChannel channel;
    public string scriptText;
}
