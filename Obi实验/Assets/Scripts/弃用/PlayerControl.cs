
using UnityEngine;
using System;

namespace Obi
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Animator))]
    public class PlayerControl : MonoBehaviour
    {
        [Header("Movement Parameters")]
        [SerializeField] float m_MovingTurnSpeed = 360;
        [SerializeField] float m_StationaryTurnSpeed = 180;
        [SerializeField] float m_JumpPower = 12f;
        [Range(1f, 4f)] [SerializeField] float m_GravityMultiplier = 2f;
        [SerializeField] float m_MoveSpeedMultiplier = 1f;
        [SerializeField] float m_AnimSpeedMultiplier = 1f;
        [SerializeField] float m_GroundCheckDistance = 0.1f;

        [Header("Animation Control")]
        [Tooltip("勾选此项，角色在空中时将不会播放Airborne动画")]
        public bool disableAirborneAnimation = false;

        [SerializeField] float m_RunCycleLegOffset = 0.2f; // 特定的动画周期偏移

        private Rigidbody m_Rigidbody;
        private Animator m_Animator;
        private CapsuleCollider m_Capsule;

        private bool m_IsGrounded;
        private bool m_Crouching; // 虽然当前没有输入，但保留逻辑

        private float m_OrigGroundCheckDistance;
        private float m_TurnAmount;
        private float m_ForwardAmount;
        private Vector3 m_GroundNormal;
        private float m_CapsuleHeight;
        private Vector3 m_CapsuleCenter;

        private const float k_Half = 0.5f;

        // --- 输入变量 ---
        private Vector2 m_MoveInput;
        private bool m_JumpInput;

        private void Start()
        {
            m_Animator = GetComponent<Animator>();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Capsule = GetComponent<CapsuleCollider>();

            m_CapsuleHeight = m_Capsule.height;
            m_CapsuleCenter = m_Capsule.center;

            m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            m_OrigGroundCheckDistance = m_GroundCheckDistance;
        }

        private void OnEnable()
        {
            if (PlayerInputManager.instance != null)
            {
                PlayerInputManager.instance.OnOnMove += HandleMoveInput;
                PlayerInputManager.instance.OnOnJump += HandleJumpInput;
            }
            else
            {
                Debug.LogError("场景中未找到 PlayerInputManager 实例！请确保已将其添加到场景中。");
                // 如果需要，可以禁用此组件以防止错误
                this.enabled = false;
            }
        }

        private void OnDisable()
        {
            // 安全地取消订阅
            if (PlayerInputManager.instance != null)
            {
                PlayerInputManager.instance.OnOnMove -= HandleMoveInput;
                PlayerInputManager.instance.OnOnJump -= HandleJumpInput;
            }
        }

        // --- 输入处理函数 ---
        private void HandleMoveInput(Vector2 direction)
        {
            m_MoveInput = direction;
        }

        private void HandleJumpInput()
        {
            m_JumpInput = true;
        }

        private void FixedUpdate()
        {
            // 将输入转换为角色需要的移动向量
            Vector3 move = new Vector3(m_MoveInput.x, 0, m_MoveInput.y);

            // 调用核心移动逻辑
            Move(move, m_Crouching, m_JumpInput);

            // 重置跳跃输入，确保只跳一次
            m_JumpInput = false;

            // 清除移动输入，以便在没有按键时角色停止
            m_MoveInput = Vector2.zero;
        }

        public void Move(Vector3 move, bool crouch, bool jump)
        {
            if (move.magnitude > 1f) move.Normalize();

            move = transform.InverseTransformDirection(move);
            CheckGroundStatus();
            move = Vector3.ProjectOnPlane(move, m_GroundNormal);

            m_TurnAmount = Mathf.Atan2(move.x, move.z);
            m_ForwardAmount = move.z;

            ApplyExtraTurnRotation();

            if (m_IsGrounded)
            {
                HandleGroundedMovement(crouch, jump);
            }
            else
            {
                HandleAirborneMovement();
            }

            // 保留蹲伏逻辑（如果未来需要）
            ScaleCapsuleForCrouching(crouch);
            PreventStandingInLowHeadroom();

            UpdateAnimator(move);
        }

        void UpdateAnimator(Vector3 move)
        {
            m_Animator.SetFloat("Forward", m_ForwardAmount, 0.1f, Time.deltaTime);
            m_Animator.SetFloat("Turn", m_TurnAmount, 0.1f, Time.deltaTime);
            m_Animator.SetBool("Crouch", m_Crouching);
            m_Animator.SetBool("OnGround", m_IsGrounded);

            // 根据开关决定是否更新空中动画
            if (!m_IsGrounded)
            {
                if (!disableAirborneAnimation)
                {
                    m_Animator.SetFloat("Jump", m_Rigidbody.linearVelocity.y);
                }
                else
                {
                    // 如果禁用了空中动画，可以将跳跃值设为0或保持不变
                    m_Animator.SetFloat("Jump", 0);
                }
            }

            float runCycle = Mathf.Repeat(m_Animator.GetCurrentAnimatorStateInfo(0).normalizedTime + m_RunCycleLegOffset, 1);
            float jumpLeg = (runCycle < k_Half ? 1 : -1) * m_ForwardAmount;
            if (m_IsGrounded)
            {
                m_Animator.SetFloat("JumpLeg", jumpLeg);
            }

            if (m_IsGrounded && move.magnitude > 0)
            {
                m_Animator.speed = m_AnimSpeedMultiplier;
            }
            else
            {
                m_Animator.speed = 1;
            }
        }

        void HandleGroundedMovement(bool crouch, bool jump)
        {
            if (jump && !crouch && m_Animator.GetCurrentAnimatorStateInfo(0).IsName("Grounded"))
            {
                m_Rigidbody.linearVelocity = new Vector3(m_Rigidbody.linearVelocity.x, m_JumpPower, m_Rigidbody.linearVelocity.z);
                m_IsGrounded = false;
                m_Animator.applyRootMotion = false;
                m_GroundCheckDistance = 0.1f;
            }
        }

        void HandleAirborneMovement()
        {
            Vector3 extraGravityForce = (Physics.gravity * m_GravityMultiplier) - Physics.gravity;
            m_Rigidbody.AddForce(extraGravityForce);
            m_GroundCheckDistance = m_Rigidbody.linearVelocity.y < 0 ? m_OrigGroundCheckDistance : 0.01f;
        }

        void CheckGroundStatus()
        {
            RaycastHit hitInfo;
#if UNITY_EDITOR
            Debug.DrawLine(transform.position + (Vector3.up * 0.1f), transform.position + (Vector3.up * 0.1f) + (Vector3.down * m_GroundCheckDistance));
#endif
            if (Physics.Raycast(transform.position + (Vector3.up * 0.1f), Vector3.down, out hitInfo, m_GroundCheckDistance, ~Physics.IgnoreRaycastLayer))
            {
                m_GroundNormal = hitInfo.normal;
                m_IsGrounded = true;
                m_Animator.applyRootMotion = true;
            }
            else
            {
                m_IsGrounded = false;
                m_GroundNormal = Vector3.up;
                m_Animator.applyRootMotion = false;
            }
        }

        public void OnAnimatorMove()
        {
            if (m_IsGrounded)
            {
                Vector3 v = (m_Animator.deltaPosition * m_MoveSpeedMultiplier) / Time.deltaTime;
                v.y = m_Rigidbody.linearVelocity.y;
                m_Rigidbody.linearVelocity = v;
            }
        }

        void ApplyExtraTurnRotation()
        {
            float turnSpeed = Mathf.Lerp(m_StationaryTurnSpeed, m_MovingTurnSpeed, m_ForwardAmount);
            transform.Rotate(0, m_TurnAmount * turnSpeed * Time.deltaTime, 0);
        }
        
        // --- 以下是保留的蹲伏逻辑，当前未启用输入，但代码保留以便未来扩展 ---
        void ScaleCapsuleForCrouching(bool crouch)
        {
            if (m_IsGrounded && crouch)
            {
                if (m_Crouching) return;
                m_Capsule.height = m_Capsule.height / 2f;
                m_Capsule.center = m_Capsule.center / 2f;
                m_Crouching = true;
            }
            else
            {
                Ray crouchRay = new Ray(m_Rigidbody.position + Vector3.up * m_Capsule.radius * k_Half, Vector3.up);
                float crouchRayLength = m_CapsuleHeight - m_Capsule.radius * k_Half;
                if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength, ~Physics.IgnoreRaycastLayer, QueryTriggerInteraction.Ignore))
                {
                    m_Crouching = true;
                    return;
                }
                m_Capsule.height = m_CapsuleHeight;
                m_Capsule.center = m_CapsuleCenter;
                m_Crouching = false;
            }
        }

        void PreventStandingInLowHeadroom()
        {
            if (!m_Crouching)
            {
                Ray crouchRay = new Ray(m_Rigidbody.position + Vector3.up * m_Capsule.radius * k_Half, Vector3.up);
                float crouchRayLength = m_CapsuleHeight - m_Capsule.radius * k_Half;
                if (Physics.SphereCast(crouchRay, m_Capsule.radius * k_Half, crouchRayLength, ~Physics.IgnoreRaycastLayer, QueryTriggerInteraction.Ignore))
                {
                    m_Crouching = true;
                }
            }
        }
    }
}
