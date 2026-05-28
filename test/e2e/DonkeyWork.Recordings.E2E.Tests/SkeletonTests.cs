namespace DonkeyWork.Recordings.E2E.Tests;

// Frontend e2e tier. Playwright .NET drives the real stack:
//   vite dev server + dotnet host + Testcontainers infra.
// Happy path for v1: login → create channel → New Recording → SignalR Pending →
// Generating → Ready → mp3 plays → copy feed URL → fetch RSS validates clean.
//
// To enable:
//   1. dotnet add package Microsoft.Playwright
//   2. dotnet build
//   3. pwsh bin/Debug/net10.0/playwright.ps1 install
//   4. Set E2E_BASE_URL (default http://localhost:5199) before running.
//
// Every test should carry [Trait("Category", "E2E")] so CI can opt in:
//   dotnet test --filter "Category=E2E"
public class SkeletonTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public void Placeholder_E2E()
    {
        Assert.True(true);
    }
}
