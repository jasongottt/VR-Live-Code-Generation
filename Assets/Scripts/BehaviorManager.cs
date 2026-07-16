using System.Collections.Generic;
using UnityEngine;

public sealed class BehaviorManager : MonoBehaviour
{
    [Min(1)]
    public int maxHistoryEntries = 20;

    [TextArea(2, 4)]
    public string lastStatus;

    private readonly List<BehaviorChange> history = new List<BehaviorChange>();
    private readonly Dictionary<ScriptedLuaBehavior, ObjectState> baselines =
        new Dictionary<ScriptedLuaBehavior, ObjectState>();

    public ScriptedLuaBehavior[] GetActiveBehaviors(GameObject target)
    {
        if (target == null)
        {
            return new ScriptedLuaBehavior[0];
        }

        ScriptedLuaBehavior[] behaviors = target.GetComponents<ScriptedLuaBehavior>();
        List<ScriptedLuaBehavior> activeBehaviors = new List<ScriptedLuaBehavior>();

        foreach (ScriptedLuaBehavior behavior in behaviors)
        {
            if (behavior.enabled)
            {
                activeBehaviors.Add(behavior);
            }
        }

        return activeBehaviors.ToArray();
    }

    public bool ExecuteManagementDecision(BehaviorDecision decision, GameObject target)
    {
        if (decision == null)
        {
            lastStatus = "The LLM returned no behavior decision.";
            return false;
        }

        switch (decision.action)
        {
            case BehaviorAction.Undo:
                return UndoLastChange(target);
            case BehaviorAction.ClearChannel:
                ClearChannel(target, decision.channel);
                return true;
            case BehaviorAction.ClearAll:
                ClearAll(target);
                return true;
            default:
                lastStatus = "Apply decisions must include a Lua script.";
                return false;
        }
    }

    public bool TryApplyBehavior(
        GameObject target,
        BehaviorDecision decision,
        string command,
        bool refineSameChannel,
        Transform headTransform,
        Transform leftHandTransform,
        Transform rightHandTransform,
        out ScriptedLuaBehavior appliedBehavior,
        out string error)
    {
        appliedBehavior = null;

        if (target == null)
        {
            error = "No target object assigned.";
            return false;
        }

        if (decision == null || decision.action != BehaviorAction.Apply)
        {
            error = "Expected an Apply behavior decision.";
            return false;
        }

        if (!LuaScriptValidator.IsSafe(decision.scriptText, out error))
        {
            return false;
        }

        ScriptedLuaBehavior existing = FindActiveBehavior(target, decision.channel, refineSameChannel);

        if (existing != null)
        {
            ObjectState stateBeforeChange = ObjectState.Capture(target);
            BehaviorChange change = BehaviorChange.CreateRefinement(existing, stateBeforeChange);
            string combinedCommand = CombineCommandHistory(existing.sourceCommand, command);

            ConfigureBehavior(existing, decision.channel, headTransform, leftHandTransform, rightHandTransform);
            existing.revisionCount++;
            existing.LoadScript(decision.scriptText, combinedCommand);

            if (!existing.enabled)
            {
                stateBeforeChange.RestoreAll();
                RestoreRefinement(change);
                error = "The generated replacement failed to initialize. The previous behavior was restored.";
                return false;
            }

            RecordChange(change);
            appliedBehavior = existing;
            lastStatus = "Refined " + decision.channel + " behavior.";
            error = null;
            return true;
        }

        ObjectState baseline = ObjectState.Capture(target);
        ScriptedLuaBehavior behavior = target.AddComponent<ScriptedLuaBehavior>();
        ConfigureBehavior(behavior, decision.channel, headTransform, leftHandTransform, rightHandTransform);
        behavior.LoadScript(decision.scriptText, command);

        if (!behavior.enabled)
        {
            baseline.RestoreAll();
            DestroyBehavior(behavior);
            error = "The generated behavior failed to initialize and was not added.";
            return false;
        }

        baselines[behavior] = baseline;
        RecordChange(BehaviorChange.CreateAddition(target, behavior, decision.channel));
        appliedBehavior = behavior;
        lastStatus = "Added " + decision.channel + " behavior.";
        error = null;
        return true;
    }

