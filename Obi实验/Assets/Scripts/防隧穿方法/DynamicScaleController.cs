using UnityEngine;
using Obi;
using System.Collections.Generic;

// V1.4 - 最终修正版。修正了所有关于 Handle 的大小写错误和 DeactivateScale 中的拼写错误。
[RequireComponent(typeof(ObiSoftbody))]
public class DynamicScaleController : MonoBehaviour
{
    [Header("核心配置")]
    [Tooltip("鳞片代理的预制体 (必须包含 Rigidbody, Collider, Obi Collider 和 ScaleProxy 脚本)")]
    public GameObject scalePrefab;
    [Tooltip("只对拥有此Tag的物体生成鳞片。留空则对所有碰撞生效。")]
    public string colliderTag;
    [Tooltip("预分配的鳞片对象池大小。")]
    public int scalePoolSize = 128;

    [Header("鳞片行为")]
    [Tooltip("Pin约束的硬度 (0-1)。")]
    [Range(0f, 1f)]
    public float pinStiffness = 1f;
    [Tooltip("鳞片在未被接触多少秒后自动消失。")]
    public float disengagementTime = 0.5f;

    // --- 私有成员 ---
    private ObiSoftbody softbody;
    private ObiSolver solver;
    
    private ObiPinConstraintsData pinConstraintsData;
    private ObiPinConstraintsBatch dynamicPinBatch;

    private List<ScaleProxy> scalePool;
    private Dictionary<ObiColliderBase, ScaleGroup> activeGroups = new Dictionary<ObiColliderBase, ScaleGroup>();
    private Dictionary<int, ScaleProxy> particleToScaleMap = new Dictionary<int, ScaleProxy>(); 

    private struct PinCreationRequest
    {
        public int ParticleSolverIndex;
        public ScaleProxy Scale;
        public ObiColliderBase TargetCollider;
    }
    private List<PinCreationRequest> pendingPinRequests = new List<PinCreationRequest>();

    #region Unity生命周期与Obi事件
    void OnEnable()
    {
        softbody = GetComponent<ObiSoftbody>();
        if (softbody.isLoaded) OnBlueprintLoaded(softbody, softbody.sourceBlueprint);
        softbody.OnBlueprintLoaded += OnBlueprintLoaded;
    }

    void OnDisable()
    {
        softbody.OnBlueprintLoaded -= OnBlueprintLoaded;
        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        
        RemoveDynamicBatch();
        DestroyScalePool();
    }

    private void OnBlueprintLoaded(ObiActor actor, ObiActorBlueprint blueprint)
    {
        InitializeSystem();
    }
    
    void LateUpdate()
    {
        if (solver == null || !solver.isActiveAndEnabled) return;

        ProcessPendingPinRequests();
        
        foreach (var group in activeGroups.Values)
        {
            group.UpdateGroupTransforms();
        }

        List<ScaleProxy> scalesToDeactivate = new List<ScaleProxy>();
        foreach (var pair in particleToScaleMap)
        {
            var scale = pair.Value;
            scale.timeSinceLastContact += Time.deltaTime;
            if (scale.timeSinceLastContact > disengagementTime)
            {
                scalesToDeactivate.Add(scale);
            }
        }
        
        if (scalesToDeactivate.Count > 0)
        {
            foreach (var scale in scalesToDeactivate)
            {
                DeactivateScale(scale);
            }
            softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
        }
        
        List<ObiColliderBase> emptyGroups = new List<ObiColliderBase>();
        foreach(var pair in activeGroups)
        {
            if (pair.Value.IsEmpty())
            {
                emptyGroups.Add(pair.Key);
            }
        }
        foreach(var key in emptyGroups)
        {
            activeGroups.Remove(key);
        }
    }
    #endregion

    #region 系统初始化与销毁
    private void InitializeSystem()
    {
        if (solver != null) solver.OnCollision -= Solver_OnCollision;
        solver = softbody.solver;
        if (solver != null) solver.OnCollision += Solver_OnCollision;

        SetupPinConstraints();
        CreateScalePool();
    }

