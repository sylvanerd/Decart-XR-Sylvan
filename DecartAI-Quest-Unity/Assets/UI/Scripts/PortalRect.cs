using UnityEngine;

/// <summary>
/// Data structure representing a rectangular portal region in UV space.
/// Used by PinchPortalManager to track portal positions and pass data to shader.
/// </summary>
[System.Serializable]
public class PortalRect
{
    /// <summary>
    /// Bottom-left corner of the portal in UV space (0-1 range).
    /// </summary>
    public Vector2 uvMin;
    
    /// <summary>
    /// Top-right corner of the portal in UV space (0-1 range).
    /// </summary>
    public Vector2 uvMax;
    
    /// <summary>
    /// Corner radius for rounded rectangle appearance (in UV units).
    /// </summary>
    public float cornerRadius = 0.02f;
    
    /// <summary>
    /// Whether this portal is currently active/visible.
    /// </summary>
    public bool isActive = true;

    /// <summary>
    /// Creates a new portal rect from two corner points.
    /// Automatically normalizes so uvMin is always bottom-left and uvMax is top-right.
    /// </summary>
    public PortalRect(Vector2 corner1, Vector2 corner2, float radius = 0.02f)
    {
        uvMin = Vector2.Min(corner1, corner2);
        uvMax = Vector2.Max(corner1, corner2);
        cornerRadius = radius;
        isActive = true;
    }

    /// <summary>
    /// Default constructor for serialization.
    /// </summary>
    public PortalRect()
    {
        uvMin = Vector2.zero;
        uvMax = Vector2.zero;
        cornerRadius = 0.02f;
        isActive = false;
    }

    /// <summary>
    /// Returns the center point of the portal in UV space.
    /// </summary>
    public Vector2 Center => (uvMin + uvMax) * 0.5f;

    /// <summary>
    /// Returns the size of the portal in UV units.
    /// </summary>
    public Vector2 Size => uvMax - uvMin;

    /// <summary>
    /// Returns true if the portal meets minimum size requirements.
    /// </summary>
    public bool IsValidSize(float minSize = 0.05f)
    {
        Vector2 size = Size;
        return size.x >= minSize && size.y >= minSize;
    }

    /// <summary>
    /// Updates the portal corners, maintaining normalization.
    /// </summary>
    public void UpdateCorners(Vector2 corner1, Vector2 corner2)
    {
        uvMin = Vector2.Min(corner1, corner2);
        uvMax = Vector2.Max(corner1, corner2);
    }

    /// <summary>
    /// Packs portal data into a Vector4 for shader consumption.
    /// xy = uvMin, zw = uvMax
    /// </summary>
    public Vector4 ToShaderVector()
    {
        return new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y);
    }
}

