using System.Text.Json.Nodes;
using System.Web;

public sealed class MergedPullRequestStats : PullRequestStats
{
    private readonly HttpClient client;
    private readonly string apiHost;
    private readonly string devratingOrganization;
    private readonly string ownerAndRepository;
    private readonly string mergeCommitSha;

    public MergedPullRequestStats(
        HttpClient client,
        string apiHost,
        string devratingOrganization,
        string ownerAndRepository,
        string mergeCommitSha)
    {
        this.devratingOrganization = devratingOrganization;
        this.ownerAndRepository = ownerAndRepository;
        this.mergeCommitSha = mergeCommitSha;
        this.client = client;
        this.apiHost = apiHost;
    }

    public async Task<JsonNode> AsJsonNode() => JsonNode.Parse(await client.GetStringAsync(
            $"{apiHost}/works/merged?organization={HttpUtility.UrlEncode(devratingOrganization)}" +
            $"&repository={HttpUtility.UrlEncode(ownerAndRepository)}" +
            $"&merge={mergeCommitSha}"
        )
    )!;
}