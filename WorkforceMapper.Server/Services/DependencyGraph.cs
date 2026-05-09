namespace WorkforceMapper.Server.Services;

public sealed class DependencyGraph
{
    private readonly Dictionary<int, HashSet<int>> _edges = [];
    private readonly Dictionary<int, HashSet<int>> _productApps = [];

    public DependencyGraph(
        IEnumerable<(int appId, int dependsOnAppId)> edges,
        IEnumerable<(int productId, int appId)> productApps)
    {
        foreach (var (appId, dep) in edges)
        {
            if (!_edges.TryGetValue(appId, out var set))
                _edges[appId] = set = [];
            set.Add(dep);
        }

        foreach (var (productId, appId) in productApps)
        {
            if (!_productApps.TryGetValue(productId, out var set))
                _productApps[productId] = set = [];
            set.Add(appId);
        }
    }

    public IReadOnlySet<int> TransitiveDependenciesOf(int appId)
    {
        var visited = new HashSet<int> { appId };
        var queue = new Queue<int>();

        if (_edges.TryGetValue(appId, out var direct))
            foreach (var dep in direct)
                queue.Enqueue(dep);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            if (_edges.TryGetValue(current, out var next))
                foreach (var dep in next)
                    if (!visited.Contains(dep))
                        queue.Enqueue(dep);
        }

        visited.Remove(appId);
        return visited;
    }

    public IReadOnlySet<int> ProductsAffectedByApp(int appId)
    {
        var affected = new HashSet<int>();
        foreach (var (productId, apps) in _productApps)
        {
            var closure = new HashSet<int>(apps);
            foreach (var app in apps)
                foreach (var dep in TransitiveDependenciesOf(app))
                    closure.Add(dep);

            if (closure.Contains(appId))
                affected.Add(productId);
        }
        return affected;
    }
}
