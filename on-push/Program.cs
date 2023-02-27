using System.Reflection;
using devrating.factory;
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

var m = new StabilityMetric(loggerFactory, new DefaultFormula(), args[4]);

Diff? d = null;

foreach (var pr in await gh.RecentMergedPrs())
    if (!m.IsCommitApplied(gh.Owner(), gh.Repository(), pr.Oid))
        m.Apply(d =
            new GitDiff(
                log: loggerFactory,
                @base:
                    new GitProcess(
                        log: loggerFactory,
                        filename: "git",
                        arguments: new[] {
                            "rev-parse",
                            $"{pr.Oid}~",
                        },
                        directory: args[2])
                    .Output()
                    .First(),
                commit:
                    new GitProcess(
                        log: loggerFactory,
                        filename: "git",
                        arguments: new[] {
                            "rev-parse",
                            pr.Oid,
                        },
                        directory: args[2])
                    .Output()
                    .First(),
                since:
                    new GitLastMajorUpdateTag(
                        loggerFactory: loggerFactory,
                        repository: args[2],
                        before: pr.Oid)
                    .Sha(),
                repository: args[2],
                key: gh.Repository(),
                link: pr.Url,
                organization: gh.Owner(),
                createdAt: pr.MergedAt!.Value,
                paths: args[6..]));

if (d != null)
    m.Report(d).Write(args[5]);