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
