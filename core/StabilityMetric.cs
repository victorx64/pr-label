using devrating.entity;
using devrating.factory;
using devrating.sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

public sealed class StabilityMetric
{
    private readonly ILoggerFactory loggerFactory;
    private readonly Formula formula;
    private readonly Database database;

    public StabilityMetric(ILoggerFactory loggerFactory, Formula formula, string database)
    {
        this.loggerFactory = loggerFactory;
        this.formula = formula;
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
                return false;

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

    public Report Report(Diff diff)
    {
        var logger = loggerFactory.CreateLogger<StabilityMetric>();

        logger.LogInformation(new EventId(1146241), $"Diff: `{diff.ToJson()}`");

        database.Instance().Connection().Open();

        using var transaction = database.Instance().Connection().BeginTransaction();

        try
        {
            return new Report(
                database,
                formula,
                diff.RelatedWork(database.Entities().Works()));
        }
        finally
        {
            transaction.Rollback();
            database.Instance().Connection().Close();
        }
    }

    private Work NewWork(Diff diff)
    {
        var authorFactory = new DefaultAuthorFactory(database.Entities().Authors());
        var ratings = database.Entities().Ratings();

        return diff.NewWork(
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
    }
}