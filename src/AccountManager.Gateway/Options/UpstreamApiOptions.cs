namespace AccountManager.Gateway.Options;

public sealed class UpstreamApiOptions
{
    public const string SectionName = "UpstreamApis";

    public List<string> BaseAddresses { get; set; } = [];
}