    private void CreateScalePool()
    {
        DestroyScalePool();
        scalePool = new List<ScaleProxy>(scalePoolSize);
        for (int i = 0; i < scalePoolSize; ++i)
        {
            var go = Instantiate(scalePrefab, Vector3.zero, Quaternion.identity, this.transform);
            var proxy = go.GetComponent<ScaleProxy>();
            if (proxy == null || go.GetComponent<ObiCollider>() == null)
            {
                Debug.LogError("鳞片预制体不完整！请确保它包含 ScaleProxy 和 ObiCollider 组件。", this);
                enabled = false;
                return;
            }
            go.SetActive(false);
            scalePool.Add(proxy);
        }
    }

    private void DestroyScalePool()
    {
        if (scalePool != null)
        {
            foreach (var scale in scalePool)
            {
                if (scale != null) Destroy(scale.gameObject);
            }
            scalePool.Clear();
        }
    }

    private void SetupPinConstraints()
    {
        RemoveDynamicBatch();
        pinConstraintsData = softbody.GetConstraintsByType(Oni.ConstraintType.Pin) as ObiPinConstraintsData;
        if (pinConstraintsData == null) { Debug.LogError($"[{this.name}] 缺少 ObiPinConstraints 组件。请添加一个。", this); enabled = false; return; }
        
        dynamicPinBatch = new ObiPinConstraintsBatch();
        for (int i = 0; i < scalePoolSize; ++i)
        {
            dynamicPinBatch.AddConstraint(-1, null, Vector3.zero, Quaternion.identity, 0, 0);
        }
        dynamicPinBatch.activeConstraintCount = 0;
        pinConstraintsData.AddBatch(dynamicPinBatch);
    }
    
    private void RemoveDynamicBatch()
    {
        if (pinConstraintsData != null && dynamicPinBatch != null)
        {
            pinConstraintsData.RemoveBatch(dynamicPinBatch);
            if (softbody.isLoaded) softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
        }
        dynamicPinBatch = null;
    }
    #endregion
    
    #region 核心逻辑
    private void Solver_OnCollision(ObiSolver s, ObiNativeContactList contacts)
    {
        if (contacts.count == 0) return;

        for (int i = 0; i < contacts.count; ++i)
        {
            if (dynamicPinBatch.activeConstraintCount + pendingPinRequests.Count >= scalePoolSize) break;
            
            Oni.Contact contact = contacts[i];
            
            if (ObiCollisionUtils.TryParseActorColliderPair(contact, s, out ObiActor foundActor, out ObiColliderBase foundCollider))
            {
                if (foundActor != this.softbody) continue;
                
                // [V1.4 修正] 严格按照您的脚本模式，从已知的 foundCollider 的 Handle.index 推断粒子索引
                int particleSolverIndex = (foundCollider.Handle.index == contact.bodyA) ? contact.bodyB : contact.bodyA;

                if (particleToScaleMap.ContainsKey(particleSolverIndex)) 
                {
                    particleToScaleMap[particleSolverIndex].NotifyContact();
                    continue;
                }
                
                if (!string.IsNullOrEmpty(colliderTag) && !foundCollider.CompareTag(colliderTag)) continue;
                
                RequestScaleActivation(particleSolverIndex, foundCollider, contact);
            }
        }
    }

    private void RequestScaleActivation(int particleSolverIndex, ObiColliderBase targetCollider, Oni.Contact contact)
    {
        ScaleProxy scale = GetScaleFromPool();
        if (scale == null) return; 

        Vector3 contactPoint = (contact.pointA + contact.pointB) * 0.5f;
        Vector3 particlePos = solver.positions[particleSolverIndex];
        Quaternion rotation = (particlePos == contactPoint) ? Quaternion.identity : Quaternion.LookRotation(contactPoint - particlePos);

        scale.Initialize(this, null, particleSolverIndex, contactPoint, rotation);
        
        pendingPinRequests.Add(new PinCreationRequest
        {
            ParticleSolverIndex = particleSolverIndex,
            Scale = scale,
            TargetCollider = targetCollider
        });
    }

