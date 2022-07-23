using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.Extensions.Logging;

internal class Program
{
    private static readonly HttpClient client = new HttpClient();
    private const string apiHost = "https://glacial-wave-63222.herokuapp.com";
    private static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(
        builder => builder
            .AddFilter("Microsoft", LogLevel.Warning)
            .AddFilter("System", LogLevel.Warning)
            .AddSystemdConsole()
    );

    private static async Task Main(string[] args)
    {
        var logger = loggerFactory.CreateLogger<Program>();
        var assemblyName = Assembly.GetExecutingAssembly().GetName();

        logger.LogInformation(new EventId(1003318), assemblyName.Version?.ToString());

        if (args.Length < 9)
        {
            logger.LogInformation(new EventId(1415543), "Not enough arguments. Probably mergeCommitSha is empty");
            return;
        }

        var devratingOrganization = args[0];
        var devratingKey = args[1];
        var ownerAndRepository = args[2];
        var githubToken = args[3];
        var workspace = args[4];
        var baseBranch = args[5];
        var number = int.Parse(args[6]);
        var merged = bool.Parse(args[7]);
        var mergeCommitSha = args[8];

        var githubGql = new Octokit.GraphQL.Connection(
            new Octokit.GraphQL.ProductHeaderValue(
                assemblyName.Name,
                assemblyName.Version!.ToString()
            ),
            githubToken
        );

        await new MergedPullRequests(
            loggerFactory,
            client,
            githubGql,
            apiHost,
            devratingOrganization,
            devratingKey,
            ownerAndRepository,
            workspace,
            baseBranch
        ).UpdateRatings();

        var stats = await (
            merged
            ? (PullRequestStats)new MergedPullRequestStats(
                client,
                apiHost,
                devratingOrganization,
                ownerAndRepository,
                mergeCommitSha
            )
            : (PullRequestStats)new OpenPullRequestStats(
                loggerFactory,
                client,
                apiHost,
                devratingOrganization,
                devratingKey,
                ownerAndRepository,
                mergeCommitSha,
                workspace,
                number
            )
        ).AsJsonNode();

        var owner = ownerAndRepository.Split('/')[0];
        var repository = ownerAndRepository.Split('/')[1];

        await new Comment(
            githubGql,
            owner,
            repository,
            number
        ).Update(
            merged
            ? MergedPrCommentBody(stats, devratingOrganization, ownerAndRepository)
            : OpenPrCommentBody(stats, devratingOrganization, ownerAndRepository)
        );

        var githubRest = new Octokit.GitHubClient(
            new Octokit.ProductHeaderValue(
                assemblyName.Name,
                assemblyName.Version!.ToString()
            )
        )
        { Credentials = new Octokit.Credentials(githubToken) };

        await new SizeLabel(
            githubRest,
            owner,
            repository,
            number
        ).Update();

        await new StabilityLabel(
            githubRest,
            owner,
            repository,
            number
        ).Update(stats);

        Console.WriteLine(
            $"Visit  https://devrating.net/#/repositories/" +
            HttpUtility.UrlEncode(devratingOrganization) + "/" +
            HttpUtility.UrlEncode(ownerAndRepository)
        );
    }

    private static string OpenPrCommentBody(JsonNode stats, string devratingOrganization, string ownerAndRepository)
    {
        var s = stats["NewRating"]?.GetValue<double>() ??
            stats["UsedRating"]?.GetValue<double>() ??
            new devrating.factory.DefaultFormula().DefaultRating();

        var b = new StringBuilder();
        b.AppendLine($"Rating: `{s:f2}`");
        b.AppendLine($"Author: `{stats["AuthorEmail"]!.GetValue<string>()}`");
        b.AppendLine($"[Repository information](https://devrating.net/#/repositories/" +
            HttpUtility.UrlEncode(devratingOrganization) + "/" +
            HttpUtility.UrlEncode(ownerAndRepository) + ")");
        return b.ToString();
    }

    private static string MergedPrCommentBody(JsonNode stats, string devratingOrganization, string ownerAndRepository)
    {
        var b = new StringBuilder(OpenPrCommentBody(stats, devratingOrganization, ownerAndRepository));
        b.AppendLine($"[Pull request details](https://devrating.net/#/works/{stats["Id"]!.GetValue<string>()})");
        return b.ToString();
    }
}