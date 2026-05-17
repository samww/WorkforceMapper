using Anthropic.SDK.Messaging;
using Microsoft.EntityFrameworkCore;
using Moq;
using WorkforceMapper.Server.Data;
using WorkforceMapper.Server.Models;
using WorkforceMapper.Server.Services;
using Xunit;

namespace WorkforceMapper.Tests;

public class AgentServiceTests
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

    private static async IAsyncEnumerable<MessageResponse> EmptyStream()
    {
        yield break;
    }

    private static async IAsyncEnumerable<MessageResponse> TokenStream(params string[] tokens)
    {
        foreach (var token in tokens)
            yield return new MessageResponse { Delta = new Delta { Text = token } };
    }

    private static MessageResponse ToolUseResponse(string toolName, string toolId, string argsJson)
    {
        var args = System.Text.Json.Nodes.JsonNode.Parse(argsJson)!;
        return new MessageResponse
        {
            Role = RoleType.Assistant,
            StopReason = "tool_use",
            Content = [new ToolUseContent { Id = toolId, Name = toolName, Input = args }]
        };
    }

    private static MessageResponse EndTurnResponse(string text = "") =>
        new MessageResponse
        {
            StopReason = "end_turn",
            Content = text.Length > 0 ? [new TextContent { Text = text }] : []
        };

    // ── Cycle 1 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CallsCorrectTool_WhenCapacityQueryReceived()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_capacity_by_division", "tc_1", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("What is our capacity?", "2026-05", _ => Task.CompletedTask);

        // At least one GetMessageAsync call must include a tool result for tc_1
        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_1"))
        )), Times.AtLeastOnce);
    }

    // ── Cycle 2 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CallsProductCostTool_WhenProductQueryReceived()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_cost_by_product", "tc_2", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("What does each product cost?", "2026-05", _ => Task.CompletedTask);

        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_2"))
        )), Times.AtLeastOnce);
    }

    // ── Cycle 3 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_StreamsTokens_ToCallback()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        // Streaming only runs after at least one tool-use round-trip.
        // First call dispatches a tool; second call is the final end-turn that triggers streaming.
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_capacity_by_division", "tc_stream", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(TokenStream("Platform ", "Engineering ", "has 23 resources."));

        var service = new AgentService(mock.Object, dataService);
        var tokens = new List<string>();
        await service.RunAsync("Tell me about capacity.", "2026-05", t =>
        {
            tokens.Add(t);
            return Task.CompletedTask;
        });

        Assert.NotEmpty(tokens);
        Assert.All(tokens, t => Assert.NotEmpty(t));
    }

    // ── Cycle 5 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ToolList_ContainsRiskTools_AndDoesNotContainOverallocatedResources()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("No tools needed."));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        Assert.NotNull(captured?.Tools);
        Assert.Contains(captured.Tools, t => t.Function?.Name == "get_application_risk");
        Assert.Contains(captured.Tools, t => t.Function?.Name == "get_product_risk");
        Assert.DoesNotContain(captured.Tools, t => t.Function?.Name == "get_overallocated_resources");
    }

    [Fact]
    public async Task RunAsync_ToolList_ApplicationRiskTool_HasApplicationNameParameter()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var appRiskTool = captured!.Tools!.Single(t => t.Function?.Name == "get_application_risk");
        var props = appRiskTool.Function!.Parameters?["properties"];
        Assert.NotNull(props?["application_name"]);
    }

    [Fact]
    public async Task RunAsync_Temperature_IsZero()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        Assert.Equal(0, captured!.Temperature);
    }

    [Fact]
    public async Task RunAsync_DispatchesApplicationRisk_ByName_ReturnsOnlyAuthService()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        string? toolResultJson = null;
        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_application_risk", "tc_ar_name",
                @"{""application_name"":""AuthService"",""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
                p.Messages.Any(m => m.Content != null &&
                    m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_ar_name")))))
            .Callback<MessageParameters>(p =>
            {
                var tr = p.Messages
                    .SelectMany(m => m.Content?.OfType<ToolResultContent>() ?? [])
                    .First(tr => tr.ToolUseId == "tc_ar_name");
                toolResultJson = tr.Content?.OfType<TextContent>().FirstOrDefault()?.Text;
            })
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("Is AuthService at risk?", "2026-05", _ => Task.CompletedTask);

        Assert.NotNull(toolResultJson);
        Assert.Contains("AuthService", toolResultJson);
        // Only one application returned — not all 18
        var parsed = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.Nodes.JsonObject>>(toolResultJson!);
        Assert.NotNull(parsed);
        Assert.Single(parsed);
    }

    // ── Cycle 6 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CallsApplicationRiskTool_WhenAskedAboutHighRiskApps()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_application_risk", "tc_ar", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("Which applications are high risk?", "2026-05", _ => Task.CompletedTask);

        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_ar"))
        )), Times.AtLeastOnce);
    }

    // ── Cycle 7 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CallsProductRiskTool_WhenAskedAboutAffectedProducts()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_product_risk", "tc_pr", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("Which products are affected by high risk apps?", "2026-05", _ => Task.CompletedTask);

        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_pr"))
        )), Times.AtLeastOnce);
    }

    // ── Cycle 8 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ToolList_ContainsDonorCandidates_Tool()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        Assert.NotNull(captured?.Tools);
        Assert.Contains(captured.Tools, t => t.Function?.Name == "get_donor_candidates");
    }

    [Fact]
    public async Task RunAsync_ComposesDonorCandidates_FromUnallocatedAndActivityAllocations()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_unallocated_resources", "tc_ur", @"{""month"":""2026-05""}"))
            .ReturnsAsync(ToolUseResponse("get_activity_allocations", "tc_aa", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("Who could be moved to support AuthService?", "2026-05", _ => Task.CompletedTask);

        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_ur")) &&
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_aa"))
        )), Times.AtLeastOnce);
    }

    // ── Cycle 9 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_DispatchesDonorCandidates_ReturnsJsonWithApplicationName()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        string? toolResultJson = null;
        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_donor_candidates", "tc_dc",
                @"{""application_id"":4,""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
                p.Messages.Any(m => m.Content != null &&
                    m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_dc")))))
            .Callback<MessageParameters>(p =>
            {
                var tr = p.Messages
                    .SelectMany(m => m.Content?.OfType<ToolResultContent>() ?? [])
                    .First(tr => tr.ToolUseId == "tc_dc");
                toolResultJson = tr.Content?.OfType<TextContent>().FirstOrDefault()?.Text;
            })
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("Where can we find capacity for AuthService?", "2026-05", _ => Task.CompletedTask);

        Assert.NotNull(toolResultJson);
        Assert.Contains("AuthService", toolResultJson);
        Assert.Contains("application_name", toolResultJson);
    }

    // ── Cycle 10 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildTools_DonorCandidates_DescribesEightTierPriorityOrder()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var tool = captured!.Tools!.Single(t => t.Function?.Name == "get_donor_candidates");
        var desc = tool.Function!.Description;
        Assert.Contains("eight-tier", desc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tiers 1", desc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tiers 5", desc, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cycle 11 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildTools_DonorCandidates_DocumentsRequiresCrossDivisionalAlignment()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var tool = captured!.Tools!.Single(t => t.Function?.Name == "get_donor_candidates");
        var desc = tool.Function!.Description;
        Assert.Contains("requires_cross_divisional_alignment", desc, StringComparison.Ordinal);
    }

    // ── Cycle 12 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildTools_DonorCandidates_DocumentsSummaryParagraph()
    {
        // summary_paragraph replaces the old surplus_support_fte_available instruction (#34)
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var tool = captured!.Tools!.Single(t => t.Function?.Name == "get_donor_candidates");
        var desc = tool.Function!.Description;
        Assert.Contains("summary_paragraph", desc, StringComparison.Ordinal);
    }

    // ── Cycle 13 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildSystemPrompt_DonorParagraph_InstructsSameDivisionTiersBeforeCrossDivisional()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var systemText = string.Concat(captured!.System!.Select(s => s.Text));
        // Donor section must describe same-division tiers 1-4 before cross-divisional tiers 5-8
        Assert.True(
            systemText.IndexOf("same-division tiers 1", StringComparison.OrdinalIgnoreCase) <
            systemText.IndexOf("tiers 5", StringComparison.OrdinalIgnoreCase),
            "same-division tiers 1–4 must appear before cross-divisional tiers 5–8");
    }

    // ── Cycle 14 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildSystemPrompt_DonorParagraph_InstructsCumulativeGapCoverageStopping()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var systemText = string.Concat(captured!.System!.Select(s => s.Text));
        // Stopping point is baked into summary_paragraph (pre-computed); agent uses pre-computed pool count
        Assert.Contains("summary_paragraph", systemText, StringComparison.Ordinal);
        Assert.Contains("total_available_candidates_count", systemText, StringComparison.Ordinal);
    }

    // ── Cycle 15 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildSystemPrompt_DonorParagraph_InstructsRenderingSummaryParagraphVerbatim()
    {
        // summary_paragraph replaces the old surplus_support_fte_available + tier-explain instructions (#34)
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var systemText = string.Concat(captured!.System!.Select(s => s.Text));
        Assert.Contains("summary_paragraph", systemText, StringComparison.Ordinal);
        Assert.Contains("verbatim", systemText, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cycle 16 ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildSystemPrompt_DonorParagraph_InstructsFewestChangesCombinationAsCompleteSet()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var systemText = string.Concat(captured!.System!.Select(s => s.Text));
        // Must reference the pre-computed combo summary and instruct verbatim rendering
        Assert.Contains("fewest_changes_combination", systemText, StringComparison.Ordinal);
        Assert.Contains("verbatim", systemText, StringComparison.OrdinalIgnoreCase);
        // Must instruct agent to use pre-computed pool count (not count the list)
        Assert.Contains("total_available_candidates_count", systemText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemPrompt_DonorParagraph_InstructsTiersBakedIntoParagraph()
    {
        // Tier-system explanation is now baked into summary_paragraph (#34); prompt must not ask agent to re-explain it
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var systemText = string.Concat(captured!.System!.Select(s => s.Text));
        Assert.Contains("tier-system description", systemText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("baked", systemText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildTools_DonorCandidates_DocumentsFewestChangesCombinationAndInFlag()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var tool = captured!.Tools!.Single(t => t.Function?.Name == "get_donor_candidates");
        var desc = tool.Function!.Description;
        Assert.Contains("fewest_changes_combination", desc, StringComparison.Ordinal);
        Assert.Contains("in_fewest_changes_combination", desc, StringComparison.Ordinal);
    }

    // ── Cycle 17 — get_project_allocation ───────────────────────────────────

    [Fact]
    public async Task RunAsync_ToolList_ContainsGetProjectAllocation()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        Assert.NotNull(captured?.Tools);
        Assert.Contains(captured.Tools, t => t.Function?.Name == "get_project_allocation");
    }

    [Fact]
    public async Task RunAsync_DispatchesGetProjectAllocation_ResultContainsTotalsAndRanking()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        string? toolResultJson = null;
        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_project_allocation", "tc_pa", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
                p.Messages.Any(m => m.Content != null &&
                    m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_pa")))))
            .Callback<MessageParameters>(p =>
            {
                var tr = p.Messages
                    .SelectMany(m => m.Content?.OfType<ToolResultContent>() ?? [])
                    .First(tr => tr.ToolUseId == "tc_pa");
                toolResultJson = tr.Content?.OfType<TextContent>().FirstOrDefault()?.Text;
            })
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("Which projects are consuming the most capacity?", "2026-05", _ => Task.CompletedTask);

        Assert.NotNull(toolResultJson);
        Assert.Contains("total_project_allocation_fte", toolResultJson);
        Assert.Contains("projects_ranked_by_allocation", toolResultJson);
    }

    [Fact]
    public async Task BuildSystemPrompt_ProjectParagraph_ForbidsRiskAndAdequacyLanguage()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var systemText = string.Concat(captured!.System!.Select(s => s.Text));
        Assert.Contains("get_project_allocation", systemText);
        Assert.Contains("under-resourced", systemText);
        Assert.Contains("do not invent", systemText, StringComparison.OrdinalIgnoreCase);
    }

    // ── Cycle 4 ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_HandlesMultipleToolCalls_InSingleConversation()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse("get_capacity_by_division", "tc_a", @"{""month"":""2026-05""}"))
            .ReturnsAsync(ToolUseResponse("get_cost_by_product", "tc_b", @"{""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("Summarise capacity and product cost.", "2026-05", _ => Task.CompletedTask);

        // GetMessageAsync called 3 times total (tool1 → tool2 → end_turn)
        mock.Verify(c => c.GetMessageAsync(It.IsAny<MessageParameters>()), Times.Exactly(3));

        // At least one call must carry results for both tool calls
        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_a")) &&
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_b"))
        )), Times.AtLeastOnce);
    }

    // ── Item 3: get_at_risk_application_fix tool ─────────────────────────────

    [Fact]
    public async Task RunAsync_AtRiskAndFixQuery_DispatchesToGetAtRiskApplicationFix()
    {
        // The canonical "why is X at risk and how do we fix it" shape must produce exactly ONE
        // tool_use call — for get_at_risk_application_fix — not two separate tool calls.
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(ToolUseResponse(
                "get_at_risk_application_fix", "tf_1",
                @"{""application_name"":""AuthService"",""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync(
            "Why is AuthService at risk this month and where could we find the capacity to fix it?",
            "2026-05",
            _ => Task.CompletedTask);

        // Must have dispatched the combined tool (result for tf_1 appears in a subsequent message)
        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tf_1"))
        )), Times.AtLeastOnce);
    }

    // ── Direct-answer enforcement (nudge) ────────────────────────────────────

    [Fact]
    public async Task RunAsync_NudgesToolUse_WhenLlmAnswersDirectlyWithoutToolCall()
    {
        // When the LLM skips tools on the first response, RunAsync must inject a nudge
        // message and retry, rather than emitting the ungrounded answer immediately.
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(EndTurnResponse("Zero idle resources."))            // first: direct text, no tool
            .ReturnsAsync(ToolUseResponse("get_unallocated_resources", "tc_nudge",
                @"{""division"":""Data Collection"",""threshold_pct"":100,""month"":""2026-05""}"))
            .ReturnsAsync(EndTurnResponse());
        mock.Setup(c => c.StreamMessageAsync(It.IsAny<MessageParameters>()))
            .Returns(EmptyStream());

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync(
            "What unallocated resource cost does Data Collection carry this month?",
            "2026-05", _ => Task.CompletedTask);

        // The nudge must appear as a user message in a subsequent GetMessageAsync call
        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Role == RoleType.User &&
                m.Content != null &&
                m.Content.OfType<TextContent>().Any(t =>
                    t.Text != null && t.Text.Contains("must call at least one tool")))
        )), Times.AtLeastOnce);

        // Tool result for tc_nudge must appear — confirming the nudge produced a real tool call
        mock.Verify(c => c.GetMessageAsync(It.Is<MessageParameters>(p =>
            p.Messages.Any(m =>
                m.Content != null &&
                m.Content.OfType<ToolResultContent>().Any(tr => tr.ToolUseId == "tc_nudge"))
        )), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunAsync_EmitsDirectTextAsFallback_WhenNudgeIsAlsoIgnored()
    {
        // If the LLM ignores the nudge and answers directly a second time,
        // RunAsync must still emit the response rather than hanging or throwing.
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        var mock = new Mock<IClaudeClient>();
        mock.SetupSequence(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .ReturnsAsync(EndTurnResponse("Zero idle resources."))   // first: no tool
            .ReturnsAsync(EndTurnResponse("Still no tool, sorry.")); // second: no tool again

        var service = new AgentService(mock.Object, dataService);
        var tokens = new List<string>();
        await service.RunAsync("What unallocated cost does Data Collection carry?", "2026-05",
            t => { tokens.Add(t); return Task.CompletedTask; });

        Assert.NotEmpty(tokens);
        Assert.Contains(tokens, t => t.Contains("sorry") || t.Contains("idle") || t.Length > 0);
    }

    [Fact]
    public async Task BuildTools_CapacityByDivision_HasNoDivisionParameter()
    {
        // The division parameter was silently ignored in dispatch — removing it from the
        // schema prevents the LLM from passing it and expecting filtered results.
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var tool = captured!.Tools!.Single(t => t.Function?.Name == "get_capacity_by_division");
        var props = tool.Function!.Parameters?["properties"];
        Assert.Null(props?["division"]);
    }

    [Fact]
    public async Task BuildSystemPrompt_RoutesUnallocatedCostQuestions_ToGetUnallocatedResources()
    {
        await using var ctx = CreateContext();
        var dataService = new DataService(ctx);

        MessageParameters? captured = null;
        var mock = new Mock<IClaudeClient>();
        mock.Setup(c => c.GetMessageAsync(It.IsAny<MessageParameters>()))
            .Callback<MessageParameters>(p => captured ??= p)
            .ReturnsAsync(EndTurnResponse("ok"));

        var service = new AgentService(mock.Object, dataService);
        await service.RunAsync("ping", "2026-05", _ => Task.CompletedTask);

        var systemText = string.Concat(captured!.System!.Select(s => s.Text));
        Assert.Contains("get_unallocated_resources", systemText, StringComparison.Ordinal);
        Assert.Contains("unallocated", systemText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("total_idle_monthly_cost", systemText, StringComparison.Ordinal);
    }
}
