using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages creation of multiple portals via two-handed pinch-and-drag gestures.
/// Detects when both hands are pinching near the canvas and tracks their positions
/// to create rectangular portal windows that reveal the AI video underneath.
/// </summary>
public class PinchPortalManager : MonoBehaviour
{
    [Header("Hand Tracking References")]
    [Tooltip("Reference to the left OVRHand component")]
    [SerializeField] private OVRHand leftHand;
    
    [Tooltip("Reference to the right OVRHand component")]
    [SerializeField] private OVRHand rightHand;
    
    [Tooltip("Reference to the left OVRSkeleton for bone positions")]
    [SerializeField] private OVRSkeleton leftSkeleton;
    
    [Tooltip("Reference to the right OVRSkeleton for bone positions")]
    [SerializeField] private OVRSkeleton rightSkeleton;

    [Header("Canvas References")]
    [Tooltip("The RectTransform of the canvas to create portals on")]
    [SerializeField] private RectTransform canvasRect;
    
    [Tooltip("The RawImage that displays the video with portal shader")]
    [SerializeField] private RawImage portalDisplay;
    
    [Tooltip("Material using MultiPortalDisplay shader")]
    [SerializeField] private Material portalMaterial;

    [Header("Portal Settings")]
    [Tooltip("Maximum number of portals allowed")]
    [SerializeField] private int maxPortals = 8;
    
    [Tooltip("Minimum portal size in UV units before it's considered valid")]
    [SerializeField] private float minPortalSize = 0.05f;
    
    [Tooltip("Corner radius for portal rectangles")]
    [SerializeField] private float portalCornerRadius = 0.02f;
    
    [Tooltip("Maximum distance from canvas (in meters) for hand to be considered 'touching'")]
    [SerializeField] private float canvasTouchDistance = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    // Portal tracking
    private List<PortalRect> _portals = new List<PortalRect>();
    private PortalRect _activePortal;
    private bool _isCreatingPortal;
    
    // Shader property IDs for performance
    private static readonly int PortalRectsId = Shader.PropertyToID("_PortalRects");
    private static readonly int PortalRadiiId = Shader.PropertyToID("_PortalRadii");
    private static readonly int PortalCountId = Shader.PropertyToID("_PortalCount");

    // Cached arrays for shader upload
    private Vector4[] _portalRectsArray = new Vector4[8];
    private float[] _portalRadiiArray = new float[8];

    private void Start()
    {
        if (portalMaterial == null && portalDisplay != null)
        {
            portalMaterial = portalDisplay.material;
        }
        
        ValidateReferences();
        UpdateShaderPortals();
    }

    private void Update()
    {
        if (!ValidateReferences()) return;

        bool leftPinching = IsHandPinching(leftHand);
        bool rightPinching = IsHandPinching(rightHand);
        
        Vector3 leftFingerPos = GetIndexFingerTipPosition(leftSkeleton);
        Vector3 rightFingerPos = GetIndexFingerTipPosition(rightSkeleton);
        
        bool leftNearCanvas = IsPositionNearCanvas(leftFingerPos);
        bool rightNearCanvas = IsPositionNearCanvas(rightFingerPos);

        // Both hands pinching near canvas - create or update portal
        if (leftPinching && rightPinching && leftNearCanvas && rightNearCanvas)
        {
            Vector2 leftUV = WorldPositionToCanvasUV(leftFingerPos);
            Vector2 rightUV = WorldPositionToCanvasUV(rightFingerPos);

            if (!_isCreatingPortal)
            {
                // Start creating a new portal
                StartPortalCreation(leftUV, rightUV);
            }
            else
            {
                // Update the active portal being created
                UpdateActivePortal(leftUV, rightUV);
            }
        }
        else if (_isCreatingPortal)
        {
            // One or both hands released - finalize the portal
            FinalizePortalCreation();
        }
    }

    private void StartPortalCreation(Vector2 corner1, Vector2 corner2)
    {
        if (_portals.Count >= maxPortals)
        {
            if (showDebugLogs)
                Debug.Log($"[PinchPortalManager] Max portals ({maxPortals}) reached. Cannot create more.");
            return;
        }

        _activePortal = new PortalRect(corner1, corner2, portalCornerRadius);
        _isCreatingPortal = true;

        if (showDebugLogs)
            Debug.Log($"[PinchPortalManager] Started creating portal at corners ({corner1}, {corner2})");

        // Add to list for immediate visual feedback
        _portals.Add(_activePortal);
        UpdateShaderPortals();
    }

    private void UpdateActivePortal(Vector2 corner1, Vector2 corner2)
    {
        if (_activePortal == null) return;

        _activePortal.UpdateCorners(corner1, corner2);
        UpdateShaderPortals();

        if (showDebugLogs && Time.frameCount % 30 == 0)
            Debug.Log($"[PinchPortalManager] Updating portal: min={_activePortal.uvMin}, max={_activePortal.uvMax}");
    }

