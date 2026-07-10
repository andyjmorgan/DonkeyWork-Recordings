using System.Net;

namespace DonkeyWork.Recordings.Integration.Tests.Audio;

public class ControllerAuthGateTests : IClassFixture<RecordingsTestFixture>
{
    private readonly RecordingsTestFixture _fixture;

    public ControllerAuthGateTests(RecordingsTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/api/v1/recordings")]
    [InlineData("/api/v1/recordings/00000000-0000-0000-0000-000000000001/events")]
    [InlineData("/api/v1/collections")]
    [InlineData("/api/v1/voices")]
    [InlineData("/api/v1/collections/00000000-0000-0000-0000-000000000001/backlog")]
    [InlineData("/api/v1/backlog/00000000-0000-0000-0000-000000000001")]
    public async Task Authorize_Endpoints_Return_401_Without_Token(string path)
    {
        using var client = _fixture.Factory.CreateClient();
        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_Endpoint_Is_Open()
    {
        using var client = _fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_Endpoint_Requires_Auth()
    {
        using var client = _fixture.Factory.CreateClient();
        using var response = await client.PostAsync("/mcp", new StringContent(
            """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
            System.Text.Encoding.UTF8,
            "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OAuth_Protected_Resource_Metadata_Is_Public()
    {
        using var client = _fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/.well-known/oauth-protected-resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("authorization_servers", body);
    }
}
