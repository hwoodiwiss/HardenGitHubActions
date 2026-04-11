using HardenGitHubActions.Core;

namespace HardenGitHubActions.Tests;

public sealed class ActionReferenceTests
{
    // Test 1 — TryParse returns false for null/empty
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task TryParse_NullOrEmpty_ReturnsFalse(string uses)
    {
        var result = ActionReference.TryParse(uses, out var reference);

        await Assert.That(result).IsFalse();
        await Assert.That(reference).IsNull();
    }

    // Test 2 — Parses owner/repo@tag ref correctly
    [Test]
    public async Task TryParse_StandardTagRef_ParsesCorrectly()
    {
        var result = ActionReference.TryParse("actions/checkout@v4", out var reference);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(reference).IsNotNull();
            await Assert.That(reference!.Owner).IsEqualTo("actions");
            await Assert.That(reference.Repo).IsEqualTo("checkout");
            await Assert.That(reference.Ref).IsEqualTo("v4");
            await Assert.That(reference.IsAlreadyPinned).IsFalse();
            await Assert.That(reference.IsLocalPath).IsFalse();
            await Assert.That(reference.IsDocker).IsFalse();
        }
    }

    // Test 3 — 40-char hex ref sets IsAlreadyPinned
    [Test]
    public async Task TryParse_FortyCharHexRef_SetsIsAlreadyPinned()
    {
        const string sha = "a81bbbf8298c0fa03ea29cdc473d45769f953675";
        var result = ActionReference.TryParse($"actions/checkout@{sha}", out var reference);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(reference).IsNotNull();
            await Assert.That(reference!.Ref).IsEqualTo(sha);
            await Assert.That(reference.IsAlreadyPinned).IsTrue();
        }
    }

    // Test 4 — local path sets IsLocalPath
    [Test]
    [Arguments("./local/path")]
    [Arguments("./.github/actions/my-action")]
    public async Task TryParse_LocalPath_SetsIsLocalPath(string uses)
    {
        var result = ActionReference.TryParse(uses, out var reference);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(reference).IsNotNull();
            await Assert.That(reference!.IsLocalPath).IsTrue();
            await Assert.That(reference.IsDocker).IsFalse();
            await Assert.That(reference.IsAlreadyPinned).IsFalse();
        }
    }

    // Test 5 — docker:// sets IsDocker
    [Test]
    [Arguments("docker://ghcr.io/owner/image:tag")]
    [Arguments("docker://alpine:3.18")]
    public async Task TryParse_Docker_SetsIsDocker(string uses)
    {
        var result = ActionReference.TryParse(uses, out var reference);

        using (Assert.Multiple())
        {
            await Assert.That(result).IsTrue();
            await Assert.That(reference).IsNotNull();
            await Assert.That(reference!.IsDocker).IsTrue();
            await Assert.That(reference.IsLocalPath).IsFalse();
            await Assert.That(reference.IsAlreadyPinned).IsFalse();
        }
    }

    // Test 6 — malformed input returns false
    [Test]
    [Arguments("no-slash-or-at")]
    [Arguments("owner/repo")]
    [Arguments("@v4")]
    [Arguments("/repo@v4")]
    public async Task TryParse_Malformed_ReturnsFalse(string uses)
    {
        var result = ActionReference.TryParse(uses, out var reference);

        await Assert.That(result).IsFalse();
        await Assert.That(reference).IsNull();
    }
}
