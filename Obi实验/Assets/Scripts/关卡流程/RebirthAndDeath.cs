using System.Collections;
using System.Collections.Generic;
using Obi;
using Obi.Samples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public class RebirthAndDeath : MonoBehaviour
{
    private ObiSolver solver;

    public Camera camera1;
    public ObiSoftbody softbody;
    public ActorCOMTransform softbodyCom;
    public ObiCollider deathPitCollider;
    public ObiCollider finishCollider;
    public Transform spawnPoint;
    public Transform cameraSpawnPoint;

    public UnityEvent onDeath = new UnityEvent();
    public UnityEvent onFinish = new UnityEvent();
    public UnityEvent onRestart = new UnityEvent();

    private bool restart;
    private CameraFollow cameraFollow; // <--- 新增：用于缓存相机组件

    private void Start()
    {
        if (camera1 != null)
        {
            cameraFollow = camera1.GetComponent<CameraFollow>();
            if (cameraFollow == null)
            {
                Debug.LogError("Main Camera does not have an 'ExtrapolationCamera' component attached!", this.gameObject);
            }
        }
        else
        {
            Debug.LogError("Could not find Main Camera in the scene. Make sure your camera has the 'MainCamera' tag.", this.gameObject);
        }
    }

    private void Awake()
    {
        solver = FindFirstObjectByType<ObiSolver>();
        if (solver == null)
            Debug.LogError($"Unable to find ObiSolver");
    }

    private void OnEnable()
    {
        solver.OnCollision += Solver_OnCollision;
        solver.OnSimulationStart += Solver_OnSimulationStart;
        PlayerInputManager.instance.OnOnReStart += Restart;
    }

    private void OnDisable()
    {
        solver.OnCollision -= Solver_OnCollision;
        solver.OnSimulationStart -= Solver_OnSimulationStart;
        PlayerInputManager.instance.OnOnReStart -= Restart;

    }

    private void Restart()
    {
        restart = true;
        
        if (!restart || !spawnPoint) return;
        softbody.Teleport(spawnPoint.position, spawnPoint.rotation);
        
        if (camera1)
            cameraFollow.Teleport(cameraSpawnPoint.position, cameraSpawnPoint.rotation);
        
        restart = false;
    }
    

    private void Solver_OnSimulationStart(ObiSolver s, float timeToSimulate, float substepTime)
    {
        if (!softbody || !softbodyCom) return;

        if (!restart || !spawnPoint) return;
        softbody.Teleport(spawnPoint.position, spawnPoint.rotation);
        
        softbodyCom.Update();

        if (camera1)
            cameraFollow.Teleport(cameraSpawnPoint.position, cameraSpawnPoint.rotation);

        restart = false;
        onRestart.Invoke();
    }

    private void Solver_OnCollision(ObiSolver s, ObiNativeContactList e)
    {
        var world = ObiColliderWorld.GetInstance();
        foreach (var contact in e)
        {
            if (!(contact.distance > 0.01)) continue;
            
            var col = world.colliderHandles[contact.bodyB].owner;
            
            if (col == deathPitCollider)
            {
                onDeath.Invoke();
                Restart();
                return;
            }

            if (col != finishCollider) continue;
            
            onFinish.Invoke();
            return;
        }
    }
}

