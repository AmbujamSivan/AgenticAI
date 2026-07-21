using RcaEngine.Parsing;

namespace RcaEngine.Tests;

public class RedfishEventParserTests
{
    [Fact]
    public void Parse_ReadsLogEntryCollection()
    {
        const string json =
            """
            {
              "Name": "System Event Log",
              "Members": [
                {
                  "Id": "2",
                  "Created": "2026-07-18T03:26:47Z",
                  "Severity": "Critical",
                  "MessageId": "Memory.1.0.CorrectableECCErrorRateExceeded",
                  "Message": "Correctable ECC error rate threshold exceeded.",
                  "OriginOfCondition": "/redfish/v1/Systems/1/Memory/DIMM_B0"
                },
                {
                  "Id": "1",
                  "Created": "2026-07-18T02:11:42Z",
                  "Severity": "Warning",
                  "MessageId": "Memory.1.0.CorrectableECCError",
                  "Message": "Correctable ECC error detected.",
                  "OriginOfCondition": { "@odata.id": "/redfish/v1/Systems/1/Memory/DIMM_B0" }
                }
              ]
            }
            """;

        var events = RedfishEventParser.Parse(json);

        Assert.Equal(2, events.Count);
        // Sorted chronologically
        Assert.Equal("1", events[0].Id);
        Assert.Equal("Warning", events[0].Severity);
        // OriginOfCondition supports both string and object forms
        Assert.Equal("/redfish/v1/Systems/1/Memory/DIMM_B0", events[0].OriginOfCondition);
        Assert.Equal("Memory.1.0.CorrectableECCErrorRateExceeded", events[1].MessageId);
    }

    [Fact]
    public void Parse_EmptyMembers_ReturnsEmpty()
    {
        Assert.Empty(RedfishEventParser.Parse("""{ "Members": [] }"""));
        Assert.Empty(RedfishEventParser.Parse("{}"));
    }
}
