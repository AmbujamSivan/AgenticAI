using AeroMindIQ.Agents;
using AeroMindIQ.Tests.Fakes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AeroMindIQ.Tests;

public class GroundednessJudgeTests
{
    [Fact]
    public async Task GroundedResponse_ReturnsGroundedTrueAsync()
    {
        var fakeChat = new FakeChatCompletionService(new ChatMessageContent(AuthorRole.Assistant, "GROUNDED"));
        var judge = new GroundednessJudge(fakeChat, "test-model");

        var verdict = await judge.EvaluateAsync("report text", "findings text");

        Assert.True(verdict.Grounded);
        Assert.Empty(verdict.UnsupportedClaims);
    }

    [Fact]
    public async Task NotGroundedResponse_ReturnsUnsupportedClaimsAsync()
    {
        const string response = "NOT_GROUNDED\n- Claim about a 42% increase not in the data\n- A date that doesn't appear anywhere";
        var fakeChat = new FakeChatCompletionService(new ChatMessageContent(AuthorRole.Assistant, response));
        var judge = new GroundednessJudge(fakeChat, "test-model");

        var verdict = await judge.EvaluateAsync("report text", "findings text");

        Assert.False(verdict.Grounded);
        Assert.Equal(2, verdict.UnsupportedClaims.Count);
        Assert.Contains("42% increase", verdict.UnsupportedClaims[0]);
    }

    [Fact]
    public void Summarize_GroundedVerdict_MentionsCalibrationCaveat()
    {
        var verdict = new JudgeVerdict(true, [], null);

        var summary = verdict.Summarize();

        Assert.Contains("GROUNDED", summary);
        Assert.Contains("Unvalidated judge", summary);
    }
}