    private void ProcessPendingPinRequests()
    {
        if (pendingPinRequests.Count == 0) return;

        foreach (var request in pendingPinRequests)
        {
            var scaleObiCollider = request.Scale.GetComponent<ObiColliderBase>();
            
            // [V1.4 修正] 使用大写 Handle
            if (scaleObiCollider == null || scaleObiCollider.Handle == null)
            {
                Debug.LogWarning($"无法为粒子 {request.ParticleSolverIndex} 创建Pin约束，因为鳞片上的ObiCollider句柄无效。");
                request.Scale.Deactivate();
                continue;
            }

            if (!activeGroups.TryGetValue(request.TargetCollider, out ScaleGroup group))
            {
                group = new ScaleGroup(request.TargetCollider);
                activeGroups[request.TargetCollider] = group;
            }
            request.Scale.SetGroup(group); 
            group.AddScale(request.Scale);

            ActivatePin(request.ParticleSolverIndex, scaleObiCollider);
            
            particleToScaleMap[request.ParticleSolverIndex] = request.Scale;
        }
        
        pendingPinRequests.Clear();
        softbody.SetConstraintsDirty(Oni.ConstraintType.Pin);
    }
    
    private void ActivatePin(int particleSolverIndex, ObiColliderBase scaleCollider)
    {
        int slotIndex = dynamicPinBatch.activeConstraintCount;
        
        Vector3 offsetInScaleSpace = scaleCollider.transform.InverseTransformPoint(solver.positions[particleSolverIndex]);

        // [V1.4 修正] 使用大写 Handle
        dynamicPinBatch.particleIndices[slotIndex] = particleSolverIndex;
        dynamicPinBatch.pinBodies[slotIndex] = scaleCollider.Handle;
        dynamicPinBatch.colliderIndices[slotIndex] = scaleCollider.Handle.index;
        dynamicPinBatch.offsets[slotIndex] = offsetInScaleSpace;
        dynamicPinBatch.stiffnesses[slotIndex * 2] = 1f - pinStiffness;
        dynamicPinBatch.stiffnesses[slotIndex * 2 + 1] = 1f;

        dynamicPinBatch.activeConstraintCount++;
    }

    private void DeactivateScale(ScaleProxy scale)
    {
        if (scale == null) return;
        
        int particleIndex = scale.AttachedParticleSolverIndex;

        for (int i = 0; i < dynamicPinBatch.activeConstraintCount; ++i)
        {
            if (dynamicPinBatch.particleIndices[i] == particleIndex)
            {
                dynamicPinBatch.activeConstraintCount--;
                int lastActiveIndex = dynamicPinBatch.activeConstraintCount;

                if (i < lastActiveIndex)
                {
                    // [V1.4 修正] 修复了所有拼写错误
                    dynamicPinBatch.particleIndices[i] = dynamicPinBatch.particleIndices[lastActiveIndex];
                    dynamicPinBatch.pinBodies[i] = dynamicPinBatch.pinBodies[lastActiveIndex];
                    dynamicPinBatch.colliderIndices[i] = dynamicPinBatch.colliderIndices[lastActiveIndex];
                    dynamicPinBatch.offsets[i] = dynamicPinBatch.offsets[lastActiveIndex];
                    dynamicPinBatch.stiffnesses[i*2] = dynamicPinBatch.stiffnesses[lastActiveIndex*2];
                    dynamicPinBatch.stiffnesses[i*2+1] = dynamicPinBatch.stiffnesses[lastActiveIndex*2+1];
                }
                break;
            }
        }
        
        if (scale.ParentGroup != null)
        {
            scale.ParentGroup.RemoveScale(scale);
        }
        particleToScaleMap.Remove(particleIndex);
        
        scale.Deactivate();
    }
    
    private ScaleProxy GetScaleFromPool()
    {
        foreach (var scale in scalePool)
        {
            if (!scale.gameObject.activeInHierarchy) return scale;
        }
        return null;
    }
    #endregion
}