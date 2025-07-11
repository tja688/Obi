// Filename: PlayerControl_Ball.cs
// MODIFIED TO FIX MOVEMENT DIRECTION ISSUE

using UnityEngine;
using UnityEngine.InputSystem;
using Obi;
using System.Linq;

public class PlayerControl_Ball : MonoBehaviour
{
    #region Singleton
    public static PlayerControl_Ball instance { get; private set; }
    #endregion

    [Header("Movement Settings")]
    public Transform referenceFrame; // 【重要】请确保在Inspector中将你的主摄像机拖拽到这里
    public float acceleration = 80;
    public float jumpPower = 1;
    [Range(0, 1)]
    public float airControl = 0.3f;

    [Header("Obi Settings")]
    public ObiActor actor;
    public Transform actorTrans = null;
    public Vector3 offset;
    
    // --- 私有变量 ---
    // private Vector3 moveDirection; // <-- 不再需要这个成员变量
    private Vector2 moveInput; // <-- 【修改1】将输入值保存为成员变量，而不是OnMove里的局部变量
    private ObiSoftbody softbody;
    private bool onGround = false;

    public Vector2 LookInput { get; private set; }
    public ObiSolver playerSolver { get; private set; }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        softbody = GetComponent<ObiSoftbody>();
        if (softbody != null && softbody.solver != null)
        {
            playerSolver = softbody.solver;
            playerSolver.OnCollision += Solver_OnCollision;
        }
    }
    
    private void OnDestroy()
    {
        if (softbody != null && softbody.solver != null)
        {
            softbody.solver.OnCollision -= Solver_OnCollision;
        }
    }

    #region New Input System Handlers

    /// <summary>
    /// 当 "Move" Action被触发时调用
    /// </summary>
    private void OnMove(InputValue value)
    {
        // 【修改2】OnMove现在只负责一件事：更新当前的输入状态
        moveInput = value.Get<Vector2>();
    }

    /// <summary>
    /// 当 "Jump" Action被触发时调用
    /// </summary>
    private void OnJump(InputValue value)
    {
        if (!onGround) return;
        onGround = false;
        softbody.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
    }

    /// <summary>
    /// 当 "Look" Action被触发时调用，用于更新鼠标输入
    /// </summary>
    private void OnLook(InputValue value)
    {
        LookInput = value.Get<Vector2>();
    }

    #endregion

    private void LateUpdate()
    {
        if (!actor || !actor.isLoaded || !actorTrans) return;
        
        actor.GetMass(out var com);
        actorTrans.position = actor.solver.transform.TransformPoint(com) + offset;
    }

    private void FixedUpdate()
    {
        // 【修改3】将移动方向的计算逻辑移到FixedUpdate中
        // 这样可以确保每一帧物理更新，都使用最新的摄像机朝向来计算移动方向

        if (referenceFrame == null) return;

        // 1. 根据保存的输入值和当前摄像机的朝向，计算出目标移动方向
        var relativeMove = (referenceFrame.forward * moveInput.y + referenceFrame.right * moveInput.x);
        relativeMove.y = 0; // 保持移动在水平面上

        var effectiveAcceleration = onGround ? acceleration : acceleration * airControl;
        Vector3 moveDirection = relativeMove.normalized * effectiveAcceleration;

        // 2. 如果有移动输入，则施加力
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            softbody.AddForce(moveDirection, ForceMode.Acceleration);
        }
    }

    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList e)
    {
        onGround = false;
        var world = ObiColliderWorld.GetInstance();
        foreach (var contact in e)
        {
            if (contact.distance > 0.01f)
            {
                var col = world.colliderHandles[contact.bodyB].owner;
                if (col != null)
                {
                    onGround = true;
                    break; 
                }
            }
        }
    }
}