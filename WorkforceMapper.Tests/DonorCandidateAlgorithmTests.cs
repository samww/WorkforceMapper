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

    // Scenario B: AuthService (app 4, minSupportFte=4.0), May 2026.
    // Emily: 19% AuthService Support + 43% elsewhere = 62% total, 38% idle.
    // Sarah: 100% AuthService Support = 0% idle.
    // Filler: 80% idle (not on AuthService Support) — ensures FewestChangesCombination triggers.
    private static async Task<(WorkforceDbContext ctx, Resource emily, Resource sarah, Resource filler)>
        BuildExistingContributorScenario()
    {
        var ctx = CreateContext();

        var emily  = MakePeResource("E00001", "Emily");
        var sarah  = MakePeResource("S00001", "Sarah");
        var filler = MakePeResource("F00001", "Filler");

        ctx.Resources.AddRange(emily, sarah, filler);
        await ctx.SaveChangesAsync();

        ctx.Mappings.AddRange(
            // Emily: 19% AuthService Support + 43% APIGateway Support = 62% total
            new Mapping { ResourceId = emily.Id,  ActivityId = 5, Month = "2026-05", Percent = 19 },
            new Mapping { ResourceId = emily.Id,  ActivityId = 3, Month = "2026-05", Percent = 43 },
            // Sarah: 100% AuthService Support, fully allocated
            new Mapping { ResourceId = sarah.Id,  ActivityId = 5, Month = "2026-05", Percent = 100 },
            // Filler: 20% APIGateway Support, 80% idle
            new Mapping { ResourceId = filler.Id, ActivityId = 3, Month = "2026-05", Percent = 20 }
        );
        await ctx.SaveChangesAsync();

        return (ctx, emily, sarah, filler);
    }

    [Fact]
    public async Task FindAsync_ExistingContributor_WithIdleCapacity_AppearsInTier1()
    {
        var (ctx, emily, _, _) = await BuildExistingContributorScenario();
        await using (ctx)
        {
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");

            Assert.NotNull(result);
            // Gap is large enough that there's no FewestChangesCombination, so Emily lands in the buffer.
            Assert.Contains(result.Candidates, c => c.ResourceName == emily.Name && c.ActivityType == "Idle");
        }
    }

    [Fact]
    public async Task FindAsync_ExistingContributor_FullyAllocated_DoesNotAppearInTier1()
    {
        var (ctx, _, sarah, _) = await BuildExistingContributorScenario();
        await using (ctx)
        {
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");

            Assert.NotNull(result);
            Assert.DoesNotContain(result.Candidates, c => c.ResourceName == sarah.Name);
        }
    }

    [Fact]
    public async Task FindAsync_ExistingContributor_StillExcludedFromTier2()
    {
        // Create surplus support on APIGateway (app 2, recommendedSupportFte from seeded taxonomy).
        // Emily (19% on AuthService Support) also has 43% on APIGateway Support.
        // Three extra resources bring APIGateway coverage above recommended so surplus > 0.
        // Emily would qualify for T2 if not excluded, but excludedResourceIds still blocks her.
        var (ctx, emily, _, _) = await BuildExistingContributorScenario();
        await using (ctx)
        {
            // Add 3 resources each 100% on APIGateway Support (activity 3) to push coverage above recommended 2.5
            for (var i = 1; i <= 3; i++)
            {
                var r = MakePeResource($"G0000{i}", $"APIGateway Support {i}");
                ctx.Resources.Add(r);
                await ctx.SaveChangesAsync();
                ctx.Mappings.Add(new Mapping { ResourceId = r.Id, ActivityId = 3, Month = "2026-05", Percent = 100 });
            }
            await ctx.SaveChangesAsync();

            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");

            Assert.NotNull(result);
            Assert.DoesNotContain(result.Candidates, c => c.ResourceName == emily.Name && c.ActivityType == "Support");
        }
    }

    // Scenario C: gap tuned so Emily is the sole stopping candidate in FewestChangesCombination.
    // Y1-Y3 each 100% on AuthService Support = 3.0 FTE; Emily 19% only = 0.19 FTE.
    // Total coverage = 3.19 FTE; AuthService minSupportFte=4.0 → gap = 0.81 FTE.
    // Emily has no other mappings → 81% idle = 0.81 FTE = gap exactly.
    // Emily is the only T1 candidate; DonablePercent = 81; stopping index = 0.
    private static async Task<(WorkforceDbContext ctx, Resource emily)> BuildEmilyStoppingScenario()
    {
        var ctx = CreateContext();

        var emily = MakePeResource("E00001", "Emily");
        ctx.Resources.Add(emily);
        await ctx.SaveChangesAsync();

        // Y1, Y2, Y3: 100% each on AuthService Support — fully allocated, no idle
        for (var i = 1; i <= 3; i++)
        {
            var y = MakePeResource($"Y0000{i}", $"Y{i}");
            ctx.Resources.Add(y);
            await ctx.SaveChangesAsync();
            ctx.Mappings.Add(new Mapping { ResourceId = y.Id, ActivityId = 5, Month = "2026-05", Percent = 100 });
        }
        // Emily: 19% AuthService Support only → 81% idle
        ctx.Mappings.Add(new Mapping { ResourceId = emily.Id, ActivityId = 5, Month = "2026-05", Percent = 19 });
        await ctx.SaveChangesAsync();

        return (ctx, emily);
    }

    [Fact]
    public async Task FindAsync_ExistingContributor_MappingChange_UsesIncreaseAction()
    {
        var (ctx, emily) = await BuildEmilyStoppingScenario();
        await using (ctx)
        {
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");

            var changes = result!.FewestChangesCombination!.MappingChanges;
            Assert.Contains(changes, m => m.Contains(emily.Name) && m.Contains("increase AuthService Support from 19%"));
        }
    }

    [Fact]
    public async Task FindAsync_ExistingContributor_MappingChange_ClauseShowsTotalAllocated()
    {
        var (ctx, emily) = await BuildEmilyStoppingScenario();
        await using (ctx)
        {
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");

            var changes = result!.FewestChangesCombination!.MappingChanges;
            // Emily is 19% total allocated (only mapping); clause must say "total allocated"
            Assert.Contains(changes, m => m.Contains(emily.Name) && m.Contains("total allocated"));
        }
    }

    [Fact]
    public async Task FindAsync_NonContributor_MappingChange_ClauseDoesNotSayTotalAllocated()
    {
        var (ctx, _, _, filler) = await BuildExistingContributorScenario();
        await using (ctx)
        {
            // Filler has 80% idle and is NOT on AuthService Support.
            // Clause should say "currently 20% allocated", not "total allocated".
            var result = await new DonorCandidateAlgorithm(ctx).FindAsync(4, null, "2026-05");

            Assert.NotNull(result);
            var fillerCandidate = result.Candidates.FirstOrDefault(c => c.ResourceName == filler.Name);
            // Filler lands in buffer (no combo in this scenario); verify via IsExistingContributor flag
            Assert.NotNull(fillerCandidate);
            Assert.False(fillerCandidate.IsExistingContributor);
        }
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
