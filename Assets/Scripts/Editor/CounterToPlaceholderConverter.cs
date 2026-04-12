using Fusion;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class CounterToPlaceholderConverter : EditorWindow
{
    private static readonly FieldInfo CounterPrefabField =
        typeof(CounterPlaceholder).GetField("counterPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo IsShuffleableField =
        typeof(CounterPlaceholder).GetField("isShuffleable", BindingFlags.NonPublic | BindingFlags.Instance);

    [MenuItem("Tools/Convert Counters to Placeholders")]
    public static void ShowWindow()
    {
        GetWindow<CounterToPlaceholderConverter>("Counter Converter");
    }

    private Transform parentContainer;

    private void OnGUI()
    {
        GUILayout.Label("Counter to Placeholder Converter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "This tool finds all BaseCounter instances in the scene, creates CounterPlaceholder " +
            "GameObjects at the same positions, and removes the originals.\n\n" +
            "ContainerCounters are marked as Shuffleable.\n\n" +
            "When the source counter came from a Fusion network prefab asset, the tool will " +
            "auto-assign the placeholder's NetworkPrefabRef.\n\n" +
            "Placeholders are named after their counter type for easy identification.",
            MessageType.Info);

        EditorGUILayout.Space();
        parentContainer = (Transform)EditorGUILayout.ObjectField(
            "Parent Container (optional)", parentContainer, typeof(Transform), true);

        EditorGUILayout.Space();

        if (GUILayout.Button("Preview (Dry Run)"))
        {
            Run(dryRun: true);
        }

        EditorGUILayout.Space();

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("Convert All Counters"))
        {
            if (EditorUtility.DisplayDialog(
                "Convert Counters",
                "This will replace all BaseCounter instances in the scene with CounterPlaceholder objects. This action can be undone with Ctrl+Z.",
                "Convert", "Cancel"))
            {
                Run(dryRun: false);
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private void Run(bool dryRun)
    {
        BaseCounter[] counters = FindObjectsByType<BaseCounter>(FindObjectsSortMode.None);

        if (counters.Length == 0)
        {
            Debug.Log("[Converter] No BaseCounter instances found in scene.");
            return;
        }

        int converted = 0;
        int autoAssigned = 0;
        List<string> unresolvedCounters = new List<string>();

        if (!dryRun)
        {
            Undo.SetCurrentGroupName("Convert Counters to Placeholders");
        }

        foreach (BaseCounter counter in counters)
        {
            string counterType = counter.GetType().Name;
            string prefabName = GetSourcePrefabName(counter.gameObject);
            bool isShuffleable = counter is ContainerCounter;
            bool hasPrefabRef = TryGetSourcePrefabRef(counter.gameObject, out NetworkPrefabRef counterPrefabRef, out string prefabRefIssue);

            string label = !string.IsNullOrEmpty(prefabName) ? prefabName : counterType;

            if (dryRun)
            {
                Debug.Log($"[Preview] {counter.gameObject.name} -> {label}_Placeholder " +
                          $"(pos={counter.transform.position}, rot={counter.transform.eulerAngles}" +
                          $"{(isShuffleable ? ", SHUFFLEABLE" : "")}) " +
                          $"— PrefabRef: {(hasPrefabRef ? counterPrefabRef.ToString() : $"MISSING ({prefabRefIssue})")}");
                continue;
            }

            // Create placeholder GameObject
            GameObject placeholderGO = new GameObject($"{label}_Placeholder");
            Undo.RegisterCreatedObjectUndo(placeholderGO, "Create Placeholder");

            placeholderGO.transform.position = counter.transform.position;
            placeholderGO.transform.rotation = counter.transform.rotation;

            if (parentContainer != null)
            {
                placeholderGO.transform.SetParent(parentContainer);
            }

            // Add CounterPlaceholder component
            CounterPlaceholder placeholder = placeholderGO.AddComponent<CounterPlaceholder>();

            IsShuffleableField?.SetValue(placeholder, isShuffleable);

            if (hasPrefabRef)
            {
                CounterPrefabField?.SetValue(placeholder, counterPrefabRef);
                EditorUtility.SetDirty(placeholder);
                autoAssigned++;
            }
            else
            {
                unresolvedCounters.Add($"{counter.gameObject.name}: {prefabRefIssue}");
            }

            // Destroy the original counter
            Undo.DestroyObjectImmediate(counter.gameObject);

            converted++;
        }

        if (dryRun)
        {
            Debug.Log($"[Preview] Would convert {counters.Length} counters. No changes made.");
        }
        else
        {
            Debug.Log($"[Converter] Converted {converted} counters to placeholders. " +
                      $"Auto-assigned prefab refs for {autoAssigned}/{converted} placeholders.");

            foreach (string unresolved in unresolvedCounters)
            {
                Debug.LogWarning($"[Converter] Could not auto-assign prefab ref for {unresolved}");
            }
        }
    }

    private static string GetSourcePrefabName(GameObject instance)
    {
        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (prefabSource != null)
        {
            return prefabSource.name;
        }
        return null;
    }

    private static bool TryGetSourcePrefabRef(GameObject instance, out NetworkPrefabRef prefabRef, out string issue)
    {
        GameObject prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(instance);
        if (prefabSource == null)
        {
            prefabRef = default;
            issue = "object is not linked to a prefab asset";
            return false;
        }

        NetworkObject networkObject = prefabSource.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            prefabRef = default;
            issue = "source prefab has no NetworkObject";
            return false;
        }

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(networkObject, out string guidString, out long _))
        {
            prefabRef = default;
            issue = "could not read prefab asset guid";
            return false;
        }

        if (!NetworkObjectGuid.TryParse(guidString, out NetworkObjectGuid prefabGuid))
        {
            prefabRef = default;
            issue = $"invalid network prefab guid: {guidString}";
            return false;
        }

        prefabRef = (NetworkPrefabRef)prefabGuid;
        issue = null;
        return prefabRef.IsValid;
    }
}
