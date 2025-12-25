using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class MissingReferencesFinder : EditorWindow
{
    [MenuItem("Tools/Find Missing References")]
    public static void ShowWindow()
    {
        GetWindow(typeof(MissingReferencesFinder));
    }

    public void OnGUI()
    {
        if (GUILayout.Button("Find Missing Scripts in Current Scene"))
        {
            FindMissingScriptsInScene();
        }
        
        if (GUILayout.Button("Find Missing Scripts in All Prefabs"))
        {
            FindMissingScriptsInPrefabs();
        }
    }

    private static void FindMissingScriptsInScene()
    {
        GameObject[] go = GetAllObjectsInScene();
        int count = 0;
        foreach (GameObject g in go)
        {
            Component[] components = g.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    Debug.LogError($"Missing Script found on GameObject: <b>{GetFullPath(g)}</b>", g);
                    count++;
                }
            }
        }
        Debug.Log($"Finished searching scene. Found {count} missing scripts.");
    }

    private static void FindMissingScriptsInPrefabs()
    {
        string[] allPrefabs = AssetDatabase.GetAllAssetPaths().Where(path => path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)).ToArray();
        int count = 0;
        foreach (string path in allPrefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            foreach (Component c in components)
            {
                if (c == null)
                {
                    Debug.LogError($"Missing Script found in Prefab: <b>{path}</b>", prefab);
                    count++;
                }
            }
        }
        Debug.Log($"Finished searching prefabs. Found {count} missing scripts.");
    }

    private static GameObject[] GetAllObjectsInScene()
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go))
                   && go.hideFlags == HideFlags.None).ToArray();
    }

    private static string GetFullPath(GameObject go)
    {
        return go.transform.parent == null
            ? go.name
            : GetFullPath(go.transform.parent.gameObject) + "/" + go.name;
    }
}
