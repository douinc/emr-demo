using EmrDemo.Wpf;

namespace EmrDemo.Wpf.Tests;

public class TextCommitTests
{
    [Fact]
    public void UnchangedValue_IsNotCommitted()
    {
        var commit = new TextCommit();
        commit.Reset("기록");

        Assert.False(commit.TryCommit("기록"));
    }

    [Fact]
    public void ChangedValue_IsCommittedOnce()
    {
        var commit = new TextCommit();
        commit.Reset("");

        Assert.True(commit.TryCommit("새 기록"));
        Assert.False(commit.TryCommit("새 기록"));
    }

    [Fact]
    public void LineEndings_AreNormalizedBeforeComparing()
    {
        var commit = new TextCommit();
        commit.Reset("첫 줄\n둘째 줄");

        Assert.False(commit.TryCommit("첫 줄\r\n둘째 줄"));
        Assert.Equal("a\nb", TextCommit.Normalize("a\r\nb"));
    }
}

public class FormCommitsTests
{
    static readonly Dictionary<string, string> Loaded = new() { ["f.b0.0"] = "기존" };

    static FormCommits LoadedCommits()
    {
        var commits = new FormCommits();
        commits.BeginLoad();
        commits.Remember("f.b0.0", Loaded);
        commits.Remember("f.b0.1", Loaded);
        commits.EndLoad();
        return commits;
    }

    [Fact]
    public void WhileLoading_NothingIsCommitted()
    {
        var commits = new FormCommits();
        commits.BeginLoad();

        Assert.True(commits.Loading);
        Assert.False(commits.ShouldCommit("f.b0.0", "값"));
    }

    [Fact]
    public void ValueEqualToLoaded_IsNotCommitted()
    {
        var commits = LoadedCommits();

        Assert.False(commits.ShouldCommit("f.b0.0", "기존"));
        Assert.False(commits.ShouldCommit("f.b0.1", ""));
    }

    [Fact]
    public void ChangedValue_IsCommittedOnceThenAgainWhenItChangesBack()
    {
        var commits = LoadedCommits();

        Assert.True(commits.ShouldCommit("f.b0.0", "새 값"));
        Assert.False(commits.ShouldCommit("f.b0.0", "새 값"));
        Assert.True(commits.ShouldCommit("f.b0.0", "기존"));
    }

    [Fact]
    public void Reload_ForgetsEarlierCommits()
    {
        var commits = LoadedCommits();
        commits.ShouldCommit("f.b0.0", "새 값");

        commits.BeginLoad();
        commits.Remember("f.b0.0", Loaded);
        commits.EndLoad();

        Assert.False(commits.ShouldCommit("f.b0.0", "기존"));
        Assert.True(commits.ShouldCommit("f.b0.0", "새 값"));
    }
}
