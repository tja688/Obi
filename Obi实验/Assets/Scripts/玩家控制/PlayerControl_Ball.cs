// Filename: PlayerControl_Ball.cs
// MODIFIED TO FIX MOVEMENT DIRECTION ISSUE & ADD ClearMove() METHOD

using UnityEngine;
using UnityEngine.InputSystem;
using Obi;
using System.Linq;
using UnityEngine.Serialization;

public class PlayerControl_Ball : MonoBehaviour
{
    #region Singleton
    public static PlayerControl_Ball instance { get; private set; }
    #endregion

    [Header("Movement Settings")]
    public Transform referenceFrame; 
    public float acceleration = 80;
    public float jumpPower = 1;
    [Range(0, 1)]
    public float airControl = 0.3f;

    [Header("Obi Settings")]
    public ObiActor actor;
    public Transform actorTrans = null;
    public Vector3 offset;
    
    public bool playerControl = true;
    
    // --- 私有变量 ---
    private Vector2 moveInput; 
    private ObiSoftbody softbody;
    private bool onGround = false;

    public Vector2 lookInput { get; private set; }
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
        if (softbody == null || softbody.solver == null) return;
        playerSolver = softbody.solver;
        playerSolver.OnCollision += Solver_OnCollision;
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
        lookInput = value.Get<Vector2>();
    }

    #endregion

    /// <summary>
    /// 【新增方法】清除软体的所有线速度和角速度，使其立即停止。
    /// </summary>
    public void ClearMove()
    {
        if (softbody == null || !softbody.isLoaded) return;

        var solver = softbody.solver;
        // 遍历该Actor在Solver中的所有粒子
        for (int i = 0; i < softbody.solverIndices.count; ++i)
        {
            int particleIndex = softbody.solverIndices[i];
            if (particleIndex >= 0 && particleIndex < solver.velocities.count)
            {
                // 清除线速度
                solver.velocities[particleIndex] = Vector3.zero;
                // 清除角速度
                solver.angularVelocities[particleIndex] = Vector3.zero;
            }
        }
    }

    private void LateUpdate()
    {
        if (!actor || !actor.isLoaded || !actorTrans) return;
        
        actor.GetMass(out var com);
        actorTrans.position = actor.solver.transform.TransformPoint(com) + offset;
    }

    private void FixedUpdate()
    {
        if (!playerControl) return;

        if (!referenceFrame) return;

        // 1. 根据保存的输入值和当前摄像机的朝向，计算出目标移动方向
        var relativeMove = (referenceFrame.forward * moveInput.y + referenceFrame.right * moveInput.x);
        relativeMove.y = 0; // 保持移动在水平面上

        var effectiveAcceleration = onGround ? acceleration : acceleration * airControl;
        var moveDirection = relativeMove.normalized * effectiveAcceleration;

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
            if (!(contact.distance > 0.01f)) continue;
            var col = world.colliderHandles[contact.bodyB].owner;
            if (!col) continue;
            onGround = true;
            break;
        }
    }
}