    public bool UndoLastChange(GameObject target)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            BehaviorChange change = history[i];

            if (target != null && change.target != target)
            {
                continue;
            }

            history.RemoveAt(i);

            if (UndoChange(change))
            {
                return true;
            }
        }

        lastStatus = target == null
            ? "There are no behavior changes to undo."
            : "There are no behavior changes to undo on " + target.name + ".";
        Debug.Log(lastStatus);
        return false;
    }

    public int ClearChannel(GameObject target, BehaviorChannel channel)
    {
        return ClearMatchingBehaviors(target, channel, false);
    }

    public int ClearAll(GameObject target)
    {
        return ClearMatchingBehaviors(target, BehaviorChannel.General, true);
    }

    private ScriptedLuaBehavior FindActiveBehavior(
        GameObject target,
        BehaviorChannel channel,
        bool refineSameChannel)
    {
        if (!refineSameChannel)
        {
            return null;
        }

        ScriptedLuaBehavior[] behaviors = target.GetComponents<ScriptedLuaBehavior>();

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            if (behaviors[i].enabled && behaviors[i].behaviorChannel == channel)
            {
                return behaviors[i];
            }
        }

        return null;
    }

    private int ClearMatchingBehaviors(GameObject target, BehaviorChannel channel, bool includeAllChannels)
    {
        if (target == null)
        {
            lastStatus = "No target object assigned.";
            Debug.LogWarning(lastStatus);
            return 0;
        }

        ScriptedLuaBehavior[] behaviors = target.GetComponents<ScriptedLuaBehavior>();
        List<SuspendedBehavior> suspended = new List<SuspendedBehavior>();

        foreach (ScriptedLuaBehavior behavior in behaviors)
        {
            if (!behavior.enabled || (!includeAllChannels && behavior.behaviorChannel != channel))
            {
                continue;
            }

            suspended.Add(new SuspendedBehavior(
                behavior,
                behavior.behaviorChannel,
                ObjectState.Capture(target)));
            RestoreBaselineWhenStopping(behavior);
            behavior.enabled = false;
        }

        if (suspended.Count == 0)
        {
            lastStatus = includeAllChannels
                ? "No active behaviors to clear on " + target.name + "."
                : "No active " + channel + " behavior to clear on " + target.name + ".";
            Debug.Log(lastStatus);
            return 0;
        }

        RecordChange(BehaviorChange.CreateClear(target, suspended));
        lastStatus = includeAllChannels
            ? "Cleared " + suspended.Count + " behavior(s) on " + target.name + "."
            : "Cleared " + suspended.Count + " " + channel + " behavior(s) on " + target.name + ".";
        Debug.Log(lastStatus);
        return suspended.Count;
    }

    private bool UndoChange(BehaviorChange change)
    {
        switch (change.kind)
        {
            case BehaviorChangeKind.Added:
                if (change.behavior == null)
                {
                    return false;
                }

                RestoreBaselineWhenStopping(change.behavior);
                baselines.Remove(change.behavior);
                DestroyBehavior(change.behavior);
                lastStatus = "Undid the added " + change.channel + " behavior.";
                break;

            case BehaviorChangeKind.Refined:
                if (change.behavior == null)
                {
                    return false;
                }

                change.stateBeforeChange.RestoreForChannel(change.channel);
                RestoreRefinement(change);

                if (!change.behavior.enabled)
                {
                    lastStatus = "Could not restore the previous " + change.channel + " behavior.";
                    Debug.LogError(lastStatus);
                    return false;
                }

                lastStatus = "Restored the previous " + change.channel + " behavior.";
                break;

            case BehaviorChangeKind.Cleared:
                int restoredCount = 0;

                foreach (SuspendedBehavior suspended in change.suspendedBehaviors)
                {
                    if (suspended.behavior == null)
                    {
                        continue;
                    }

                    suspended.stateBeforeClear.RestoreForChannel(suspended.channel);
                    suspended.behavior.enabled = true;
                    restoredCount++;
                }

                if (restoredCount == 0)
                {
                    return false;
                }

                lastStatus = "Restored " + restoredCount + " cleared behavior(s).";
                break;

            default:
                return false;
        }

        Debug.Log(lastStatus);
        return true;
    }

    private static void ConfigureBehavior(
        ScriptedLuaBehavior behavior,
        BehaviorChannel channel,
        Transform headTransform,
        Transform leftHandTransform,
        Transform rightHandTransform)
    {
        behavior.behaviorChannel = channel;
        behavior.headTransform = headTransform;
        behavior.leftHandTransform = leftHandTransform;
        behavior.rightHandTransform = rightHandTransform;
    }

    private void RestoreRefinement(BehaviorChange change)
    {
        change.behavior.behaviorChannel = change.channel;
        change.behavior.revisionCount = change.previousRevisionCount;
        change.behavior.LoadScript(change.previousScript, change.previousCommand);
    }

    private void RestoreBaselineWhenStopping(ScriptedLuaBehavior behavior)
    {
        if (behavior.behaviorChannel != BehaviorChannel.Appearance &&
            behavior.behaviorChannel != BehaviorChannel.Scale &&
            behavior.behaviorChannel != BehaviorChannel.General)
        {
            return;
        }

        if (baselines.TryGetValue(behavior, out ObjectState baseline))
        {
            baseline.RestoreForChannel(behavior.behaviorChannel);
        }
    }

    private void RecordChange(BehaviorChange change)
    {
        history.Add(change);
        int limit = Mathf.Max(1, maxHistoryEntries);

        while (history.Count > limit)
        {
            BehaviorChange expired = history[0];
            history.RemoveAt(0);
            PermanentlyDiscardClearedBehaviors(expired);
        }
    }

    private void PermanentlyDiscardClearedBehaviors(BehaviorChange change)
    {
        if (change.kind != BehaviorChangeKind.Cleared || change.suspendedBehaviors == null)
        {
            return;
        }

        foreach (SuspendedBehavior suspended in change.suspendedBehaviors)
        {
            if (suspended.behavior == null || suspended.behavior.enabled)
            {
                continue;
            }

            baselines.Remove(suspended.behavior);
            DestroyBehavior(suspended.behavior);
        }
    }

    private static void DestroyBehavior(ScriptedLuaBehavior behavior)
    {
        if (Application.isPlaying)
        {
            Destroy(behavior);
        }
        else
        {
            DestroyImmediate(behavior);
        }
    }

    private static string CombineCommandHistory(string existingCommand, string newCommand)
    {
        return string.IsNullOrWhiteSpace(existingCommand)
            ? newCommand
            : existingCommand + " + " + newCommand;
    }

    private enum BehaviorChangeKind
    {
        Added,
        Refined,
        Cleared
    }

    private sealed class BehaviorChange
    {
        public BehaviorChangeKind kind;
        public GameObject target;
        public ScriptedLuaBehavior behavior;
        public BehaviorChannel channel;
        public string previousScript;
        public string previousCommand;
        public int previousRevisionCount;
        public ObjectState stateBeforeChange;
        public List<SuspendedBehavior> suspendedBehaviors;

        public static BehaviorChange CreateAddition(
            GameObject target,
            ScriptedLuaBehavior behavior,
            BehaviorChannel channel)
        {
            return new BehaviorChange
            {
                kind = BehaviorChangeKind.Added,
                target = target,
                behavior = behavior,
                channel = channel
            };
        }

        public static BehaviorChange CreateRefinement(
            ScriptedLuaBehavior behavior,
            ObjectState stateBeforeChange)
        {
            return new BehaviorChange
            {
                kind = BehaviorChangeKind.Refined,
                target = behavior.gameObject,
                behavior = behavior,
                channel = behavior.behaviorChannel,
                previousScript = behavior.scriptText,
                previousCommand = behavior.sourceCommand,
                previousRevisionCount = behavior.revisionCount,
                stateBeforeChange = stateBeforeChange
            };
        }

        public static BehaviorChange CreateClear(GameObject target, List<SuspendedBehavior> suspendedBehaviors)
        {
            return new BehaviorChange
            {
                kind = BehaviorChangeKind.Cleared,
                target = target,
                suspendedBehaviors = suspendedBehaviors
            };
        }
    }

    private sealed class SuspendedBehavior
    {
        public readonly ScriptedLuaBehavior behavior;
        public readonly BehaviorChannel channel;
        public readonly ObjectState stateBeforeClear;

        public SuspendedBehavior(
            ScriptedLuaBehavior behavior,
            BehaviorChannel channel,
            ObjectState stateBeforeClear)
        {
            this.behavior = behavior;
            this.channel = channel;
            this.stateBeforeClear = stateBeforeClear;
        }
    }

    private sealed class ObjectState
    {
        private readonly Transform targetTransform;
        private readonly Vector3 position;
        private readonly Quaternion rotation;
        private readonly Vector3 scale;
        private readonly Renderer renderer;
        private readonly bool rendererEnabled;
        private readonly Material material;
        private readonly bool hasBaseColor;
        private readonly Color baseColor;
        private readonly bool hasColor;
        private readonly Color color;
        private readonly bool hasEmission;
        private readonly Color emissionColor;
        private readonly bool emissionEnabled;

        private ObjectState(GameObject target)
        {
            targetTransform = target.transform;
            position = targetTransform.position;
            rotation = targetTransform.rotation;
            scale = targetTransform.localScale;
            renderer = target.GetComponent<Renderer>();

            if (renderer == null)
            {
                return;
            }

            rendererEnabled = renderer.enabled;
            material = renderer.material;

            if (material == null)
            {
                return;
            }

            hasBaseColor = material.HasProperty("_BaseColor");
            baseColor = hasBaseColor ? material.GetColor("_BaseColor") : default(Color);
            hasColor = material.HasProperty("_Color");
            color = hasColor ? material.GetColor("_Color") : default(Color);
            hasEmission = material.HasProperty("_EmissionColor");
            emissionColor = hasEmission ? material.GetColor("_EmissionColor") : default(Color);
            emissionEnabled = material.IsKeywordEnabled("_EMISSION");
        }

        public static ObjectState Capture(GameObject target)
        {
            return new ObjectState(target);
        }

        public void RestoreAll()
        {
            RestorePosition();
            RestoreRotation();
            RestoreScale();
            RestoreAppearance();
        }

        public void RestoreForChannel(BehaviorChannel channel)
        {
            switch (channel)
            {
                case BehaviorChannel.Position:
                    RestorePosition();
                    break;
                case BehaviorChannel.Rotation:
                case BehaviorChannel.Attention:
                    RestoreRotation();
                    break;
                case BehaviorChannel.Scale:
                    RestoreScale();
                    break;
                case BehaviorChannel.Appearance:
                    RestoreAppearance();
                    break;
                default:
                    RestoreAll();
                    break;
            }
        }

        private void RestorePosition()
        {
            if (targetTransform != null)
            {
                targetTransform.position = position;
            }
        }

        private void RestoreRotation()
        {
            if (targetTransform != null)
            {
                targetTransform.rotation = rotation;
            }
        }

        private void RestoreScale()
        {
            if (targetTransform != null)
            {
                targetTransform.localScale = scale;
            }
        }

        private void RestoreAppearance()
        {
            if (renderer != null)
            {
                renderer.enabled = rendererEnabled;
            }

            if (material == null)
            {
                return;
            }

            if (hasBaseColor)
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (hasColor)
            {
                material.SetColor("_Color", color);
            }

            if (hasEmission)
            {
                material.SetColor("_EmissionColor", emissionColor);
            }

            if (emissionEnabled)
            {
                material.EnableKeyword("_EMISSION");
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
        }
    }
}
