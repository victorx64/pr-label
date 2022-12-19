using Octokit;

public sealed class SizeLabel
{
    private readonly IGitHubClient github;
    private readonly string owner;
    private readonly string repository;
    private readonly int number;
    private readonly NewLabel[] labels = new[]{
        new NewLabel("extra small", "F8F9FA"),
        new NewLabel("small", "218757"),
        new NewLabel("medium", "FEBE36"),
        new NewLabel("large", "DA2B47"),
    };

    public SizeLabel(
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

    public async Task Update()
    {
        await CreateLabelsForRepository();

        var additions = (await github.PullRequest.Get(owner, repository, number)).Additions;

        var suggested = 25;

        var labelId = 3;

        if (additions < suggested)
            labelId = 0;
        else if (additions < suggested * 3)
            labelId = 1;
        else if (additions < suggested * 6)
            labelId = 2;

        var issueLabels = await github.Issue.Labels.GetAllForIssue(owner, repository, number);

        foreach (var label in labels)
            if (label != labels[labelId] &&
                issueLabels.Any(l => l.Name.Equals(label.Name, StringComparison.OrdinalIgnoreCase)))
                await github.Issue.Labels.RemoveFromIssue(owner, repository, number, label.Name);

        if (!issueLabels.Any(l => l.Equals(labels[labelId])))
            await github.Issue.Labels.AddToIssue(
                owner,
                repository,
                number,
                new[] { labels[labelId].Name });
    }

    private async Task CreateLabelsForRepository()
    {
        var repositoryLabels = await github.Issue.Labels.GetAllForRepository(owner, repository);

        foreach (var label in labels)
            if (!repositoryLabels.Any(l => l.Name.Equals(label.Name, StringComparison.OrdinalIgnoreCase)))
                await github.Issue.Labels.Create(owner, repository, label);
    }
}