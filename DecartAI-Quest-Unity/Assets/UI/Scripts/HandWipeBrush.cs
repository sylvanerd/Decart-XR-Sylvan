using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class HandWipeBrush : MonoBehaviour
{
    [Header("Physics Settings")]
    [Tooltip("Only objects with this tag will trigger the wipe effect.")]
    public string targetTag = "Hand";
    [Tooltip("Thickness of the trigger volume (in meters).")]
    public float triggerThickness = 0.05f;

    [Header("Brush Settings")]
    [Tooltip("Size of the reveal brush in UV units (0 to 1).")]
    public float brushSize = 0.05f;
    [Tooltip("How fast the reveal fades out (0 = never, 1 = fast).")]
    public float fadeSpeed = 0.5f;

    [Header("Resolution")]
    public int maskResolution = 512;

    [Header("Debug")]
    public bool showDebugLogs = true;
    [Tooltip("Test mode: fill entire mask with white on start")]
    public bool testFillWhite = false;
    [Tooltip("Debug: fill entire texture white on ANY touch (to verify stamp pass works)")]
    public bool debugFillOnTouch = false;

    [Header("Shader Reference (REQUIRED for Quest builds)")]
    [Tooltip("Drag the WipeBrushStamp shader here from Assets/UI/Shaders/")]
    public Shader brushShader;
    
    private Material _brushMaterial;
    private RenderTexture _maskRT;
    private RawImage _targetDisplay;
    private RectTransform _targetRect;
    private BoxCollider _triggerCollider;
    private Rigidbody _rb;

    private void Awake()
    {
        // Ensure Rigidbody is kinematic
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
    }

    private void Start()
    {
        InitializeBrush();
        InitializeMask();
    }

    private void InitializeBrush()
    {
        if (brushShader == null)
        {
            // Try to find via Shader.Find (works in Editor, often fails in builds)
            brushShader = Shader.Find("Hidden/WipeBrushStamp");
            if (brushShader == null)
            {
                Debug.LogError("[HandWipeBrush] ========================================");
                Debug.LogError("[HandWipeBrush] SHADER NOT FOUND!");
                Debug.LogError("[HandWipeBrush] Please assign the shader manually:");
                Debug.LogError("[HandWipeBrush] 1. Select this GameObject in the Inspector");
                Debug.LogError("[HandWipeBrush] 2. Find 'Brush Shader' field");
                Debug.LogError("[HandWipeBrush] 3. Drag 'WipeBrushStamp' from Assets/UI/Shaders/");
                Debug.LogError("[HandWipeBrush] ========================================");
            }
            else if (showDebugLogs)
            {
                Debug.Log("[HandWipeBrush] Found brush shader via Shader.Find");
            }
        }
        else if (showDebugLogs)
        {
            Debug.Log("[HandWipeBrush] Using manually assigned brush shader");
        }

        if (brushShader != null)
        {
            _brushMaterial = new Material(brushShader);
            if (showDebugLogs)
                Debug.Log($"[HandWipeBrush] SUCCESS: Created brush material");
        }
    }

    private void InitializeMask()
    {
        _maskRT = new RenderTexture(maskResolution, maskResolution, 0, RenderTextureFormat.ARGB32);
        _maskRT.useMipMap = false;
        _maskRT.filterMode = FilterMode.Bilinear;
        _maskRT.wrapMode = TextureWrapMode.Clamp;
        _maskRT.Create();

        RenderTexture.active = _maskRT;
        if (testFillWhite)
        {
            GL.Clear(true, true, Color.white);
            if (showDebugLogs)
                Debug.Log("[HandWipeBrush] TEST MODE: Filled mask with WHITE");
        }
        else
        {
            GL.Clear(true, true, Color.black);
        }
        RenderTexture.active = null;
        
        if (showDebugLogs)
            Debug.Log($"[HandWipeBrush] Created mask RenderTexture: {_maskRT.width}x{_maskRT.height}");
    }

    private void Update()
    {
        EnsureTargetDisplay();

        if (_targetDisplay == null) return;

        UpdateMask();
        UpdateColliderBounds();
    }

    private void EnsureTargetDisplay()
    {
        if (_targetDisplay != null) return;

        _targetDisplay = GetComponentInChildren<RawImage>();
        if (_targetDisplay != null)
        {
            _targetRect = _targetDisplay.rectTransform;
            SetupTriggerCollider();
        }
    }

    private void SetupTriggerCollider()
    {
        _triggerCollider = GetComponent<BoxCollider>();
        if (_triggerCollider == null)
        {
            _triggerCollider = gameObject.AddComponent<BoxCollider>();
        }

        _triggerCollider.isTrigger = true;
        UpdateColliderBounds();

        if (showDebugLogs)
            Debug.Log($"[HandWipeBrush] Setup collider on parent {gameObject.name} to track child {_targetDisplay.name}");
    }

    private void UpdateColliderBounds()
    {
        if (_targetRect == null || _triggerCollider == null) return;

        Vector3 localPosOfChild = transform.InverseTransformPoint(_targetRect.position);
        float worldWidth = _targetRect.rect.width * _targetRect.lossyScale.x;
        float worldHeight = _targetRect.rect.height * _targetRect.lossyScale.y;
        Vector3 pScale = transform.lossyScale;
        
        _triggerCollider.size = new Vector3(worldWidth / pScale.x, worldHeight / pScale.y, triggerThickness / pScale.z);
        
        Vector3 pivotOffset = new Vector3(
            (0.5f - _targetRect.pivot.x) * (worldWidth / pScale.x),
            (0.5f - _targetRect.pivot.y) * (worldHeight / pScale.y),
            0
        );
        
        _triggerCollider.center = localPosOfChild + pivotOffset;
    }

    private void UpdateMask()
    {
        if (_maskRT == null || _targetDisplay == null) return;

        // Ensure the mask is assigned EVERY frame to the material
        if (_targetDisplay.material != null)
        {
            _targetDisplay.material.SetTexture("_WipeMask", _maskRT);
        }

        // Run fade pass if we have the brush material
        if (_brushMaterial != null)
        {
            // Pass 0: Fade
            _brushMaterial.SetFloat("_FadeSpeed", fadeSpeed);
            RenderTexture temp = RenderTexture.GetTemporary(_maskRT.descriptor);
            Graphics.Blit(_maskRT, temp, _brushMaterial, 0);
            Graphics.Blit(temp, _maskRT);
            RenderTexture.ReleaseTemporary(temp);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        bool isHand = other.CompareTag(targetTag);
        
        if (showDebugLogs && Time.frameCount % 60 == 0)
            Debug.Log($"[HandWipeBrush] Contact: {other.name} (Tag: {other.tag}). Is Target: {isHand}");

        if (!isHand || _targetRect == null || _triggerCollider == null) return;

        // Get the closest point ON THE HAND COLLIDER to the canvas center
        // This gives us the actual contact point (fingertip), not the bone pivot (wrist)
        Vector3 contactPointOnHand = other.ClosestPoint(_triggerCollider.bounds.center);
        
        // Convert that world point to UV
        Vector3 localPos = _targetRect.InverseTransformPoint(contactPointOnHand);

        float width = _targetRect.rect.width;
        float height = _targetRect.rect.height;
        
        float u = (localPos.x / width) + _targetRect.pivot.x;
        float v = (localPos.y / height) + _targetRect.pivot.y;

        if (u >= 0 && u <= 1 && v >= 0 && v <= 1)
        {
            StampMask(u, v);
            if (showDebugLogs && Time.frameCount % 30 == 0)
                Debug.Log($"[HandWipeBrush] Wiping at UV: ({u:F2}, {v:F2}) from {other.name}");
        }
    }

    private void StampMask(float u, float v)
    {
        // Silently fail if not properly initialized (error already shown at Start)
        if (_maskRT == null || _brushMaterial == null)
        {
            return;
        }

        // Set debug flag to fill entire texture white (for testing if pass runs at all)
        _brushMaterial.SetFloat("_DebugFillWhite", debugFillOnTouch ? 1.0f : 0.0f);
        _brushMaterial.SetVector("_BrushPos", new Vector4(u, v, 0, 0));
        _brushMaterial.SetFloat("_BrushSize", brushSize);
        
        RenderTexture temp = RenderTexture.GetTemporary(_maskRT.descriptor);
        Graphics.Blit(_maskRT, temp, _brushMaterial, 1); // Pass 1: Stamp
        Graphics.Blit(temp, _maskRT);
        RenderTexture.ReleaseTemporary(temp);
        
        if (showDebugLogs && Time.frameCount % 60 == 0)
            Debug.Log($"[HandWipeBrush] Stamp executed at UV ({u:F2}, {v:F2}). BrushSize: {brushSize}");
    }

    private void OnDestroy()
    {
        if (_maskRT != null)
        {
            _maskRT.Release();
        }
        if (_brushMaterial != null)
        {
            Destroy(_brushMaterial);
        }
    }
}
