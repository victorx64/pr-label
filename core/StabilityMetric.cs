using devrating.entity;
using devrating.factory;
using devrating.sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

public sealed class StabilityMetric
{
    private readonly ILoggerFactory loggerFactory;
    private readonly Formula formula = new DefaultFormula();
    private readonly Database database;

    public StabilityMetric(ILoggerFactory loggerFactory, string database)
    {
        this.loggerFactory = loggerFactory;
        this.database =
            new SqliteDatabase(
                new TransactedDbConnection(
                    new SqliteConnection($"Data Source='{database}'")));
    }

    public bool IsCommitApplied(string organization, string repository, string commit)
    {
        var logger = loggerFactory.CreateLogger<StabilityMetric>();

        database.Instance().Connection().Open();

        using var transaction = database.Instance().Connection().BeginTransaction();

        try
        {
            if (!database.Instance().Present())
            {
                logger.LogInformation(
                    new EventId(1932471),
                    $"The DB is not present. Creating");

                database.Instance().Create();
            }

            return database.Entities().Works().ContainsOperation().Contains(organization, repository, commit);
        }
        finally
        {
            transaction.Rollback();
            database.Instance().Connection().Close();
        }
    }

    public void Apply(Diff diff)
    {
        var logger = loggerFactory.CreateLogger<StabilityMetric>();

        logger.LogInformation(
            new EventId(1461486),
            $"Applying diff: `{diff.ToJson()}`");

        database.Instance().Connection().Open();

        using var transaction = database.Instance().Connection().BeginTransaction();

        try
        {
            if (!database.Instance().Present())
            {
                logger.LogInformation(
                    new EventId(1824416),
                    $"The DB is not present. Creating");

                database.Instance().Create();
            }

            if (diff.PresentIn(database.Entities().Works()))
            {
                logger.LogInformation(
                    new EventId(1435402),
                    $"The diff is already applied. Skipping");
                return;
            }

            NewWork(diff);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
        finally
        {
            database.Instance().Connection().Close();
        }
    }

    public double ValueFor(Diff diff)
    {
        var logger = loggerFactory.CreateLogger<StabilityMetric>();

        logger.LogInformation(new EventId(1146241), $"Diff: `{diff.ToJson()}`");

        database.Instance().Connection().Open();

        using var transaction = database.Instance().Connection().BeginTransaction();

        try
        {
            if (!database.Instance().Present())
            {
                logger.LogInformation(
                    new EventId(1955253),
                    $"The DB is not present. Creating");

                database.Instance().Create();
            }

            var rating = NewWork(diff).UsedRating();
            return rating.Id().Filled() ? rating.Value() : formula.DefaultRating();
        }
        finally
        {
            transaction.Rollback();
            database.Instance().Connection().Close();
        }
    }

    private Work NewWork(Diff diff)
    {
        var authors = database.Entities().Authors();
        var authorFactory = new DefaultAuthorFactory(authors);
        var ratings = database.Entities().Ratings();

        var w = diff.NewWork(
            new DefaultFactories(
                authorFactory,
                new DefaultWorkFactory(
                    database.Entities().Works(),
                    ratings,
                    authorFactory),
                new DefaultRatingFactory(
                    loggerFactory,
                    authorFactory,
                    ratings,
                    formula)));

        var logger = loggerFactory.CreateLogger<StabilityMetric>();
        logger.LogInformation(new EventId(1437603), "Rating | Author");
        logger.LogInformation(new EventId(1437603), "------ | ------");

        foreach (var author in authors
            .GetOperation()
            .Top(
                w.Author().Organization(),
                w.Author().Repository(),
                DateTimeOffset.UtcNow.AddDays(-90)))
        {
            var rating = database.Entities().Ratings().GetOperation().RatingOf(author.Id()).Value();

            logger.LogInformation(new EventId(1437603), $"{rating,6:F0} | <{author.Email()}>");
        }

        return w;
    }
}