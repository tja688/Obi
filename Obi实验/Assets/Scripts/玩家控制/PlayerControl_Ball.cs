using System.Linq;
using Obi;
using UnityEngine;

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
    
    private Vector3 direction;
    private ObiSoftbody softbody;
    private bool onGround = false;

    public ObiSolver PlayerSolver => playerSolver;
    private ObiSolver playerSolver;

    
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
        playerSolver = softbody.solver;
        playerSolver.OnCollision += Solver_OnCollision;
        
        if (PlayerInputManager.instance == null) return;
        PlayerInputManager.instance.OnOnMove += HandleMove;
        PlayerInputManager.instance.OnOnJump += HandleJump;
    }
    

    private void OnDestroy()
    {
        
        if(softbody != null && softbody.solver != null)
        {
            softbody.solver.OnCollision -= Solver_OnCollision;
        }
        
        if (PlayerInputManager.instance == null) return;
        PlayerInputManager.instance.OnOnMove -= HandleMove;
        PlayerInputManager.instance.OnOnJump -= HandleJump;
    }

    private void HandleMove(Vector2 moveInput)
    {
        if (!referenceFrame) return;

        var moveDirection = (referenceFrame.forward * moveInput.y + referenceFrame.right * moveInput.x);
        moveDirection.y = 0;

        var effectiveAcceleration = onGround ? acceleration : acceleration * airControl;

        direction = moveDirection.normalized * effectiveAcceleration;
    }

    private void HandleJump()
    {
        if (!onGround) return;
        onGround = false;
        softbody.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
    }

    private void LateUpdate()
    {
        if (!actor || !actor.isLoaded || !actorTrans) return;
        
        actor.GetMass(out var com);
        
        actorTrans.position = actor.solver.transform.TransformPoint(com) + offset;
    }

    private void FixedUpdate()
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            softbody.AddForce(direction, ForceMode.Acceleration);
        }
    }

    private void Solver_OnCollision(ObiSolver solver, ObiNativeContactList e)
    {
        onGround = false;

        var world = ObiColliderWorld.GetInstance();
        if ((from contact in e where contact.distance > 0.01f select world.colliderHandles[contact.bodyB].owner).Any(col => col))
        {
            onGround = true;
        }
    }
}