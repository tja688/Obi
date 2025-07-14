// PlayerControl_Ball.cs
using UnityEngine;
using Obi;

public class PlayerControl_Ball : MonoBehaviour, IControllable
{
    // ---- IControllable 接口实现 ----
    
    // 【已修正】实现接口中的新属性名，并正确返回 MonoBehaviour 的原生 gameObject
    public GameObject controlledGameObject => this.gameObject;

    public void Move(Vector2 moveVector)
    {
        this.moveInput = moveVector;
    }

    public void Jump()
    {
        if (!onGround) return;
        onGround = false;
        softbody.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
    }

    public void Interact()
    {
        // Debug.Log($"{this.name} 触发了交互！");
    }
    // ---- 接口实现结束 ----

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

    private Vector2 moveInput;
    private ObiSoftbody softbody;
    private bool onGround = false;
    public ObiSolver playerSolver { get; private set; }
    
    private void Start()
    {
        softbody = GetComponent<ObiSoftbody>();
        if (softbody == null || softbody.solver == null) return;
        playerSolver = softbody.solver;
        playerSolver.OnCollision += Solver_OnCollision;
        
        if (referenceFrame == null)
            referenceFrame = CameraManager.instance.gameObject.transform;
    }

    private void OnDestroy()
    {
        if (softbody != null && softbody.solver != null)
        {
            softbody.solver.OnCollision -= Solver_OnCollision;
        }
    }
    
    public void ClearMove()
    {
        if (!softbody || !softbody.isLoaded) return;

        for (var i = 0; i < softbody.solverIndices.count; ++i)
        {
            var particleIndex = softbody.solverIndices[i];
            if (particleIndex < 0 || particleIndex >= playerSolver.velocities.count) continue;
            
            playerSolver.velocities[particleIndex] = Vector3.zero;
            playerSolver.angularVelocities[particleIndex] = Vector3.zero;
        }
        
        if (playerSolver.velocities.count <= 0) return;
        playerSolver.velocities.Upload();
        playerSolver.angularVelocities.Upload();
    }

    private void LateUpdate()
    {
        if (!actor || !actor.isLoaded || !actorTrans) return;

        actor.GetMass(out var com);
        actorTrans.position = actor.solver.transform.TransformPoint(com) + offset;
    }

    private void FixedUpdate()
    {
        if (!referenceFrame) return;

        var relativeMove = (referenceFrame.forward * moveInput.y + referenceFrame.right * moveInput.x);
        relativeMove.y = 0;

        var effectiveAcceleration = onGround ? acceleration : acceleration * airControl;
        var moveDirection = relativeMove.normalized * effectiveAcceleration;

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