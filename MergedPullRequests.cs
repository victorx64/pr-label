using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Web;
using devrating.git;
using Microsoft.Extensions.Logging;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

public sealed class MergedPullRequests
{
    private readonly string apiHost;
    private readonly ILoggerFactory loggerFactory;
    private readonly HttpClient client;
    private readonly IConnection github;
    private readonly string devratingOrganization;
    private readonly string devratingKey;
    private readonly string ownerAndRepository;
    private readonly string workspace;
    private readonly string baseBranch;

    public MergedPullRequests(
        ILoggerFactory loggerFactory,
        HttpClient client,
        IConnection github,
        string apiHost,
        string devratingOrganization,
        string devratingKey,
        string ownerAndRepository,
        string workspace,
        string baseBranch)
    {
        this.loggerFactory = loggerFactory;
        this.client = client;
        this.github = github;
        this.devratingOrganization = devratingOrganization;
        this.devratingKey = devratingKey;
        this.ownerAndRepository = ownerAndRepository;
        this.workspace = workspace;
        this.baseBranch = baseBranch;
        this.apiHost = apiHost;
    }

    public async Task UpdateRatings()
    {
        await AnalyzeMergedPrs(await Last100MergedPrs(await LastWorkCreatedAt()));
    }

    private async Task AnalyzeMergedPrs(IEnumerable<PullRequestRecord> prs)
    {
        var logger = loggerFactory.CreateLogger<MergedPullRequests>();

        foreach (var pr in prs)
        {
            var commit = pr.Oid;

            logger.LogInformation(
                new EventId(1672777),
                $"[{pr.Url}] merge commit: `{commit}`"
            );

            var c = new StringContent(
                new GitDiff(
                    loggerFactory,
                    new GitProcess(loggerFactory, "git", $"rev-parse {commit}~", workspace).Output().First(),
                    new GitProcess(loggerFactory, "git", $"rev-parse {commit}", workspace).Output().First(),
                    new GitLastMajorUpdateTag(loggerFactory, workspace, commit).Sha(),
                    workspace,
                    ownerAndRepository,
                    pr.Url,
                    devratingOrganization,
                    pr.MergedAt!.Value
                ).ToJson()
            );

            c.Headers.Add("key", devratingKey);
            c.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            await client.PostAsync($"{apiHost}/diffs/key", c);
        }
    }

    private Task<IEnumerable<PullRequestRecord>> Last100MergedPrs(DateTimeOffset after) {

        var q = $"repo:{ownerAndRepository} base:{baseBranch} type:pr merged:>{after:O} sort:updated-desc";

        loggerFactory.CreateLogger<MergedPullRequests>().LogInformation(
            new EventId(1880477),
            $"Last merged PRs search query: `{q}`"
        );

        return github.Run(
            new Query()
                .Search(
                    q,
                    SearchType.Issue,
                    100
                )
                .Nodes
                .OfType<PullRequest>()
                .Select(
                    pr => new PullRequestRecord(
                        pr.MergeCommit.Oid,
                        pr.MergedAt,
                        pr.Url
                    )
                )
                .Compile()
        );
    }

    private async Task<DateTimeOffset> LastWorkCreatedAt()
    {
        var threeMonthsAgoIsh = DateTimeOffset.Now.Subtract(TimeSpan.FromDays(90));

        var jsonNode = JsonNode.Parse(
            await client.GetStringAsync(
                $"{apiHost}/works?organization={HttpUtility.UrlEncode(devratingOrganization)}" +
                $"&repository={HttpUtility.UrlEncode(ownerAndRepository)}" +
                $"&after={HttpUtility.UrlEncode(threeMonthsAgoIsh.ToString("O"))}"
            )
        )!;

        if (jsonNode.AsArray().Any())
        {
            return jsonNode[0]!["CreatedAt"]!.GetValue<DateTimeOffset>();
        }

        loggerFactory.CreateLogger<MergedPullRequests>().LogInformation(
            new EventId(1502039),
            $"Last Work created at: `{threeMonthsAgoIsh}`"
        );

        return threeMonthsAgoIsh;
    }

    internal record PullRequestRecord(string Oid, DateTimeOffset? MergedAt, string Url);
}