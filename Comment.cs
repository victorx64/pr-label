using Octokit.GraphQL;
using Octokit.GraphQL.Model;

public sealed class Comment
{
    private const string commentStart = "## Stability";
    private readonly IConnection github;
    private readonly string owner;
    private readonly string repository;
    private readonly int number;

    public Comment(
        IConnection github,
        string owner,
        string repository,
        int number)
    {
        this.github = github;
        this.owner = owner;
        this.repository = repository;
        this.number = number;
    }

    public async Task Update(string body)
    {
        var c = (
            await github.Run(
                new Query()
                .Repository(
                    repository,
                    owner
                )
                .PullRequest(number)
                .Comments(
                    first: 50,
                    orderBy: new IssueCommentOrder
                    {
                        Field = IssueCommentOrderField.UpdatedAt,
                        Direction = OrderDirection.Desc
                    }
                )
                .Nodes
                .OfType<IssueComment>()
                .Select(
                    ic => new
                    {
                        ic.Id,
                        ic.Body
                    }
                )
                .Compile()
            )
        )
        .FirstOrDefault(c => c.Body.StartsWith(commentStart));

        if (c is object)
        {
            await UpdateComment(c.Id, body);
        }
        else
        {
            await CreateComment(number, body);
        }
    }

    private async Task UpdateComment(
        ID id,
        string comment)
    {
        await github.Run(
            new Mutation()
            .UpdateIssueComment(
                new UpdateIssueCommentInput
                {
                    Body = commentStart + "\n\n" + comment,
                    Id = id,
                }
            )
            .Select(ic => new { ic.ClientMutationId })
            .Compile()
        );
    }

    private async Task CreateComment(
        int number,
        string comment)
    {
        await github.Run(
            new Mutation()
            .AddComment(
                new AddCommentInput
                {
                    SubjectId = await github.Run(
                        new Query()
                        .Repository(
                            repository,
                            owner
                        )
                        .PullRequest(number)
                        .Select(
                            r => r.Id
                        )
                        .Compile()
                    ),
                    Body = commentStart + "\n\n" + comment,
                }
            )
            .Select(ac => new { ac.ClientMutationId })
            .Compile()
        );
    }
}