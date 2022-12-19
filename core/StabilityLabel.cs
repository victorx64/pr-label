using Octokit;

public sealed class StabilityLabel
{
    private readonly IGitHubClient github;
    private readonly string owner;
    private readonly string repository;
    private readonly int number;
    private readonly devrating.factory.Formula formula = new devrating.factory.DefaultFormula();
    private readonly NewLabel[] labels = new[]{
        new NewLabel("stability/low", "6C757C"),
        new NewLabel("stability/medium", "F8F9FA"),
        new NewLabel("stability/high", "0E74F7"),
    };

    public StabilityLabel(
        IGitHubClient github,
        string owner,
        string repository,
        int number)
    {
        this.github = github;
        this.owner = owner;
        this.repository = repository;
        this.number = number;
    }

    public async Task Update(double rating)
    {
        await CreateLabelsForRepository();

        var rank = (int)(RatingPercentile(rating) * 3d);

        var issueLabels = await github.Issue.Labels.GetAllForIssue(owner, repository, number);

        foreach (var label in labels)
            if (label != labels[rank] &&
                issueLabels.Any(l => l.Name.Equals(label.Name, StringComparison.OrdinalIgnoreCase)))
                await github.Issue.Labels.RemoveFromIssue(owner, repository, number, label.Name);

        if (!issueLabels.Any(l => l.Equals(labels[rank])))
            await github.Issue.Labels.AddToIssue(
                owner,
                repository,
                number,
                new[] { labels[rank].Name });
    }

    private async Task CreateLabelsForRepository()
    {
        var repositoryLabels = await github.Issue.Labels.GetAllForRepository(owner, repository);

        foreach (var label in labels)
            if (!repositoryLabels.Any(l => l.Name.Equals(label.Name, StringComparison.OrdinalIgnoreCase)))
                await github.Issue.Labels.Create(owner, repository, label);
    }

    private double RatingPercentile(double a)
    {
        const int n = 400;

        return Math.Pow(10, a / n) / (Math.Pow(10, a / n) + Math.Pow(10, formula.DefaultRating() / n));
    }
}