using CommitApi.Extractors.Helpers;
using Xunit;

namespace CommitApi.Tests.Extractors;

public class EmailSignalsTests
{
    // ── HasActionSignal ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("action required: please review the doc")]
    [InlineData("please review the attached document")]
    [InlineData("fyi: here is the update")]
    [InlineData("follow up from yesterday's meeting")]
    [InlineData("reminder: deadline is Friday")]
    public void HasActionSignal_ActionSubjectPrefixes_ReturnsTrue(string text)
    {
        Assert.True(EmailSignals.HasActionSignal(text));
    }

    [Theory]
    [InlineData("please do this now")]
    [InlineData("can you review the document?")]
    [InlineData("could you share the file?")]
    [InlineData("this is an action item for you")]
    [InlineData("follow up on the request")]
    [InlineData("by when should this be done?")]
    [InlineData("deadline is next Friday")]
    public void HasActionSignal_BodySignals_ReturnsTrue(string text)
    {
        Assert.True(EmailSignals.HasActionSignal(text));
    }

    [Fact]
    public void HasActionSignal_NoSignal_ReturnsFalse()
    {
        Assert.False(EmailSignals.HasActionSignal("just a regular update email"));
    }

    [Fact]
    public void HasActionSignal_CaseInsensitive()
    {
        Assert.True(EmailSignals.HasActionSignal("ACTION REQUIRED: URGENT"));
    }

    // ── NormalizeSubject ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Re: Meeting follow-up",      "Meeting follow-up")]
    [InlineData("RE: Q3 planning",            "Q3 planning")]
    [InlineData("Fwd: Important doc",         "Important doc")]
    [InlineData("FW: Review needed",          "Review needed")]
    [InlineData("FWD: Action required",       "Action required")]
    public void NormalizeSubject_StripsPrefixes(string input, string expected)
    {
        Assert.Equal(expected, EmailSignals.NormalizeSubject(input));
    }

    [Fact]
    public void NormalizeSubject_LongSubject_Truncated()
    {
        var long_ = new string('x', 110);
        var result = EmailSignals.NormalizeSubject(long_);
        Assert.True(result.Length <= 103); // 100 + "…"
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void NormalizeSubject_NoPrefixShortSubject_Unchanged()
    {
        Assert.Equal("Project update", EmailSignals.NormalizeSubject("Project update"));
    }

    // ── InferDueDate ──────────────────────────────────────────────────────────

    [Fact]
    public void InferDueDate_ByEod_ReturnsToday18h()
    {
        var due = EmailSignals.InferDueDate("deadline by eod");
        Assert.NotNull(due);
        Assert.Equal(18, due!.Value.Hour);
    }

    [Fact]
    public void InferDueDate_Tomorrow_ReturnsTomorrow()
    {
        var due = EmailSignals.InferDueDate("need this tomorrow");
        Assert.NotNull(due);
        Assert.Equal(DateTimeOffset.UtcNow.AddDays(1).Date, due!.Value.Date);
    }

    [Fact]
    public void InferDueDate_ByFriday_ReturnsFriday()
    {
        var due = EmailSignals.InferDueDate("please send by friday");
        Assert.NotNull(due);
        Assert.Equal(DayOfWeek.Friday, due!.Value.DayOfWeek);
    }

    [Fact]
    public void InferDueDate_NextWeek_ReturnsMonday()
    {
        var due = EmailSignals.InferDueDate("please have by next week");
        Assert.NotNull(due);
        Assert.Equal(DayOfWeek.Monday, due!.Value.DayOfWeek);
    }

    [Fact]
    public void InferDueDate_NoSignal_ReturnsNull()
    {
        Assert.Null(EmailSignals.InferDueDate("please review this document"));
    }
}
