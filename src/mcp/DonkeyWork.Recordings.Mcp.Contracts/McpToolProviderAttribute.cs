namespace DonkeyWork.Recordings.Mcp.Contracts;

[AttributeUsage(AttributeTargets.Class)]
public sealed class McpToolProviderAttribute : Attribute
{
    public McpToolProvider Provider { get; set; } = McpToolProvider.DonkeyWork;
}
