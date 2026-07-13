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

    public LuaVector3 getScale()
    {
        return new LuaVector3(target.transform.localScale);
    }

    public void setScale(float x, float y, float z)
    {
        target.transform.localScale = new Vector3(x, y, z);
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

        if (cachedRenderer == null)
        {
            cachedRenderer = target.GetComponent<Renderer>();
        }

        if (cachedRenderer == null)
        {
            return null;
        }

        runtimeMaterial = cachedRenderer.material;
        return runtimeMaterial;
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
}
