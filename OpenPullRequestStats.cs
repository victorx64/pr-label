using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using devrating.git;
using Microsoft.Extensions.Logging;

public sealed class OpenPullRequestStats : PullRequestStats
{
    private readonly ILoggerFactory loggerFactory;
    private readonly HttpClient client;
    private readonly string apiHost;
    private readonly string devratingOrganization;
    private readonly string devratingKey;
    private readonly string ownerAndRepository;
    private readonly string mergeCommitSha;
    private readonly string workspace;
    private readonly int number;

    public OpenPullRequestStats(
        ILoggerFactory loggerFactory,
        HttpClient client,
        string apiHost,
        string devratingOrganization,
        string devratingKey,
        string ownerAndRepository,
        string mergeCommitSha,
        string workspace,
        int number)
    {
        this.devratingOrganization = devratingOrganization;
        this.devratingKey = devratingKey;
        this.ownerAndRepository = ownerAndRepository;
        this.mergeCommitSha = mergeCommitSha;
        this.workspace = workspace;
        this.number = number;
        this.loggerFactory = loggerFactory;
        this.client = client;
        this.apiHost = apiHost;
    }

    public async Task<JsonNode> AsJsonNode()
    {
        var c = new StringContent(
            new GitDiff(
                loggerFactory,
                new GitProcess(loggerFactory, "git", $"rev-parse {mergeCommitSha}~", workspace).Output().First(),
                new GitProcess(loggerFactory, "git", $"rev-parse {mergeCommitSha}", workspace).Output().First(),
                new GitLastMajorUpdateTag(loggerFactory, workspace, mergeCommitSha).Sha(),
                workspace,
                ownerAndRepository,
                $"https://github.com/{ownerAndRepository}/pull/{number}",
                devratingOrganization,
                DateTimeOffset.UtcNow
            ).ToJson()
        );

        c.Headers.Add("key", devratingKey);
        c.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var r = await (await client.PostAsync($"{apiHost}/diffs/key/hallucination", c)).Content.ReadAsStringAsync();

        return JsonNode.Parse(r)!;
    }
}