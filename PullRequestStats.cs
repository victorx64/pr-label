using System.Text.Json.Nodes;

public interface PullRequestStats
{
    Task<JsonNode> AsJsonNode();
}