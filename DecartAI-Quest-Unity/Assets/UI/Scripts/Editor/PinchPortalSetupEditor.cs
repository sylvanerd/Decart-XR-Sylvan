#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Editor utility for setting up the PinchPortalManager in the scene.
/// Provides a menu item to automatically configure the portal system.
/// </summary>
public class PinchPortalSetupEditor : EditorWindow
{
    private OVRHand leftHand;
    private OVRHand rightHand;
    private OVRSkeleton leftSkeleton;
    private OVRSkeleton rightSkeleton;
    private RectTransform canvasRect;
    private RawImage portalDisplay;
    private Material portalMaterial;

    [MenuItem("Decart/Setup Pinch Portal System")]
    public static void ShowWindow()
    {
        GetWindow<PinchPortalSetupEditor>("Pinch Portal Setup");
    }

    private void OnEnable()
    {
        // Try to auto-find references
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        // Find OVRHand components by GameObject name (more reliable than internal properties)
        var allHands = FindObjectsOfType<OVRHand>(true);
        foreach (var hand in allHands)
        {
            string nameLower = hand.gameObject.name.ToLower();
            
            // Check for left hand by name patterns
            if (nameLower.Contains("left") || nameLower.Contains("_l") || nameLower.EndsWith(" l"))
            {
                leftHand = hand;
                leftSkeleton = hand.GetComponent<OVRSkeleton>();
            }
            // Check for right hand by name patterns
            else if (nameLower.Contains("right") || nameLower.Contains("_r") || nameLower.EndsWith(" r"))
            {
                rightHand = hand;
                rightSkeleton = hand.GetComponent<OVRSkeleton>();
            }
        }

        // Try to find the ReceivingRawImages or similar canvas
        var allRectTransforms = FindObjectsOfType<RectTransform>(true);
        foreach (var rt in allRectTransforms)
        {
            if (rt.name.Contains("ReceivingRawImages") || rt.name.Contains("StreamingCanvas"))
            {
                canvasRect = rt;
                portalDisplay = rt.GetComponent<RawImage>();
                break;
            }
        }

        // Try to find the MultiPortalMaterial
        string[] guids = AssetDatabase.FindAssets("MultiPortalMaterial t:Material");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            portalMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Pinch Portal System Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "This utility helps you set up the PinchPortalManager component in your scene.\n\n" +
            "Assign the references below and click 'Create Portal Manager' to add the component.",
            MessageType.Info);

        GUILayout.Space(10);

        GUILayout.Label("Hand Tracking", EditorStyles.boldLabel);
        leftHand = (OVRHand)EditorGUILayout.ObjectField("Left Hand", leftHand, typeof(OVRHand), true);
        rightHand = (OVRHand)EditorGUILayout.ObjectField("Right Hand", rightHand, typeof(OVRHand), true);
        leftSkeleton = (OVRSkeleton)EditorGUILayout.ObjectField("Left Skeleton", leftSkeleton, typeof(OVRSkeleton), true);
        rightSkeleton = (OVRSkeleton)EditorGUILayout.ObjectField("Right Skeleton", rightSkeleton, typeof(OVRSkeleton), true);

        GUILayout.Space(10);

        GUILayout.Label("Canvas", EditorStyles.boldLabel);
        canvasRect = (RectTransform)EditorGUILayout.ObjectField("Canvas RectTransform", canvasRect, typeof(RectTransform), true);
        portalDisplay = (RawImage)EditorGUILayout.ObjectField("Portal Display (RawImage)", portalDisplay, typeof(RawImage), true);
        portalMaterial = (Material)EditorGUILayout.ObjectField("Portal Material", portalMaterial, typeof(Material), true);

        GUILayout.Space(20);

        if (GUILayout.Button("Auto-Find References"))
        {
            AutoFindReferences();
        }

        GUILayout.Space(10);

        bool canCreate = leftHand != null && rightHand != null && canvasRect != null;

        EditorGUI.BeginDisabledGroup(!canCreate);
        if (GUILayout.Button("Create Portal Manager", GUILayout.Height(30)))
        {
            CreatePortalManager();
        }
        EditorGUI.EndDisabledGroup();

        if (!canCreate)
        {
            EditorGUILayout.HelpBox(
                "Please assign at minimum:\n- Left Hand\n- Right Hand\n- Canvas RectTransform",
                MessageType.Warning);
        }
    }

    private void CreatePortalManager()
    {
        // Check if one already exists
        var existing = FindObjectOfType<PinchPortalManager>();
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Portal Manager Exists",
                "A PinchPortalManager already exists in the scene. Do you want to update its references?",
                "Update", "Cancel"))
            {
                return;
            }
            SetupManager(existing);
            return;
        }

        // Create new GameObject with PinchPortalManager
        GameObject managerGO = new GameObject("[PinchPortalManager]");
        PinchPortalManager manager = managerGO.AddComponent<PinchPortalManager>();
        SetupManager(manager);

        // Select the new object
        Selection.activeGameObject = managerGO;
        
        EditorUtility.DisplayDialog("Success", 
            "PinchPortalManager has been created and configured!\n\n" +
            "Make sure to:\n" +
            "1. Apply the MultiPortalMaterial to your video RawImage\n" +
            "2. Test in VR with hand tracking enabled",
            "OK");
    }

    private void SetupManager(PinchPortalManager manager)
    {
        // Use SerializedObject to set private serialized fields
        SerializedObject serializedManager = new SerializedObject(manager);
        
        serializedManager.FindProperty("leftHand").objectReferenceValue = leftHand;
        serializedManager.FindProperty("rightHand").objectReferenceValue = rightHand;
        serializedManager.FindProperty("leftSkeleton").objectReferenceValue = leftSkeleton;
        serializedManager.FindProperty("rightSkeleton").objectReferenceValue = rightSkeleton;
        serializedManager.FindProperty("canvasRect").objectReferenceValue = canvasRect;
        serializedManager.FindProperty("portalDisplay").objectReferenceValue = portalDisplay;
        serializedManager.FindProperty("portalMaterial").objectReferenceValue = portalMaterial;
        
        serializedManager.ApplyModifiedProperties();
        
        EditorUtility.SetDirty(manager);
        
        Debug.Log("[PinchPortalSetupEditor] PinchPortalManager configured successfully!");
    }
}
#endif

