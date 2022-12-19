using System.Reflection;
using Microsoft.Extensions.Logging;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

public sealed class GitHubClient
{
    private readonly ILoggerFactory loggerFactory;
    private readonly string githubToken;
    private readonly string baseBranch;
    private readonly string owner;
    private readonly string repository;

    public string Owner() => owner;
    public string Repository() => repository;

    public GitHubClient(
        string githubToken,
        ILoggerFactory logger,
        string baseBranch,
        string ownerAndRepository)
    {
        this.githubToken = githubToken;
        this.loggerFactory = logger;
        this.baseBranch = baseBranch;
        this.owner = ownerAndRepository.Split('/')[0];
        this.repository = ownerAndRepository.Split('/')[1];
    }

    public async Task<IEnumerable<PullRequestRecord>> RecentMergedPrs()
    {
        var q = $"repo:{owner}/{repository} base:{baseBranch} type:pr merged:>{DateTimeOffset.UtcNow.AddDays(-90):O} sort:updated-desc";

        loggerFactory.CreateLogger<GitHubClient>().LogInformation(
            new EventId(1880477),
            $"Last merged PRs search query: `{q}`");

        return (
            await new Octokit.GraphQL.Connection(
                new Octokit.GraphQL.ProductHeaderValue(
                    Assembly.GetExecutingAssembly().GetName().Name,
                    Assembly.GetExecutingAssembly().GetName().Version!.ToString()),
                githubToken)
            .Run(
                new Query()
                .Search(
                    q,
                    SearchType.Issue,
                    100)
                .Nodes
                .OfType<Octokit.GraphQL.Model.PullRequest>()
                .Select(
                    pr => new PullRequestRecord(
                        pr.MergeCommit.Oid,
                        pr.MergedAt,
                        pr.Url))
                .Compile()))
        .Reverse();
    }

    public async Task UpdatePrLabels(
        double rating,
        int prNumber)
    {
        var githubRest = new Octokit.GitHubClient(
           new Octokit.ProductHeaderValue(
               Assembly.GetExecutingAssembly().GetName().Name,
               Assembly.GetExecutingAssembly().GetName().Version!.ToString()))
        {
            Credentials = new Octokit.Credentials(githubToken)
        };

        await new SizeLabel(githubRest, owner, repository, prNumber).Update();
        await new StabilityLabel(githubRest, owner, repository, prNumber).Update(rating);
    }

    public record PullRequestRecord(string Oid, DateTimeOffset? MergedAt, string Url);
}