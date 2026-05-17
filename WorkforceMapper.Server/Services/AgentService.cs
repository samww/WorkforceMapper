using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;

namespace WorkforceMapper.Server.Services;

public class AgentService(IClaudeClient claude, WorkforceToolCatalog toolCatalog)
{
    public async Task RunAsync(string query, string month, Func<string, Task> onToken,
        CancellationToken ct = default)
    {
        var messages = new List<Message> { new(RoleType.User, query) };
        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude45Haiku,
            MaxTokens = 4096,
            Temperature = 0,
            System = [new SystemMessage(toolCatalog.BuildSystemPrompt(month), null)],
            Tools = toolCatalog.BuildTools(),
            Messages = messages,
            Stream = false
        };

        var toolsDispatched = false;
        var nudgedOnce = false;

        while (true)
        {
            var response = await claude.GetMessageAsync(parameters);

            if (response.StopReason != "tool_use")
            {
                if (!toolsDispatched)
                {
                    if (!nudgedOnce)
                    {
                        nudgedOnce = true;
                        messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content ?? [] });
                        messages.Add(new Message(RoleType.User,
                            "You must call at least one tool before answering. Please call the appropriate tool now."));
                        parameters.Messages = messages;
                        continue;
                    }

                    var directText = string.Concat(
                        (response.Content ?? []).OfType<TextContent>().Select(t => t.Text));
                    if (directText.Length > 0)
                        await onToken(directText);
                    return;
                }
                break;
            }

            toolsDispatched = true;
            messages.Add(new Message { Role = RoleType.Assistant, Content = response.Content ?? [] });

            var toolResults = new List<ContentBase>();
            foreach (var toolUse in (response.Content ?? []).OfType<ToolUseContent>())
            {
                var result = await toolCatalog.DispatchAsync(toolUse, month);
                toolResults.Add(new ToolResultContent
                {
                    ToolUseId = toolUse.Id,
                    Content = [new TextContent { Text = result }]
                });
            }

            messages.Add(new Message { Role = RoleType.User, Content = toolResults });
            parameters.Messages = messages;
        }

        parameters.Stream = true;
        parameters.Tools = null;

        await foreach (var chunk in claude.StreamMessageAsync(parameters).WithCancellation(ct))
        {
            if (chunk.Delta?.Text is { Length: > 0 } text)
                await onToken(text);
        }
    }
}
