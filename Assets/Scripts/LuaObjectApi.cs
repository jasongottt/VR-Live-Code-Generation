using MoonSharp.Interpreter;
using UnityEngine;

[MoonSharpUserData]
public sealed class LuaObjectApi
{
    private readonly GameObject target;
    private Renderer cachedRenderer;
    private Material runtimeMaterial;

    public LuaObjectApi(GameObject target)
    {
        this.target = target;
    }

    public LuaVector3 getPosition()
    {
        return new LuaVector3(target.transform.position);
    }

    public LuaVector3 position
    {
        get { return getPosition(); }
    }

    public LuaVector3 getForward()
    {
        return new LuaVector3(target.transform.forward);
    }

    public LuaVector3 forward
    {
        get { return getForward(); }
    }

    public LuaVector3 getRotationEuler()
    {
        return new LuaVector3(target.transform.eulerAngles);
    }

    public LuaVector3 rotationEuler
    {
        get { return getRotationEuler(); }
    }

    public void setPosition(float x, float y, float z)
    {
        target.transform.position = new Vector3(x, y, z);
    }

    public void translate(float x, float y, float z)
    {
        target.transform.Translate(new Vector3(x, y, z), Space.World);
    }

    public void rotate(float x, float y, float z)
    {
        target.transform.Rotate(new Vector3(x, y, z), Space.Self);
    }

    public void setRotationEuler(float x, float y, float z)
    {
        target.transform.eulerAngles = new Vector3(x, y, z);
    }

    public void lookAt(float x, float y, float z)
    {
        target.transform.LookAt(new Vector3(x, y, z));
    }

    public void moveToward(float x, float y, float z, float speed, float dt)
    {
        Vector3 destination = new Vector3(x, y, z);
        float step = Mathf.Max(0f, speed) * Mathf.Max(0f, dt);
        target.transform.position = Vector3.MoveTowards(target.transform.position, destination, step);
    }

    public LuaVector3 getScale()
    {
        return new LuaVector3(target.transform.localScale);
    }

    public LuaVector3 scale
    {
        get { return getScale(); }
    }

    public void setScale(float x, float y, float z)
    {
        target.transform.localScale = new Vector3(x, y, z);
    }

    public void setVisible(bool isVisible)
    {
        Renderer renderer = GetRenderer();

        if (renderer != null)
        {
            renderer.enabled = isVisible;
        }
    }

    public bool isVisible()
    {
        Renderer renderer = GetRenderer();
        return target.activeInHierarchy && renderer != null && renderer.enabled;
    }

    public LuaColor getColor()
    {
        Material material = GetRuntimeMaterial();

        if (material == null)
        {
            return new LuaColor(Color.white);
        }

        if (material.HasProperty("_BaseColor"))
        {
            return new LuaColor(material.GetColor("_BaseColor"));
        }

        if (material.HasProperty("_Color"))
        {
            return new LuaColor(material.GetColor("_Color"));
        }

        return new LuaColor(Color.white);
    }

    public void setColor(float r, float g, float b, float a)
    {
        Material material = GetRuntimeMaterial();

        if (material == null)
        {
            return;
        }

        Color color = new Color(r, g, b, a);

        bool colorWasSet = false;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
            colorWasSet = true;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
            colorWasSet = true;
        }

        if (!colorWasSet)
        {
            Debug.LogWarning("Runtime material has no _BaseColor or _Color property.");
        }
    }

    public LuaColor getEmission()
    {
        Material material = GetRuntimeMaterial();

        if (material != null && material.HasProperty("_EmissionColor"))
        {
            return new LuaColor(material.GetColor("_EmissionColor"));
        }

        return new LuaColor(Color.black);
    }

    public void setEmission(float r, float g, float b, float intensity)
    {
        Material material = GetRuntimeMaterial();

        if (material == null)
        {
            return;
        }

        Color emissionColor = new Color(r, g, b, 1f) * Mathf.Max(0f, intensity);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emissionColor);
        }
    }

    private Material GetRuntimeMaterial()
    {
        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        Renderer renderer = GetRenderer();

        if (renderer == null)
        {
            return null;
        }

        runtimeMaterial = renderer.material;
        return runtimeMaterial;
    }

    private Renderer GetRenderer()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = target.GetComponent<Renderer>();
        }

        return cachedRenderer;
    }
}

[MoonSharpUserData]
public sealed class LuaColor
{
    public float r;
    public float g;
    public float b;
    public float a;

    public LuaColor()
    {
    }

    public LuaColor(Color color)
    {
        r = color.r;
        g = color.g;
        b = color.b;
        a = color.a;
    }
}

[MoonSharpUserData]
public sealed class LuaVector3
{
    public float x;
    public float y;
    public float z;

    public LuaVector3()
    {
    }

    public LuaVector3(Vector3 vector)
    {
        x = vector.x;
        y = vector.y;
        z = vector.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
