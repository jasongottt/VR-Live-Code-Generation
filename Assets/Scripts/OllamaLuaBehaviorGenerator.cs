using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaLuaBehaviorGenerator : MonoBehaviour
{
    public string endpoint = "http://localhost:11434/api/generate";
    public string model = "qwen2.5-coder:7b";
    [Range(0f, 1f)]
    public float temperature = 0.2f;
    public int requestTimeoutSeconds = 60;
    public bool logPrompt;
    public bool logRawResponse;

    [TextArea(8, 24)]
    public string lastPrompt;

    [TextArea(8, 24)]
    public string lastRawResponse;

    public IEnumerator GenerateScript(string userCommand, Action<string> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(userCommand))
        {
            onError?.Invoke("Command was empty.");
            yield break;
        }

        lastPrompt = BuildPrompt(userCommand);

        if (logPrompt)
        {
            Debug.Log(lastPrompt);
        }

        OllamaGenerateRequest requestBody = new OllamaGenerateRequest
        {
            model = model,
            prompt = lastPrompt,
            stream = false,
            options = new OllamaOptions
            {
                temperature = temperature
            }
        };

        string requestJson = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);

        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(request.error);
                yield break;
            }

            lastRawResponse = request.downloadHandler.text;

            if (logRawResponse)
            {
                Debug.Log(lastRawResponse);
            }
        }

        OllamaGenerateResponse response;

        try
        {
            response = JsonUtility.FromJson<OllamaGenerateResponse>(lastRawResponse);
        }
        catch (Exception exception)
        {
            onError?.Invoke("Could not parse Ollama response: " + exception.Message);
            yield break;
        }

        string scriptText = SanitizeLuaResponse(response != null ? response.response : null);

        if (!LuaScriptValidator.IsSafe(scriptText, out string validationError))
        {
            onError?.Invoke(validationError);
            yield break;
        }

        onSuccess?.Invoke(scriptText);
    }

    private static string BuildPrompt(string userCommand)
    {
        return
@"You generate Lua scripts for a Unity VR behavior sandbox.
Return only Lua code. Do not use Markdown fences. Do not explain anything.

Write optional function start() and/or function update(dt).
The script controls one selected object.

Allowed globals:
- object
- player
- world
- leftHand
- rightHand
- time
- dt
- log(message)
- distance(a, b)
- direction(from, to)
- math

Allowed object methods:
- object:getPosition()
- object:setPosition(x, y, z)
- object:translate(x, y, z)
- object:rotate(x, y, z)
- object:lookAt(x, y, z)
- object:moveToward(x, y, z, speed, dt)
- object:getScale()
- object:setScale(x, y, z)
- object:setColor(r, g, b, a)
- object:setEmission(r, g, b, intensity)
- object:setVisible(isVisible)

Allowed player/world methods:
- player:getHeadPosition()
- player:getHeadForward()
- world:getHeadPosition()
- world:getHeadForward()

Allowed hand methods:
- leftHand:getPosition()
- leftHand:getForward()
- leftHand:getRotationEuler()
- leftHand:isTracked()
- rightHand:getPosition()
- rightHand:getForward()
- rightHand:getRotationEuler()
- rightHand:isTracked()

Rules:
- Use only the allowed API above.
- Do not use require, io, os, debug, load, loadstring, dofile, collectgarbage, package, coroutine, setmetatable, getmetatable, rawget, rawset, or while loops.
- Do not create infinite loops.
- Prefer simple frame-by-frame behavior in update(dt).
- If using a hand position, check isTracked() first.
- Use numeric literals for colors, speeds, distances, and amplitudes.

User command:
""" + userCommand.Replace("\"", "'") + @"""";
    }

    private static string SanitizeLuaResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        string script = response.Trim();

        if (script.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineBreak = script.IndexOf('\n');

            if (firstLineBreak >= 0)
            {
                script = script.Substring(firstLineBreak + 1);
            }

            int closingFence = script.LastIndexOf("```", StringComparison.Ordinal);

            if (closingFence >= 0)
            {
                script = script.Substring(0, closingFence);
            }
        }

        return script.Trim();
    }

    [Serializable]
    private sealed class OllamaGenerateRequest
    {
        public string model;
        public string prompt;
        public bool stream;
        public OllamaOptions options;
    }

    [Serializable]
    private sealed class OllamaOptions
    {
        public float temperature;
    }

    [Serializable]
    private sealed class OllamaGenerateResponse
    {
        public string response;
    }
}
