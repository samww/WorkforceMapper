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
        Assert.Contains("same-division", systemText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cross-divisional", systemText, StringComparison.OrdinalIgnoreCase);
        // same-division guidance must appear before cross-divisional guidance
        Assert.True(
            systemText.IndexOf("same-division", StringComparison.OrdinalIgnoreCase) <
            systemText.IndexOf("cross-divisional", StringComparison.OrdinalIgnoreCase));
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
        Assert.Contains("cumulative_gap_coverage_pct", systemText, StringComparison.Ordinal);
        Assert.Contains("100", systemText, StringComparison.Ordinal);
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
        // Must reference the per-row flag and the pre-computed summary
        Assert.Contains("in_fewest_changes_combination", systemText, StringComparison.Ordinal);
        Assert.Contains("fewest_changes_combination", systemText, StringComparison.Ordinal);
        // Must forbid presenting the stopping row in isolation
        Assert.Contains("final row", systemText, StringComparison.OrdinalIgnoreCase);
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
}
