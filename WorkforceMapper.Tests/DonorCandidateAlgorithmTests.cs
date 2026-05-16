using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;
using Xunit;

namespace WorkforceMapper.Tests;

public class DonorCandidateAlgorithmTests
{
    private static WorkforceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<WorkforceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new WorkforceDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Resource MakePeResource(string id, string name) => new()
    {
        ResourceId = id, Name = name, ResourceType = "Employee",
        Company = "Test Co", Seniority = "Analyst", RoleType = "Developer",
        Division = "Platform Engineering", Country = "UK", MonthlyRate = 10_000m,
        Jan=0, Feb=0, Mar=0, Apr=0, May=1, Jun=0, Jul=0, Aug=0, Sep=0, Oct=0, Nov=0, Dec=0
    };

    // Shared scenario: AuthService (app 4, minSupportFte=4.0), May 2026.
    // X1+X2 contribute 2.0 FTE to AuthService Support → gap = 2.0 FTE.
    // Candidates A (80% idle), B (70% idle), C (60% idle) — all PE, tier 1.
    // Ordered by idle FTE desc: A, B, C.
    // Cumulative: A=0.80 (40%), B=1.50 (75%), C=2.10 (105% → stopping at idx 2).
    // Trim-last: A=80, B=70, C=max(1,round((2.0-1.50)*100))=50.
    private static async Task<(WorkforceDbContext ctx, Resource a, Resource b, Resource c)> BuildScenario()
    {
        var ctx = CreateContext();

        var x1 = MakePeResource("X00001", "Support Contributor 1");
        var x2 = MakePeResource("X00002", "Support Contributor 2");
        var a  = MakePeResource("C00001", "Candidate A");
        var b  = MakePeResource("C00002", "Candidate B");
        var c  = MakePeResource("C00003", "Candidate C");

        ctx.Resources.AddRange(x1, x2, a, b, c);
        await ctx.SaveChangesAsync();

        // Activity 5 = AuthService Support (PE); Activity 3 = APIGateway Support (PE, app 2).
        // APIGateway coverage = 0.90 FTE < recommended 2.5 → no surplus, so A/B/C stay tier-1 only.
        ctx.Mappings.AddRange(
            new Mapping { ResourceId = x1.Id, ActivityId = 5, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = x2.Id, ActivityId = 5, Month = "2026-05", Percent = 100 },
            new Mapping { ResourceId = a.Id,  ActivityId = 3, Month = "2026-05", Percent = 20  },
            new Mapping { ResourceId = b.Id,  ActivityId = 3, Month = "2026-05", Percent = 30  },
            new Mapping { ResourceId = c.Id,  ActivityId = 3, Month = "2026-05", Percent = 40  }
        );
        await ctx.SaveChangesAsync();

        return (ctx, a, b, c);
    }

    [Fact]
    public async Task FindAsync_NonStoppingCandidates_RetainFullTierBuilderDonablePercent()
    {
        var (ctx, a, b, _) = await BuildScenario();
        await using (ctx)
        {
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");
            var changes = result!.FewestChangesCombination!.MappingChanges;

            Assert.Contains(changes, m => m.Contains(a.Name) && m.Contains("add 80%"));
            Assert.Contains(changes, m => m.Contains(b.Name) && m.Contains("add 70%"));
        }
    }

    [Fact]
    public async Task FindAsync_StoppingCandidate_IsTrimmedToRemainingGap()
    {
        var (ctx, _, _, c) = await BuildScenario();
        await using (ctx)
        {
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");
            var changes = result!.FewestChangesCombination!.MappingChanges;

            // Remaining gap after A+B: 2.0 - 1.50 = 0.50 FTE → DonablePercent = 50
            Assert.Contains(changes, m => m.Contains(c.Name) && m.Contains("add 50%"));
        }
    }

    [Fact]
    public async Task FindAsync_FewestChangesCombination_HasCorrectRowCount()
    {
        var (ctx, _, _, _) = await BuildScenario();
        await using (ctx)
        {
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");

            Assert.Equal(3, result!.FewestChangesCombination!.RowCount);
        }
    }
}
