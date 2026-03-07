using CommitApi.Extractors.Helpers;
using Xunit;

namespace CommitApi.Tests.Extractors;

public class AdoSignalsTests
{
    [Theory]
    [InlineData("please fix this before merging")]
    [InlineData("please update the comment")]
    [InlineData("can you add a test for this?")]
    [InlineData("could you explain this logic?")]
    [InlineData("nitpick: rename this variable")]
    [InlineData("blocking: this causes a crash")]
    [InlineData("needs to be addressed")]
    [InlineData("should be refactored")]
    [InlineData("must be fixed before merge")]
    public void HasReviewSignal_AllKnownSignals_ReturnsTrue(string text)
    {
        Assert.True(AdoSignals.HasReviewSignal(text));
    }

    [Fact]
    public void HasReviewSignal_NoSignal_ReturnsFalse()
    {
        Assert.False(AdoSignals.HasReviewSignal("LGTM, nice work!"));
    }

    [Fact]
    public void HasReviewSignal_CaseInsensitive()
    {
        Assert.True(AdoSignals.HasReviewSignal("PLEASE FIX THIS NOW"));
    }

    [Fact]
    public void HasReviewSignal_MultipleSignals_ReturnsTrue()
    {
        Assert.True(AdoSignals.HasReviewSignal("action: needs to be refactored, todo: add unit test"));
    }
}
