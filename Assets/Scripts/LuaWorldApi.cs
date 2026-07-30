using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

[MoonSharpUserData]
public sealed class LuaWorldApi
{
    private readonly Transform headTransform;

    public LuaWorldApi(Transform headTransform)
    {
        this.headTransform = headTransform;
    }

    public LuaVector3 getHeadPosition()
    {
        Transform head = GetHeadTransform();
        return new LuaVector3(head != null ? head.position : Vector3.zero);
    }

    public LuaVector3 getPosition()
    {
        return getHeadPosition();
    }

    public LuaVector3 getHeadForward()
    {
        Transform head = GetHeadTransform();
        return new LuaVector3(head != null ? head.forward : Vector3.forward);
    }

    public LuaVector3 getForward()
    {
        return getHeadForward();
    }

    public LuaVector3 getRotationEuler()
    {
        Transform head = GetHeadTransform();
        return new LuaVector3(head != null ? head.eulerAngles : Vector3.zero);
    }

    public bool isTracked()
    {
        return GetHeadTransform() != null;
    }

    public LuaVector3 position
    {
        get { return getHeadPosition(); }
    }

    public LuaVector3 forward
    {
        get { return getHeadForward(); }
    }

    private Transform GetHeadTransform()
    {
        if (headTransform != null)
        {
            return headTransform;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }
}

[MoonSharpUserData]
public sealed class LuaControllerApi
{
    private readonly XRNode node;
    private readonly Transform fallbackTransform;
    private readonly List<XRInputDevice> devices = new List<XRInputDevice>();
    private int devicesFrame = -1;

    public LuaControllerApi(XRNode node, Transform fallbackTransform)
    {
        this.node = node;
        this.fallbackTransform = fallbackTransform;
    }

    public LuaVector3 getPosition()
    {
        if (TryGetDevicePosition(out Vector3 position))
        {
            return new LuaVector3(position);
        }

        return new LuaVector3(fallbackTransform != null ? fallbackTransform.position : Vector3.zero);
    }

    public LuaVector3 position
    {
        get { return getPosition(); }
    }

    public LuaVector3 getForward()
    {
        if (TryGetDeviceRotation(out Quaternion rotation))
        {
            return new LuaVector3(rotation * Vector3.forward);
        }

        return new LuaVector3(fallbackTransform != null ? fallbackTransform.forward : Vector3.forward);
    }

    public LuaVector3 forward
    {
        get { return getForward(); }
    }

    public LuaVector3 getRotationEuler()
    {
        if (TryGetDeviceRotation(out Quaternion rotation))
        {
            return new LuaVector3(rotation.eulerAngles);
        }

        return new LuaVector3(fallbackTransform != null ? fallbackTransform.eulerAngles : Vector3.zero);
    }

    public LuaVector3 rotationEuler
    {
        get { return getRotationEuler(); }
    }

    public LuaVector3 getVelocity()
    {
        return TryGetDeviceVector3(CommonUsages.deviceVelocity, out Vector3 velocity)
            ? new LuaVector3(velocity)
            : new LuaVector3(Vector3.zero);
    }

    public LuaVector3 getAngularVelocity()
    {
        return TryGetDeviceVector3(CommonUsages.deviceAngularVelocity, out Vector3 velocity)
            ? new LuaVector3(velocity)
            : new LuaVector3(Vector3.zero);
    }

    public float getGrip()
    {
        return TryGetDeviceFloat(CommonUsages.grip, out float grip)
            ? Mathf.Clamp01(grip)
            : 0f;
    }

    public float getTrigger()
    {
        return TryGetDeviceFloat(CommonUsages.trigger, out float trigger)
            ? Mathf.Clamp01(trigger)
            : 0f;
    }

    public bool isPrimaryPressed()
    {
        return TryGetDeviceBool(CommonUsages.primaryButton, out bool pressed) && pressed;
    }

    public bool isSecondaryPressed()
    {
        return TryGetDeviceBool(CommonUsages.secondaryButton, out bool pressed) && pressed;
    }

    public bool isTracked()
    {
        RefreshDevices();

        foreach (XRInputDevice device in devices)
        {
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked)
            {
                return true;
            }
        }

