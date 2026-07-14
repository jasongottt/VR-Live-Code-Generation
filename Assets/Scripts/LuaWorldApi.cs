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

    public bool isTracked()
    {
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);

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
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);

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
        devices.Clear();
        InputDevices.GetDevicesAtXRNode(node, devices);

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
}

public static class LuaMathApi
{
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
