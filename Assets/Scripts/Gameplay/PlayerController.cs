using UnityEngine;
using BokeGameJam.Core;
using BokeGameJam.Input;

namespace BokeGameJam.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 7f;
        [SerializeField] private float airControlMultiplier = 1f;
        [SerializeField] private bool faceMoveDirection = true;
        [SerializeField] private bool freezeRotation = true;

        [Header("Jump")]
        [SerializeField] private float jumpForce = 12f;
        [SerializeField] private float coyoteTime = 0.08f;
        [SerializeField] private float jumpBufferTime = 0.1f;
        [SerializeField] private float maxFallSpeed = 18f;

        [Header("Ground Check")]
        [SerializeField] private LayerMask groundLayer = ~0;
        [SerializeField] private Vector2 groundCheckOffset = new(0f, -0.55f);
        [SerializeField] private float groundCheckRadius = 0.16f;

        private readonly Collider2D[] groundHits = new Collider2D[8];

        private Rigidbody2D body;
        private float moveInput;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpBufferedTime = float.NegativeInfinity;
        private float initialScaleX;
        private bool isGrounded;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            initialScaleX = Mathf.Approximately(transform.localScale.x, 0f) ? 1f : transform.localScale.x;

            if (freezeRotation)
                body.freezeRotation = true;
        }

        private void OnEnable()
        {
            EventManager.On<float>(InputEvents.PlayerMove, OnMove);
            EventManager.On(InputEvents.PlayerJumpPressed, OnJumpPressed);
        }

        private void OnDisable()
        {
            EventManager.Off<float>(InputEvents.PlayerMove, OnMove);
            EventManager.Off(InputEvents.PlayerJumpPressed, OnJumpPressed);

            // 组件被禁用（例如进入编辑模式）时清空输入，避免残留速度
            moveInput = 0f;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            airControlMultiplier = Mathf.Max(0f, airControlMultiplier);
            jumpForce = Mathf.Max(0f, jumpForce);
            coyoteTime = Mathf.Max(0f, coyoteTime);
            jumpBufferTime = Mathf.Max(0f, jumpBufferTime);
            maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
            groundCheckRadius = Mathf.Max(0.01f, groundCheckRadius);
        }

        private void Update()
        {
            UpdateFacing();
        }

        private void FixedUpdate()
        {
            isGrounded = CheckGrounded();
            if (isGrounded)
                lastGroundedTime = Time.time;

            ApplyHorizontalMovement();
            TryJump();
            ClampFallSpeed();
        }

        // ---------- 事件回调 ----------

        private void OnMove(float horizontal)
        {
            moveInput = Mathf.Clamp(horizontal, -1f, 1f);
        }

        private void OnJumpPressed()
        {
            jumpBufferedTime = Time.time;
        }

        // ---------- 物理 ----------

        private void ApplyHorizontalMovement()
        {
            float control = isGrounded ? 1f : airControlMultiplier;
            Vector2 velocity = body.velocity;
            velocity.x = moveInput * moveSpeed * control;
            body.velocity = velocity;
        }

        private void TryJump()
        {
            bool hasBufferedJump = Time.time <= jumpBufferedTime + jumpBufferTime;
            bool canJump = Time.time <= lastGroundedTime + coyoteTime;

            if (!hasBufferedJump || !canJump)
                return;

            Vector2 velocity = body.velocity;
            velocity.y = jumpForce;
            body.velocity = velocity;

            jumpBufferedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            isGrounded = false;
        }

        private void ClampFallSpeed()
        {
            if (body.velocity.y >= -maxFallSpeed)
                return;

            Vector2 velocity = body.velocity;
            velocity.y = -maxFallSpeed;
            body.velocity = velocity;
        }

        private bool CheckGrounded()
        {
            Vector2 checkPosition = (Vector2)transform.position + groundCheckOffset;
            int hitCount = Physics2D.OverlapCircleNonAlloc(checkPosition, groundCheckRadius, groundHits, groundLayer);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = groundHits[i];
                if (IsValidGroundHit(hit))
                    return true;
            }

            return false;
        }

        private bool IsValidGroundHit(Collider2D hit)
        {
            if (hit == null || hit.isTrigger)
                return false;

            if (hit.attachedRigidbody == body)
                return false;

            Transform hitTransform = hit.transform;
            return hitTransform != transform && !hitTransform.IsChildOf(transform);
        }

        private void UpdateFacing()
        {
            if (!faceMoveDirection || Mathf.Approximately(moveInput, 0f))
                return;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(initialScaleX) * Mathf.Sign(moveInput);
            transform.localScale = scale;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector2 checkPosition = (Vector2)transform.position + groundCheckOffset;
            Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
        }
    }
}
