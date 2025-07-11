using Obi;

/// <summary>
/// Obi 碰撞解析静态工具类
/// 提供通用的方法来解码Oni.Contact，从中安全地提取Actor和Collider。
/// </summary>
public static class ObiCollisionUtils
{
    /// <summary>
    /// 尝试从一个Oni.Contact中解析出ObiActor和ObiColliderBase。
    /// 这个方法会自动处理两种可能的组合（bodyA是粒子或bodyB是粒子）。
    /// </summary>
    /// <param name="contact">要解析的碰撞接触点。</param>
    /// <param name="solver">触发该接触点的求解器。</param>
    /// <param name="actor">如果解析成功，将返回碰撞中的ObiActor。</param>
    /// <param name="collider">如果解析成功，将返回碰撞中的ObiColliderBase。</param>
    /// <returns>如果成功解析出一个有效的Actor-Collider对，则返回true。</returns>
    public static bool TryParseActorColliderPair(Oni.Contact contact, ObiSolver solver, out ObiActor actor, out ObiColliderBase collider)
    {
        actor = null;
        collider = null;

        // 获取全局的碰撞体世界实例
        var world = ObiColliderWorld.GetInstance();
        if (world == null) return false;

        // 组合 1: 假设 bodyA 是粒子, bodyB 是碰撞体
        var potentialActor1 = GetActorFromSolver(solver, contact.bodyA);
        var potentialCollider1 = GetColliderFromWorld(world, contact.bodyB);

        if (potentialActor1 != null && potentialCollider1 != null)
        {
            actor = potentialActor1;
            collider = potentialCollider1;
            return true;
        }

        // 组合 2: 假设 bodyB 是粒子, bodyA 是碰撞体
        var potentialActor2 = GetActorFromSolver(solver, contact.bodyB);
        var potentialCollider2 = GetColliderFromWorld(world, contact.bodyA);

        if (potentialActor2 == null || potentialCollider2 == null) return false;
        actor = potentialActor2;
        collider = potentialCollider2;
        return true;

    }

    /// <summary>
    /// [辅助方法] 从给定的Solver中安全地根据粒子索引获取Actor。
    /// </summary>
    private static ObiActor GetActorFromSolver(ObiSolver solver, int particleIndex)
    {
        if (solver == null || !solver.gameObject.activeInHierarchy || particleIndex < 0 || particleIndex >= solver.particleToActor.Length)
            return null;
        
        // ?.actor 会安全地处理 handle 为 null 的情况
        return solver.particleToActor[particleIndex]?.actor;
    }

    /// <summary>
    /// [辅助方法] 从全局碰撞体世界中安全地根据索引获取Collider。
    /// </summary>
    private static ObiColliderBase GetColliderFromWorld(ObiColliderWorld world, int colliderIndex)
    {
        if (colliderIndex < 0 || colliderIndex >= world.colliderHandles.Count)
            return null;
        
        // ?.owner 会安全地处理 handle 为 null 的情况
        return world.colliderHandles[colliderIndex]?.owner;
    }
}