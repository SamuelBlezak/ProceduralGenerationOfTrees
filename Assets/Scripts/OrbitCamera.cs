using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Orbit kamera pre nový Input System.
/// 
/// Ovládanie:
///   - Stredné tlačidlo + ťahanie: otáčanie okolo stromu
///   - Scroll: priblíženie / oddialenie
///   - Pravé tlačidlo + ťahanie: posun kamery (pan)
///   - F: vycentrovanie na strom
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    [Tooltip("Cieľ okolo ktorého kamera rotuje")]
    public Transform Target;

    public float Distance = 8f;
    public float RotationSpeed = 0.3f;
    public float ZoomSpeed = 0.005f;
    public float PanSpeed = 0.003f;
    public float MinDistance = 1f;
    public float MaxDistance = 30f;

    private float _yaw = 30f;
    private float _pitch = 20f;
    private Vector3 _panOffset = Vector3.zero;

    void Start()
    {
        if (Target == null)
        {
            var treeGen = FindFirstObjectByType<TreeGenerator>();
            if (treeGen != null)
                Target = treeGen.transform;
        }

        if (Target != null)
        {
            Vector3 dir = transform.position - Target.position;
            Distance = Mathf.Max(dir.magnitude, MinDistance);
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Asin(Mathf.Clamp(dir.y / Distance, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }

    void LateUpdate()
    {
        if (Target == null) return;

        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();

        // Stredné tlačidlo — otáčanie
        if (mouse.middleButton.isPressed)
        {
            _yaw += delta.x * RotationSpeed;
            _pitch -= delta.y * RotationSpeed;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        }

        // Pravé tlačidlo — pan
        if (mouse.rightButton.isPressed)
        {
            _panOffset += transform.right * (-delta.x * PanSpeed * Distance);
            _panOffset += transform.up * (-delta.y * PanSpeed * Distance);
        }

        // Scroll — zoom (len bez Shift)
        bool shiftHeld = kb != null && kb.leftShiftKey.isPressed;
        if (!shiftHeld)
        {
            float scroll = mouse.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) > 0.1f)
            {
                Distance -= scroll * ZoomSpeed * Distance;
                Distance = Mathf.Clamp(Distance, MinDistance, MaxDistance);
            }
        }

        // F — vycentrovanie
        if (kb != null && kb.fKey.wasPressedThisFrame)
        {
            _panOffset = Vector3.zero;
            Distance = 8f;
        }

        // Pozícia kamery
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0);
        Vector3 offset = rotation * new Vector3(0, 0, -Distance);
        Vector3 targetPos = Target.position + _panOffset + Vector3.up * 2f;

        transform.position = targetPos + offset;
        transform.LookAt(targetPos);
    }
}