using CommitApi.Extractors.Helpers;
using Xunit;

namespace CommitApi.Tests.Extractors;

public class ChatSignalsTests
{
    // ── HasActionSignal ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("I'll send you the report tomorrow")]
    [InlineData("i will do it")]
    [InlineData("Will do, thanks")]
    [InlineData("I will send you the update by Friday")]
    [InlineData("will review the PR today")]
    [InlineData("will fix the bug")]
    [InlineData("will submit the form")]
    [InlineData("on it!")]
    [InlineData("taking this one")]
    [InlineData("I own this task")]
    public void HasActionSignal_KnownActionPhrases_ReturnsTrue(string text)
    {
        Assert.True(ChatSignals.HasActionSignal(text));
    }

    [Theory]
    [InlineData("by eod please")]
    [InlineData("by end of day")]
    [InlineData("by tomorrow morning")]
    [InlineData("by friday end of day")]
    [InlineData("by monday")]
    [InlineData("by next week")]
    [InlineData("by 8th march")]
    [InlineData("by march 8")]
    public void HasActionSignal_DeadlinePhrases_ReturnsTrue(string text)
    {
        Assert.True(ChatSignals.HasActionSignal(text));
    }

    [Fact]
    public void HasActionSignal_NoSignal_ReturnsFalse()
    {
        Assert.False(ChatSignals.HasActionSignal("sounds good, let's talk later"));
    }

    [Fact]
    public void HasActionSignal_CaseInsensitive()
    {
        Assert.True(ChatSignals.HasActionSignal("I'LL GET THIS DONE"));
    }

    [Fact]
    public void HasActionSignal_HtmlBody_DetectsSignal()
    {
        Assert.True(ChatSignals.HasActionSignal("<p>I'll send the report</p>"));
    }

    // ── InferTitle ────────────────────────────────────────────────────────────

    [Fact]
    public void InferTitle_UsesFirstActionSignalSentence()
    {
        const string text = "Some intro sentence. I'll fix the bug by Friday. More info here.";
        var title = ChatSignals.InferTitle(text);
        Assert.Contains("I'll fix", title);
    }

    [Fact]
    public void InferTitle_FallsBackToFirstSentence_WhenNoSignal()
    {
        const string text = "This is the first sentence. This is the second.";
        var title = ChatSignals.InferTitle(text);
        Assert.Equal("This is the first sentence", title);
    }

    [Fact]
    public void InferTitle_TruncatesAt80Chars()
    {
        var longText = new string('x', 100);
        var title = ChatSignals.InferTitle(longText);
        Assert.True(title.Length <= 82); // 80 + "…"
        Assert.EndsWith("…", title);
    }

    // ── InferDueDate ──────────────────────────────────────────────────────────

    [Fact]
    public void InferDueDate_ByEod_ReturnsToday18h()
    {
        var due = ChatSignals.InferDueDate("I'll do it by eod");
        Assert.NotNull(due);
        Assert.Equal(18, due!.Value.Hour);
    }

    [Fact]
    public void InferDueDate_Tomorrow_ReturnsTomorrow18h()
    {
        var due = ChatSignals.InferDueDate("I'll send it tomorrow");
        Assert.NotNull(due);
        Assert.Equal(DateTimeOffset.UtcNow.AddDays(1).Date, due!.Value.Date);
    }

    [Fact]
    public void InferDueDate_ByFriday_ReturnsFriday()
    {
        var due = ChatSignals.InferDueDate("I'll finish by friday");
        Assert.NotNull(due);
        Assert.Equal(DayOfWeek.Friday, due!.Value.DayOfWeek);
    }

    [Fact]
    public void InferDueDate_ByMonday_ReturnsNextMonday()
    {
        var due = ChatSignals.InferDueDate("I'll do it by monday");
        Assert.NotNull(due);
        Assert.Equal(DayOfWeek.Monday, due!.Value.DayOfWeek);
    }

    [Fact]
    public void InferDueDate_ByNextWeek_ReturnsNextMonday()
    {
        var due = ChatSignals.InferDueDate("by next week");
        Assert.NotNull(due);
        Assert.Equal(DayOfWeek.Monday, due!.Value.DayOfWeek);
    }

    [Fact]
    public void InferDueDate_By8thMarch_ParsesDate()
    {
        var due = ChatSignals.InferDueDate("I'll do it by 8th march");
        Assert.NotNull(due);
        Assert.Equal(8, due!.Value.Day);
        Assert.Equal(3, due!.Value.Month);
    }

    [Fact]
    public void InferDueDate_ByMarch8_ParsesDate()
    {
        var due = ChatSignals.InferDueDate("I'll send it by march 8");
        Assert.NotNull(due);
        Assert.Equal(8, due!.Value.Day);
        Assert.Equal(3, due!.Value.Month);
    }

    [Fact]
    public void InferDueDate_MonthInPast_ReturnsNextYear()
    {
        // Pick a month guaranteed to be in the past (January if we're past January)
        var now = DateTimeOffset.UtcNow;
        // Use a month that is always in the past relative to test execution in March 2026
        var due = ChatSignals.InferDueDate("by january 1st");
        Assert.NotNull(due);
        Assert.True(due!.Value >= now);
    }

    [Fact]
    public void InferDueDate_NoSignal_ReturnsNull()
    {
        Assert.Null(ChatSignals.InferDueDate("sounds good, see you later"));
    }

    // ── StripHtml ─────────────────────────────────────────────────────────────

    [Fact]
    public void StripHtml_RemovesTags()
    {
        Assert.Equal("Hello World", ChatSignals.StripHtml("<p>Hello</p> <b>World</b>").Trim());
    }

    [Fact]
    public void StripHtml_NoTags_ReturnsOriginal()
    {
        Assert.Equal("plain text", ChatSignals.StripHtml("plain text"));
    }

    [Fact]
    public void StripHtml_NestedTags_Stripped()
    {
        var result = ChatSignals.StripHtml("<div><span>text</span></div>");
        Assert.DoesNotContain("<", result);
    }
}
