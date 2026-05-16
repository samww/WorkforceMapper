using Microsoft.EntityFrameworkCore;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;

namespace WorkforceMapper.Server.Services;

public class DonorCandidateAlgorithm(WorkforceDbContext context)
{
    private const int BufferRows = 5;

    public async Task<DonorCandidatesResult?> FindAsync(
        int? applicationId, string? applicationName, string month)
    {
        var appQuery = context.Applications.Include(a => a.Activities).AsQueryable();
        var application = applicationId.HasValue
            ? await appQuery.FirstOrDefaultAsync(a => a.Id == applicationId.Value)
            : await appQuery.FirstOrDefaultAsync(a =>
                a.Name.ToLower() == (applicationName ?? "").ToLower());
        if (application is null) return null;

        var division = application.Activities.FirstOrDefault()?.Division ?? "";

        var snapshot = await MonthlyAllocationSnapshot.LoadAsync(context, month);

        var currentCoverage = snapshot.SupportCoverageOf(application.Id);
        var gap = Math.Max(0.0, application.MinSupportFte - currentCoverage);

        var excludedResourceIds = snapshot.ActiveMappings
            .Where(am => am.Activity.ActivityType == "Support" && am.Activity.ApplicationId == application.Id)
            .Select(am => am.Resource.Id)
            .ToHashSet();

        var allApplications = await context.Applications.Include(a => a.Activities).ToListAsync();
        string AppDiv(Application a) => a.Activities.FirstOrDefault()?.Division ?? "";

        var appSurplus  = new Dictionary<int, double>();
        var appCoverage = new Dictionary<int, double>();
        foreach (var app in allApplications)
        {
            var cov = snapshot.SupportCoverageOf(app.Id);
            appCoverage[app.Id] = cov;
            appSurplus[app.Id]  = Math.Max(0, cov - app.RecommendedSupportFte);
        }

        var surplusSupportFteAvailable = allApplications
            .Where(a => AppDiv(a) == division)
            .Sum(a => appSurplus.GetValueOrDefault(a.Id));

        var tier1 = BuildIdleTier(snapshot.ActiveResources, excludedResourceIds, division, gap, crossDiv: false);
        var tier2 = BuildSurplusSupportTier(snapshot.ActiveMappings, allApplications, appSurplus, appCoverage, excludedResourceIds, division, gap, crossDiv: false);
        var tier3 = BuildNonSupportTier(snapshot.ActiveMappings, excludedResourceIds, division, "Minor Changes", gap, crossDiv: false);
        var tier4 = BuildNonSupportTier(snapshot.ActiveMappings, excludedResourceIds, division, "Project",       gap, crossDiv: false);
        var tier5 = BuildIdleTier(snapshot.ActiveResources, excludedResourceIds, division, gap, crossDiv: true);
        var tier6 = BuildSurplusSupportTier(snapshot.ActiveMappings, allApplications, appSurplus, appCoverage, excludedResourceIds, division, gap, crossDiv: true);
        var tier7 = BuildNonSupportTier(snapshot.ActiveMappings, excludedResourceIds, division, "Minor Changes", gap, crossDiv: true);
        var tier8 = BuildNonSupportTier(snapshot.ActiveMappings, excludedResourceIds, division, "Project",       gap, crossDiv: true);

        var sorted = tier1.Concat(tier2).Concat(tier3).Concat(tier4).Concat(tier5).Concat(tier6).Concat(tier7).Concat(tier8).ToList();

        var runningFte = 0.0;
        var withCumulative = sorted.Select(c =>
        {
            runningFte += c.AllocationFte;
            var cumCoverage = gap > 0 ? runningFte / gap * 100.0 : 0.0;
            return c with { CumulativeFte = runningFte, CumulativeGapCoveragePct = cumCoverage };
        }).ToList();

        var stoppingIdx = withCumulative.FindIndex(c => c.CumulativeGapCoveragePct >= 100.0);
        var candidates = withCumulative
            .Select((c, i) => c with { InFewestChangesCombination = stoppingIdx >= 0 && i <= stoppingIdx })
            .ToList();

        if (gap > 0 && stoppingIdx >= 0)
            candidates = ApplyLargestRemainders(candidates, stoppingIdx, gap);

        FewestChangesCombinationSummary? fewestChangesCombo = null;
        if (gap > 0 && stoppingIdx >= 0)
        {
            var comboRows = candidates.Where(c => c.InFewestChangesCombination).ToList();
            var mappingChanges = comboRows
                .Select(r => ComposeMappingChange(r, application.Name, division))
                .ToList();
            fewestChangesCombo = new FewestChangesCombinationSummary(
                comboRows.Count,
                comboRows.Select(c => c.ResourceName).Distinct().Count(),
                comboRows.Sum(c => c.AllocationFte),
                comboRows.Sum(c => c.ImpliedMonthlyCost),
                candidates[stoppingIdx].CumulativeGapCoveragePct,
                mappingChanges,
                ComposeSummaryParagraph(comboRows, application.Name, surplusSupportFteAvailable));
        }

        var totalAvailableCandidatesCount = candidates.Count;

        var trimmed = stoppingIdx >= 0
            ? candidates.Where(c => !c.InFewestChangesCombination).Take(BufferRows).ToList()
            : candidates.Take(BufferRows * 2).ToList();

        return new DonorCandidatesResult(
            application.Id, application.Name, division,
            currentCoverage, application.MinSupportFte, application.RecommendedSupportFte,
            gap,
            trimmed,
            surplusSupportFteAvailable,
            fewestChangesCombo,
            totalAvailableCandidatesCount);
    }

