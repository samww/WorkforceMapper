using Anthropic.SDK.Messaging;

namespace WorkforceMapper.Server.Services;

public interface IClaudeClient
{
    Task<MessageResponse> GetMessageAsync(MessageParameters parameters);
    IAsyncEnumerable<MessageResponse> StreamMessageAsync(MessageParameters parameters);
}
