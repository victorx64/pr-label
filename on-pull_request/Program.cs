using System.Reflection;
using devrating.git;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(
    builder => builder
        .AddFilter("Microsoft", LogLevel.Warning)
        .AddFilter("System", LogLevel.Warning)
        .AddSystemdConsole());

loggerFactory.CreateLogger<Program>().LogInformation(
    new EventId(1511174),
    Assembly.GetExecutingAssembly().GetName().Version?.ToString());

var gh = new GitHubClient(
    githubToken: args[1],
    logger: loggerFactory,
    baseBranch: args[3],
    ownerAndRepository: args[0]);

var d = new GitDiff(
    log: loggerFactory,
    @base: new GitProcess(
            log: loggerFactory,
            filename: "git",
            arguments: new[] {
                "rev-parse",
                args[5],
            },
            directory: args[2])
        .Output()
        .First(),
    commit: new GitProcess(
            log: loggerFactory,
            filename: "git",
            arguments: new[] {
                "rev-parse",
                args[6],
            },
            directory: args[2])
        .Output()
        .First(),
    since: new GitLastMajorUpdateTag(
            loggerFactory: loggerFactory,
            repository: args[2],
            before: args[6])
        .Sha(),
    repository: args[2],
    key: gh.Repository(),
    link: $"{gh.Owner()}/{gh.Repository()}#{args[4]}",
    organization: gh.Owner(),
    createdAt: DateTimeOffset.UtcNow,
    paths: args[8..]);

var r = new StabilityMetric(loggerFactory, args[7]).ValueFor(diff: d);

await gh.UpdatePrLabels(
    size: d.Additions(),
    rating: r,
    prNumber: int.Parse(args[4]));

loggerFactory.CreateLogger<Program>().LogInformation(
    new EventId(1833095),
    $"Reward: {new Reward().Value(r):F2}");
