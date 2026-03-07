using CommitApi.Extractors.Helpers;
using Xunit;

namespace CommitApi.Tests.Extractors;

public class VttParserTests
{
    [Fact]
    public void Parse_WebvttHeader_IsSkipped()
    {
        const string vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:05.000\nAlice: Hello world";
        var result = VttParser.Parse(vtt, "m1", "Test Meeting");
        Assert.Single(result);
        Assert.Equal("Alice", result[0].SpeakerName);
    }

    [Fact]
    public void Parse_NoteLines_AreSkipped()
    {
        const string vtt = "WEBVTT\nNOTE This is a note\n\n00:00:00.000 --> 00:00:05.000\nAlice: Hello";
        var result = VttParser.Parse(vtt, "m1", null);
        Assert.Single(result);
    }

    [Fact]
    public void Parse_SpeakerColonSplit_ExtractsSpeakerAndText()
    {
        const string vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:05.000\nBob: I will fix the bug by Friday";
        var result = VttParser.Parse(vtt, "m1", "Sprint Planning");
        Assert.Single(result);
        Assert.Equal("Bob", result[0].SpeakerName);
        Assert.Contains("I will fix", result[0].Text);
    }

    [Fact]
    public void Parse_UserIdEmbedded_ExtractedToSpeakerUserId()
    {
        const string vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:05.000\nAlice <user-123>: Hi there";
        var result = VttParser.Parse(vtt, "m1", null);
        Assert.Single(result);
        Assert.Equal("user-123", result[0].SpeakerUserId);
        Assert.Equal("Alice", result[0].SpeakerName);
    }

    [Fact]
    public void Parse_SpeakerChange_FlushesChunk()
    {
        const string vtt = """
            WEBVTT

            00:00:00.000 --> 00:00:02.000
            Alice: First line

            00:00:02.000 --> 00:00:04.000
            Bob: Second line
            """;
        var result = VttParser.Parse(vtt, "m1", null);
        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0].SpeakerName);
        Assert.Equal("Bob", result[1].SpeakerName);
    }

    [Fact]
    public void Parse_EmptyVtt_ReturnsEmptyList()
    {
        var result = VttParser.Parse("", "m1", null);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_OnlyHeader_ReturnsEmptyList()
    {
        var result = VttParser.Parse("WEBVTT\n", "m1", null);
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MeetingIdSetOnAllChunks()
    {
        const string vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:02.000\nAlice: text one\n\n00:00:02.000 --> 00:00:04.000\nBob: text two";
        var result = VttParser.Parse(vtt, "meeting-xyz", null);
        Assert.All(result, c => Assert.Equal("meeting-xyz", c.MeetingId));
    }

    [Fact]
    public void Parse_MeetingSubjectSetOnAllChunks()
    {
        const string vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:02.000\nAlice: text";
        var result = VttParser.Parse(vtt, "m1", "My Meeting");
        Assert.All(result, c => Assert.Equal("My Meeting", c.MeetingSubject));
    }

    [Fact]
    public void Parse_NoUserId_FallsBackToSpeakerName()
    {
        const string vtt = "WEBVTT\n\n00:00:00.000 --> 00:00:02.000\nCharlie: some text";
        var result = VttParser.Parse(vtt, "m1", null);
        Assert.Single(result);
        Assert.Equal("Charlie", result[0].SpeakerUserId);
    }

    [Fact]
    public void Parse_MalformedTimestampLine_SkippedGracefully()
    {
        const string vtt = "WEBVTT\n\nbadline --> oops\nAlice: text";
        var result = VttParser.Parse(vtt, "m1", null);
        // Should not throw — may or may not produce a chunk
        Assert.NotNull(result);
    }

    [Fact]
    public void ExtractUserId_AngleBracketFormat_ReturnsId()
    {
        Assert.Equal("usr-abc", VttParser.ExtractUserId("Alice <usr-abc>"));
    }

    [Fact]
    public void ExtractUserId_NoAngleBrackets_ReturnsNull()
    {
        Assert.Null(VttParser.ExtractUserId("Alice"));
    }

    [Fact]
    public void CleanSpeakerName_RemovesAngleBracketSuffix()
    {
        Assert.Equal("Alice", VttParser.CleanSpeakerName("Alice <usr-abc>"));
    }
}
