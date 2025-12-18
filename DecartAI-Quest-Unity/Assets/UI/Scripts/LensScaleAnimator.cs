using DG.Tweening;
using UnityEngine;

public class LensScaleAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Animation duration in seconds")]
    public float speed = 1.0f;
    
    [Tooltip("Target scale size. If set to (0,0,0), uses the GameObject's current scale at Start")]
    public Vector3 finalSize = Vector3.zero;
    
    private Vector3 _targetScale;
    private Tween _scaleTween;
    private bool _isInitialized = false;

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            Initialize();
        }
        
        // Only animate if the GameObject is becoming active
        if (gameObject.activeSelf)
        {
            AnimateScale();
        }
    }

    private void OnDisable()
    {
        // Kill any ongoing animation when disabled
        if (_scaleTween != null)
        {
            _scaleTween.Kill();
            _scaleTween = null;
        }
    }

    private void Initialize()
    {
        // Determine the target scale
        if (finalSize != Vector3.zero)
        {
            _targetScale = finalSize;
        }
        else
        {
            // Use current scale as the target
            _targetScale = transform.localScale;
        }
        
        _isInitialized = true;
    }

    private void AnimateScale()
    {
        // Kill any existing animation
        if (_scaleTween != null)
        {
            _scaleTween.Kill();
        }

        // Set initial scale to zero
        transform.localScale = Vector3.zero;

        // Animate to target scale
        _scaleTween = transform.DOScale(_targetScale, speed)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                _scaleTween = null;
            });
    }
}
