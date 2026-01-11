using UnityEngine;
using System.Runtime.InteropServices;
using TMPro;

public class WebGLHelper : MonoBehaviour
{
    // Singleton for callbacks
    public static WebGLHelper instance;

    [DllImport("__Internal")]
    private static extern void CopyToClipboardJS(string str);

    [DllImport("__Internal")]
    private static extern void RequestPasteFromClipboardJS(string gameObjectName, string methodName);

    private System.Action<string> onPasteCallback;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static void CopyToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CopyToClipboardJS(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }

    public static void PasteFromClipboard(System.Action<string> callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (instance == null)
        {
            GameObject go = new GameObject("WebGLHelper");
            instance = go.AddComponent<WebGLHelper>();
        }
        instance.onPasteCallback = callback;
        RequestPasteFromClipboardJS(instance.gameObject.name, "OnClipboardPasteReceived");
#else
        callback?.Invoke(GUIUtility.systemCopyBuffer);
#endif
    }

    // Callback from JS
    public void OnClipboardPasteReceived(string text)
    {
        onPasteCallback?.Invoke(text);
    }

    // --- Input Field Handling ---
    public static void SetupInputField(TMP_InputField input)
    {
        if (input == null) return;
        input.onSelect.AddListener((val) => SetKeyboardCapture(false));
        input.onDeselect.AddListener((val) => SetKeyboardCapture(true));
    }

    public static void SetKeyboardCapture(bool allowCapture)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLInput.captureAllKeyboardInput = allowCapture;
#endif
    }
}
