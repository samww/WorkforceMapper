using WorkforceMapper.Server.Services;

namespace WorkforceMapper.Tests;

public class DependencyGraphTests
{
    // --- Cycle 1: empty graph ---

    [Fact]
    public void TransitiveDependenciesOf_ReturnsEmpty_WhenNoEdges()
    {
        var graph = new DependencyGraph([], []);

        var result = graph.TransitiveDependenciesOf(1);

        Assert.Empty(result);
    }

    // --- Cycle 2: BFS traversal ---

    [Fact]
    public void TransitiveDependenciesOf_ReturnsDirectAndIndirect()
    {
        // A(1) -> B(2) -> C(3)
        var graph = new DependencyGraph([(1, 2), (2, 3)], []);

        var result = graph.TransitiveDependenciesOf(1);

        Assert.Equal([2, 3], result.Order().ToArray());
    }

    // --- Cycle 3: diamond ---

    [Fact]
    public void TransitiveDependenciesOf_HandlesDiamond()
    {
        // A(1)->B(2), A(1)->C(3), B(2)->D(4), C(3)->D(4)
        var graph = new DependencyGraph([(1, 2), (1, 3), (2, 4), (3, 4)], []);

        var result = graph.TransitiveDependenciesOf(1);

        Assert.Equal([2, 3, 4], result.Order().ToArray());
    }

    // --- Cycle 4: cycle termination ---

    [Fact]
    public void TransitiveDependenciesOf_TerminatesOnCycle()
    {
        // A(1)->B(2)->A(1)
        var graph = new DependencyGraph([(1, 2), (2, 1)], []);

        var result = graph.TransitiveDependenciesOf(1);

        Assert.Equal([2], result.Order().ToArray());
    }

    // --- Cycle 4b: depth-1 vs depth-N traversal sanity ---

    [Fact]
    public void TransitiveDependenciesOf_ReturnsAllDepths_NotJustDirectNeighbours()
    {
        // Chain A(1)->B(2)->C(3)->D(4). A naive depth-1-only BFS would return {2}; correct BFS returns {2,3,4}.
        // Also verifies depth-1 seed B(2) returns only {3,4}, not A(1).
        var graph = new DependencyGraph([(1, 2), (2, 3), (3, 4)], []);

        var fromA = graph.TransitiveDependenciesOf(1);
        var fromB = graph.TransitiveDependenciesOf(2);

        Assert.Equal([2, 3, 4], fromA.Order().ToArray());
        Assert.Equal([3, 4], fromB.Order().ToArray());
    }

    // --- Cycle 5: ProductsAffectedByApp ---

    [Fact]
    public void ProductsAffectedByApp_DirectAndTransitive()
    {
        // Product P(10) uses App A(1). A(1) depends on B(2). B(2) depends on C(3).
        // Querying C(3) should find P(10) via transitive path.
        var graph = new DependencyGraph([(1, 2), (2, 3)], [(10, 1)]);

        Assert.Contains(10, graph.ProductsAffectedByApp(3));
    }

    // --- TransitiveDependentsOf ---

    [Fact]
    public void TransitiveDependentsOf_ReturnsEmpty_WhenNothingDependsOnApp()
    {
        // A(1)->B(2)->C(3): nothing depends on A(1), so TransitiveDependentsOf(1) is empty
        var graph = new DependencyGraph([(1, 2), (2, 3)], []);

        var result = graph.TransitiveDependentsOf(1);

        Assert.Empty(result);
    }

    [Fact]
    public void TransitiveDependentsOf_ReturnsDirectDependent()
    {
        // A(1)->B(2): B is depended on by A directly
        var graph = new DependencyGraph([(1, 2)], []);

        var result = graph.TransitiveDependentsOf(2);

        Assert.Equal([1], result.Order().ToArray());
    }

    [Fact]
    public void TransitiveDependentsOf_ReturnsTransitiveChain()
    {
        // A(1)->B(2)->C(3): querying C returns both B and A
        var graph = new DependencyGraph([(1, 2), (2, 3)], []);

        var result = graph.TransitiveDependentsOf(3);

        Assert.Equal([1, 2], result.Order().ToArray());
    }

    // --- Cycle 6: multiple paths ---

    [Fact]
    public void ProductsAffectedByApp_ReachableThroughMultiplePaths()
    {
        // P(10) uses A(1) and A(2). Both depend on C(3). C(3) should affect P(10) only once.
        var graph = new DependencyGraph([(1, 3), (2, 3)], [(10, 1), (10, 2)]);

        var result = graph.ProductsAffectedByApp(3);

        Assert.Equal([10], result.Order().ToArray());
    }
}
