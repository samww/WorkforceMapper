using Anthropic.SDK;
using Anthropic.SDK.Messaging;

namespace WorkforceMapper.Server.Services;

public class AnthropicClaudeClient(AnthropicClient client) : IClaudeClient
{
    public Task<MessageResponse> GetMessageAsync(MessageParameters parameters)
        => client.Messages.GetClaudeMessageAsync(parameters);

    public IAsyncEnumerable<MessageResponse> StreamMessageAsync(MessageParameters parameters)
        => client.Messages.StreamClaudeMessageAsync(parameters);
}
