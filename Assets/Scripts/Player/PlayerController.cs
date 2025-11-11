using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace FF
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] AutoShooter autoShooter;
        [SerializeField] Transform gunPivot;
        [SerializeField] Transform playerVisual;

        [SerializeField] float acceleration = 0.18f;
        [SerializeField] float bodyTiltDegrees = 15f;

        Rigidbody2D rb;
        PlayerStats stats;
        Vector2 moveInput;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            stats = GetComponent<PlayerStats>();
            if (!cam) cam = Camera.main;
        }

        void Update()
        {
            AimGunAtMouse();
        }

        void FixedUpdate()
        {
            float targetSpeed = stats.GetMoveSpeed();
            Vector2 targetVelocity = moveInput.normalized * targetSpeed;

            // Smooth acceleration/deceleration
            rb.linearVelocity = Vector2.Lerp(
                rb.linearVelocity,
                targetVelocity,
                acceleration  
            );

            HandleBodyTilt();
        }

        void HandleBodyTilt()
        {
            if (!playerVisual) return;

            float speed = rb.linearVelocity.magnitude;
            float maxSpeed = stats.GetMoveSpeed();

            float normalized = speed / maxSpeed; // 0 to 1

            float targetTilt;

            // Logic for tilt behavior
            if (speed > 0.1f)
            {
                // If accelerating, tilt backward
                targetTilt = -bodyTiltDegrees * normalized;
            }
            else
            {
                // When stopping, tilt forward slightly before settling
                targetTilt = bodyTiltDegrees * 0.3f;
            }

            float horizontal = moveInput.x;

            // Lean left/right slightly based on horizontal movement
            float sideTilt = horizontal * (bodyTiltDegrees * 0.5f);

            targetTilt += sideTilt;

            // Current rotation angle (in degrees)
            float currentZ = playerVisual.localEulerAngles.z;

            // Convert range 0-360 to -180 to 180
            if (currentZ > 180f)
                currentZ -= 360f;

            // Smooth transition
            float newZ = Mathf.Lerp(currentZ, targetTilt, 0.15f);
            playerVisual.localRotation = Quaternion.Euler(0, 0, newZ);
        }

        void AimGunAtMouse()
        {
            if (!gunPivot) return;

            Vector3 mousePos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector2 dir = mousePos - gunPivot.position;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            gunPivot.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            // Flip gun sprite if aiming left
            if (dir.x < 0)
            {
                gunPivot.localScale = new Vector3(1, -1, 1);
                playerVisual.localScale = new Vector3(-1, 1, 1);
            }
            else
            {
                gunPivot.localScale = new Vector3(1, 1, 1);
                playerVisual.localScale = new Vector3(1, 1, 1);
            }
        }

        public void OnMove(InputValue v) => moveInput = v.Get<Vector2>();

        public void OnAttack(InputValue v)
        {
            autoShooter.OnFire(v);
        }

    }
}
