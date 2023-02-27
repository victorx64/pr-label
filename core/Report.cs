using System.Text;
using devrating.entity;
using devrating.factory;

public sealed class Report
{
    public uint Reward { get; init; }
    public double UsedRating { get; init; }
    private readonly StringBuilder content;

    public Report(Database database, Formula formula, Work work)
    {
        var ur = work.UsedRating();

        UsedRating = ur.Id().Filled() ? ur.Value() : formula.DefaultRating();
        Reward = new Reward(formula).Value(UsedRating);

        content = new StringBuilder();
        content.AppendLine($"PR: {work.Link()}  ");
        content.AppendLine($"Author: <{work.Author().Email()}>  ");
        content.AppendLine($"Previous Rating: {UsedRating:F0}  ");
        content.AppendLine($"XP: +{Reward}  ");
        content.AppendLine();

        content.AppendLine("Rating | Author");
        content.AppendLine("------ | ------");

        foreach (var author in database
            .Entities()
            .Authors()
            .GetOperation()
            .Top(
                work.Author().Organization(),
                work.Author().Repository(),
                DateTimeOffset.UtcNow.AddDays(-90)))
        {
            var rating = database.Entities().Ratings().GetOperation().RatingOf(author.Id()).Value();

            content.AppendLine($"{rating,6:F0} | <{author.Email()}>");
        }
    }

    public void Write(string path) => File.WriteAllText(path, content.ToString());
}