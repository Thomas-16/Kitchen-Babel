using UnityEditor;
using UnityEngine;

public class CounterToPlaceholderConverter : EditorWindow
{
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
            "After conversion, you need to assign the NetworkPrefabRef on each placeholder " +
            "in the Inspector (drag the counter prefab from Assets/Prefabs/Counters/).\n\n" +
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

        if (!dryRun)
        {
            Undo.SetCurrentGroupName("Convert Counters to Placeholders");
        }

        foreach (BaseCounter counter in counters)
        {
            string counterType = counter.GetType().Name;
            string prefabName = GetSourcePrefabName(counter.gameObject);
            bool isShuffleable = counter is ContainerCounter;

            string label = !string.IsNullOrEmpty(prefabName) ? prefabName : counterType;

            if (dryRun)
            {
                Debug.Log($"[Preview] {counter.gameObject.name} -> {label}_Placeholder " +
                          $"(pos={counter.transform.position}, rot={counter.transform.eulerAngles}" +
                          $"{(isShuffleable ? ", SHUFFLEABLE" : "")}) " +
                          $"— Assign prefab: {label}");
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

            // Set shuffleable flag via SerializedObject
            SerializedObject so = new SerializedObject(placeholder);
            so.FindProperty("isShuffleable").boolValue = isShuffleable;
            so.ApplyModifiedProperties();

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
                      "Now assign NetworkPrefabRef on each placeholder in the Inspector.");
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
}