    private static List<DonorCandidateRow> ApplyLargestRemainders(
        List<DonorCandidateRow> candidates, int stoppingIdx, double gap)
    {
        var count   = stoppingIdx + 1;
        var gapCovReached = candidates[stoppingIdx].CumulativeGapCoveragePct;
        var target  = (int)Math.Round(gapCovReached);
        target      = Math.Max(0, Math.Min(target, 100 * count));

        var unrounded = Enumerable.Range(0, count)
            .Select(i => candidates[i].AllocationFte / gap * 100.0)
            .ToArray();

        var caps = Enumerable.Range(0, count)
            .Select(i => (int)Math.Round(candidates[i].AllocationFte * 100.0))
            .ToArray();

        var adj = Enumerable.Range(0, count)
            .Select(i => Math.Min((int)Math.Floor(unrounded[i]), caps[i]))
            .ToArray();

        var deficit = target - adj.Sum();

        var order = Enumerable.Range(0, count)
            .OrderByDescending(i => unrounded[i] - Math.Floor(unrounded[i]))
            .ThenByDescending(i => candidates[i].AllocationFte)
            .ToList();

        var distributed = 0;
        foreach (var idx in order)
        {
            if (distributed >= deficit) break;
            if (adj[idx] < caps[idx])
            {
                adj[idx]++;
                distributed++;
            }
        }

        return candidates
            .Select((c, i) => i < count ? c with { DonablePercent = adj[i] } : c)
            .ToList();
    }

    private static List<DonorCandidateRow> BuildIdleTier(
        IReadOnlyList<ActiveResource> activeResources,
        HashSet<int> excludedResourceIds,
        string targetDivision,
        double gap,
        bool crossDiv)
    {
        return activeResources
            .Where(ar => !excludedResourceIds.Contains(ar.Resource.Id)
                         && (crossDiv ? ar.Resource.Division != targetDivision : ar.Resource.Division == targetDivision)
                         && ar.IdleFte > 0)
            .OrderByDescending(ar => ar.IdleFte)
            .Select(ar =>
            {
                var idlePct = 100 - ar.AllocationPercent;
                var gapCov  = gap > 0 ? ar.IdleFte / gap * 100.0 : 0.0;
                return new DonorCandidateRow(
                    ar.Resource.Name, "Idle capacity", "Idle",
                    ar.AllocationPercent, ar.IdleCost, ar.IdleFte, ar.Resource.MonthlyRate, gapCov,
                    0.0, 0.0, crossDiv, DonablePercent: idlePct,
                    ResourceDivision: ar.Resource.Division);
            })
            .ToList();
    }

    private static List<DonorCandidateRow> BuildSurplusSupportTier(
        IReadOnlyList<ActiveMapping> activeMappings,
        List<Application> allApplications,
        Dictionary<int, double> appSurplus,
        Dictionary<int, double> appCoverage,
        HashSet<int> excludedResourceIds,
        string targetDivision,
        double gap,
        bool crossDiv)
    {
        var rows = new List<DonorCandidateRow>();
        foreach (var app in allApplications)
        {
            var surplus  = appSurplus.GetValueOrDefault(app.Id);
            var coverage = appCoverage.GetValueOrDefault(app.Id);
            if (surplus <= 0 || coverage <= 0) continue;

            var supportMappings = activeMappings
                .Where(am => am.Activity.ActivityType == "Support"
                             && am.Activity.ApplicationId == app.Id
                             && !excludedResourceIds.Contains(am.Resource.Id));

            foreach (var am in supportMappings)
            {
                var isSameDiv = am.Resource.Division == targetDivision;
                if (crossDiv == isSameDiv) continue;

                var donatableFte = (am.Percent / 100.0) * (surplus / coverage);
                var impliedCost  = am.Resource.MonthlyRate * (decimal)donatableFte;
                var gapCoverage  = gap > 0 ? donatableFte / gap * 100.0 : 0.0;
                var donablePct   = (int)Math.Round(donatableFte * 100.0);
                rows.Add(new DonorCandidateRow(
                    am.Resource.Name, am.Activity.Name, am.Activity.ActivityType,
                    am.Percent, impliedCost, donatableFte, am.Resource.MonthlyRate, gapCoverage,
                    0.0, 0.0, crossDiv, DonablePercent: donablePct,
                    ResourceDivision: am.Resource.Division));
            }
        }
        return rows.OrderByDescending(r => r.AllocationFte).ToList();
    }