        return fallbackTransform != null;
    }

    private bool TryGetDevicePosition(out Vector3 position)
    {
        RefreshDevices();

        foreach (XRInputDevice device in devices)
        {
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out position))
            {
                return true;
            }
        }

        position = Vector3.zero;
        return false;
    }

    private bool TryGetDeviceRotation(out Quaternion rotation)
    {
        RefreshDevices();

        foreach (XRInputDevice device in devices)
        {
            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
            {
                return true;
            }
        }

        rotation = Quaternion.identity;
        return false;
    }

    private bool TryGetDeviceVector3(
        InputFeatureUsage<Vector3> usage,
        out Vector3 value)
    {
        RefreshDevices();

        foreach (XRInputDevice device in devices)
        {
            if (device.TryGetFeatureValue(usage, out value))
            {
                return true;
            }
        }

        value = Vector3.zero;
        return false;
    }

    private bool TryGetDeviceFloat(
        InputFeatureUsage<float> usage,
        out float value)
    {
        RefreshDevices();

        foreach (XRInputDevice device in devices)
        {
            if (device.TryGetFeatureValue(usage, out value))
            {
                return true;
            }
        }

        value = 0f;
        return false;
    }

    private bool TryGetDeviceBool(
        InputFeatureUsage<bool> usage,
        out bool value)
    {
        RefreshDevices();

        foreach (XRInputDevice device in devices)
        {
            if (device.TryGetFeatureValue(usage, out value))
            {
                return true;
            }
        }

        value = false;
        return false;
    }

    private void RefreshDevices()
    {
        if (devicesFrame == Time.frameCount)
        {
            return;
        }

        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);
        devicesFrame = Time.frameCount;
    }
}

public static class LuaMathApi
{
    public static LuaVector3 vec3(float x, float y, float z)
    {
        return new LuaVector3(new Vector3(x, y, z));
    }

    public static LuaVector3 add(LuaVector3 a, LuaVector3 b)
    {
        return new LuaVector3(ToUnityVector(a) + ToUnityVector(b));
    }

    public static LuaVector3 subtract(LuaVector3 a, LuaVector3 b)
    {
        return new LuaVector3(ToUnityVector(a) - ToUnityVector(b));
    }

    public static LuaVector3 scale(LuaVector3 vector, float amount)
    {
        return new LuaVector3(ToUnityVector(vector) * amount);
    }

    public static LuaVector3 normalize(LuaVector3 vector)
    {
        return new LuaVector3(ToUnityVector(vector).normalized);
    }

    public static float dot(LuaVector3 a, LuaVector3 b)
    {
        return Vector3.Dot(ToUnityVector(a), ToUnityVector(b));
    }

    public static LuaVector3 cross(LuaVector3 a, LuaVector3 b)
    {
        return new LuaVector3(Vector3.Cross(ToUnityVector(a), ToUnityVector(b)));
    }

    public static LuaVector3 lerp(LuaVector3 a, LuaVector3 b, float t)
    {
        return new LuaVector3(Vector3.Lerp(ToUnityVector(a), ToUnityVector(b), t));
    }

    public static float clamp(float value, float minimum, float maximum)
    {
        float lower = Mathf.Min(minimum, maximum);
        float upper = Mathf.Max(minimum, maximum);
        return Mathf.Clamp(value, lower, upper);
    }

    public static float smoothstep(float from, float to, float t)
    {
        return Mathf.SmoothStep(from, to, Mathf.Clamp01(t));
    }

    public static float distance(LuaVector3 a, LuaVector3 b)
    {
        return Vector3.Distance(ToUnityVector(a), ToUnityVector(b));
    }

    public static LuaVector3 direction(LuaVector3 from, LuaVector3 to)
    {
        Vector3 delta = ToUnityVector(to) - ToUnityVector(from);

        if (delta.sqrMagnitude <= Mathf.Epsilon)
        {
            return new LuaVector3(Vector3.zero);
        }

        return new LuaVector3(delta.normalized);
    }

    private static Vector3 ToUnityVector(LuaVector3 vector)
    {
        return vector != null ? vector.ToVector3() : Vector3.zero;
    }
}