    private void FinalizePortalCreation()
    {
        _isCreatingPortal = false;

        if (_activePortal == null) return;

        // Check if portal meets minimum size requirements
        if (!_activePortal.IsValidSize(minPortalSize))
        {
            if (showDebugLogs)
                Debug.Log($"[PinchPortalManager] Portal too small ({_activePortal.Size}), removing.");
            
            _portals.Remove(_activePortal);
            UpdateShaderPortals();
        }
        else
        {
            if (showDebugLogs)
                Debug.Log($"[PinchPortalManager] Portal finalized! Size: {_activePortal.Size}, Total portals: {_portals.Count}");
        }

        _activePortal = null;
    }

    private void UpdateShaderPortals()
    {
        if (portalMaterial == null) return;

        int count = Mathf.Min(_portals.Count, maxPortals);

        for (int i = 0; i < maxPortals; i++)
        {
            if (i < count && _portals[i].isActive)
            {
                _portalRectsArray[i] = _portals[i].ToShaderVector();
                _portalRadiiArray[i] = _portals[i].cornerRadius;
            }
            else
            {
                _portalRectsArray[i] = Vector4.zero;
                _portalRadiiArray[i] = 0f;
            }
        }

        portalMaterial.SetVectorArray(PortalRectsId, _portalRectsArray);
        portalMaterial.SetFloatArray(PortalRadiiId, _portalRadiiArray);
        portalMaterial.SetInt(PortalCountId, count);
    }

    /// <summary>
    /// Checks if the specified hand is performing a pinch gesture.
    /// </summary>
    private bool IsHandPinching(OVRHand hand)
    {
        if (hand == null || !hand.IsTracked) return false;
        return hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
    }

    /// <summary>
    /// Gets the world position of the index finger tip from the skeleton.
    /// </summary>
    private Vector3 GetIndexFingerTipPosition(OVRSkeleton skeleton)
    {
        if (skeleton == null || skeleton.Bones == null || skeleton.Bones.Count == 0)
            return Vector3.zero;

        // OVRSkeleton.BoneId.Hand_IndexTip is the index fingertip
        foreach (var bone in skeleton.Bones)
        {
            if (bone.Id == OVRSkeleton.BoneId.Hand_IndexTip)
            {
                return bone.Transform.position;
            }
        }

        // Fallback: try to find by index (IndexTip is typically bone 20)
        int tipIndex = (int)OVRSkeleton.BoneId.Hand_IndexTip;
        if (tipIndex < skeleton.Bones.Count)
        {
            return skeleton.Bones[tipIndex].Transform.position;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// Checks if a world position is close enough to the canvas to be considered "touching".
    /// </summary>
    private bool IsPositionNearCanvas(Vector3 worldPos)
    {
        if (canvasRect == null || worldPos == Vector3.zero) return false;

        // Get the canvas plane
        Vector3 canvasPos = canvasRect.position;
        Vector3 canvasNormal = canvasRect.forward;

        // Calculate distance from point to canvas plane
        float distance = Mathf.Abs(Vector3.Dot(worldPos - canvasPos, canvasNormal));

        return distance <= canvasTouchDistance;
    }

    /// <summary>
    /// Converts a world position to UV coordinates on the canvas (0-1 range).
    /// </summary>
    private Vector2 WorldPositionToCanvasUV(Vector3 worldPos)
    {
        if (canvasRect == null) return Vector2.zero;

        // Transform world position to local canvas space
        Vector3 localPos = canvasRect.InverseTransformPoint(worldPos);

        // Get canvas dimensions
        float width = canvasRect.rect.width;
        float height = canvasRect.rect.height;

        // Convert to UV (0-1 range), accounting for pivot
        float u = (localPos.x / width) + canvasRect.pivot.x;
        float v = (localPos.y / height) + canvasRect.pivot.y;

        // Clamp to valid UV range
        return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
    }

    /// <summary>
    /// Validates that all required references are assigned.
    /// </summary>
    private bool ValidateReferences()
    {
        bool valid = true;

        if (leftHand == null)
        {
            if (showDebugLogs && Time.frameCount % 300 == 0)
                Debug.LogWarning("[PinchPortalManager] Left OVRHand reference is missing.");
            valid = false;
        }

        if (rightHand == null)
        {
            if (showDebugLogs && Time.frameCount % 300 == 0)
                Debug.LogWarning("[PinchPortalManager] Right OVRHand reference is missing.");
            valid = false;
        }

        if (canvasRect == null)
        {
            if (showDebugLogs && Time.frameCount % 300 == 0)
                Debug.LogWarning("[PinchPortalManager] Canvas RectTransform reference is missing.");
            valid = false;
        }

        if (portalMaterial == null)
        {
            if (showDebugLogs && Time.frameCount % 300 == 0)
                Debug.LogWarning("[PinchPortalManager] Portal material reference is missing.");
            valid = false;
        }

        return valid;
    }

    /// <summary>
    /// Removes all portals.
    /// </summary>
    public void ClearAllPortals()
    {
        _portals.Clear();
        _activePortal = null;
        _isCreatingPortal = false;
        UpdateShaderPortals();

        if (showDebugLogs)
            Debug.Log("[PinchPortalManager] All portals cleared.");
    }

    /// <summary>
    /// Gets the current number of active portals.
    /// </summary>
    public int PortalCount => _portals.Count;

    /// <summary>
    /// Gets a read-only list of current portals.
    /// </summary>
    public IReadOnlyList<PortalRect> Portals => _portals.AsReadOnly();
}