    private static List<DonorCandidateRow> BuildNonSupportTier(
        IReadOnlyList<ActiveMapping> activeMappings,
        HashSet<int> excludedResourceIds,
        string targetDivision,
        string activityType,
        double gap,
        bool crossDiv)
    {
        return activeMappings
            .Where(am => am.Activity.ActivityType == activityType
                         && !excludedResourceIds.Contains(am.Resource.Id)
                         && (crossDiv ? am.Resource.Division != targetDivision
                                      : am.Resource.Division == targetDivision))
            .Select(am =>
            {
                var fte    = am.Percent / 100.0;
                var gapCov = gap > 0 ? fte / gap * 100.0 : 0.0;
                return new DonorCandidateRow(
                    am.Resource.Name, am.Activity.Name, am.Activity.ActivityType,
                    am.Percent, am.AllocatedCost, fte, am.Resource.MonthlyRate, gapCov,
                    0.0, 0.0, crossDiv, DonablePercent: am.Percent,
                    ResourceDivision: am.Resource.Division);
            })
            .OrderByDescending(r => r.AllocationFte)
            .ToList();
    }

    private static string ComposeSummaryParagraph(
        List<DonorCandidateRow> comboRows,
        string applicationName,
        double surplusSupportFteAvailable)
    {
        static int TierOf(DonorCandidateRow r) => r.ActivityType switch
        {
            "Idle"          => r.RequiresCrossDivisionalAlignment ? 5 : 1,
            "Support"       => r.RequiresCrossDivisionalAlignment ? 6 : 2,
            "Minor Changes" => r.RequiresCrossDivisionalAlignment ? 7 : 3,
            _               => r.RequiresCrossDivisionalAlignment ? 8 : 4
        };

        static string TierLabel(int tier) => tier switch
        {
            1 => "idle capacity in the same division",
            2 => "surplus Support capacity in the same division",
            3 => "Minor Changes capacity in the same division",
            4 => "Project capacity in the same division",
            5 => "idle capacity from other divisions",
            6 => "surplus Support capacity from other divisions",
            7 => "Minor Changes capacity from other divisions",
            _ => "Project capacity from other divisions"
        };

        var rowCount      = comboRows.Count;
        var distinctCount = comboRows.Select(r => r.ResourceName).Distinct().Count();
        var totalFte      = comboRows.Sum(c => c.AllocationFte);
        var totalCost     = comboRows.Sum(c => c.ImpliedMonthlyCost);

        var personWord = distinctCount == 1 ? "person" : "people";
        var changeWord = rowCount      == 1 ? "change" : "changes";

        var tiers     = comboRows.Select(TierOf).Distinct().OrderBy(t => t).ToList();
        var tierScope = tiers.Count == 1
            ? $"(all Tier {tiers[0]}: {TierLabel(tiers[0])})"
            : $"(spanning Tiers {tiers.First()}-{tiers.Last()})";

        var costK  = (int)Math.Round(totalCost / 1000m);
        var totals = $"Total: {totalFte:F1} FTE, ${costK}k/month.";

        var tier2Fte       = comboRows.Where(r => TierOf(r) == 2).Sum(r => r.AllocationFte);
        var exhaustionNote = tier2Fte > 0 && Math.Abs(tier2Fte - surplusSupportFteAvailable) <= 0.01
            ? $" This exhausts the {surplusSupportFteAvailable:F1} FTE of same-division surplus Support available."
            : "";

        var tierExplanation = $"Tier system: tiers 1-4 draw from the same division as {applicationName} " +
            "(Tier 1 idle capacity, Tier 2 surplus Support, Tier 3 Minor Changes, Tier 4 Project); " +
            "tiers 5-8 are the same source types from other divisions and require cross-divisional alignment.";

        return $"{rowCount} {changeWord} across {distinctCount} {personWord} {tierScope}. {totals}{exhaustionNote} {tierExplanation}";
    }

    private static string ComposeMappingChange(DonorCandidateRow row, string targetAppName, string targetDivision)
    {
        string clause = row.ActivityType == "Idle"
            ? $"(use idle capacity - currently {row.Percent}% allocated)"
            : row.ActivityType == "Support"
                ? $"(reduce {row.ActivityName} from {row.Percent}% to {row.Percent - row.DonablePercent}%)"
                : $"(remove {row.ActivityName} mapping, currently {row.Percent}%)";

        var sentence = $"{row.ResourceName}: add {row.DonablePercent}% to {targetAppName} Support {clause}";

        if (row.RequiresCrossDivisionalAlignment)
            sentence += $" - requires cross-divisional sign-off ({row.ResourceDivision} -> {targetDivision})";

        return sentence;
    }
}
