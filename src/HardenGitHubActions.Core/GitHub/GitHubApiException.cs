namespace HardenGitHubActions.Core.GitHub;

public sealed class GitHubApiException : Exception
{
    public int StatusCode { get; }

    public GitHubApiException()
    {
    }

    public GitHubApiException(string message)
        : base(message)
    {
    }

    public GitHubApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GitHubApiException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
