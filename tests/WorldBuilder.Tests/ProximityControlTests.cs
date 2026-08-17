using System.Security.Cryptography;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Geography;
using WorldBuilder.Core.Serialization;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The synthetic distance models, and the assertions that make their results mean anything.
///
/// A control is only worth running if the difference it produces is attributable to the values
/// it substituted rather than to the machinery that substituted them. Since a re-sequenced RNG
/// stream changes worlds on its own — the standing constraint on
/// <see cref="RngPurpose"/> — that is a real risk and not a theoretical one, so it is asserted
/// before any control figure is read.
/// </summary>
public class ProximityControlTests
{
    private static readonly ulong[] Panel = [7, 42, 99, 1234, 2025];

    private static string Hash(EventLog log)
    {
        StringBuilder sb = new();
        foreach (Event e in log.Events) sb.Append(JsonlIo.Serialise(e)).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string Run(ulong seed, ProximityControlKind control)
    {
        Simulation sim = new(seed, control: control);
        sim.Run(50);
        return Hash(sim.Log);
    }

    private static EventLog Log(ulong seed, ProximityControlKind control)
    {
        Simulation sim = new(seed, control: control);
        sim.Run(50);
        return sim.Log;
    }

    [Theory]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(99UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void TheIdentityControlProducesExactlyTheRealWorld(ulong seed)
    {
        // The assertion the whole method rests on. The identity control routes every proximity
        // through the same substitution machinery the other controls use and hands back what the
        // board said — so if the history is identical, the machinery consumes nothing from the
        // streams the rules are drawing on, and any difference the redraw or shuffle controls
        // produce is attributable to their values.
        //
        // Without it, a control result is confounded with re-sequencing, and the confounding is
        // invisible: the run is perfectly plausible either way.
        //
        // The genesis event is exempted and it is the *only* exemption, because a control run is
        // required to mark itself and that marking is a key on that event. Asserted precisely
        // rather than by skipping the row: every other event byte-identical, and the genesis
        // events differing in nothing but the marker.
        EventLog real = Log(seed, ProximityControlKind.None);
        EventLog identity = Log(seed, ProximityControlKind.Identity);

        Assert.Equal(real.Count, identity.Count);

        for (int i = 1; i < real.Count; i++)
            Assert.Equal(JsonlIo.Serialise(real.Events[i]), JsonlIo.Serialise(identity.Events[i]));

        Assert.Equal(
            JsonlIo.Serialise(real.Events[0]),
            JsonlIo.Serialise(identity.Events[0])
                .Replace(",\"control\":\"identity\"", "", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ProximityControlKind.Redraw)]
    [InlineData(ProximityControlKind.Shuffle)]
    public void AControlActuallyChangesTheWorldItIsControlling(ProximityControlKind kind)
    {
        // Otherwise the identity assertion above is satisfied by a control that never fires, and
        // a null result would be reported as "the mechanism does not matter" when it is really
        // "the control was never wired in".
        int changed = 0;
        foreach (ulong seed in Panel)
            if (Run(seed, ProximityControlKind.None) != Run(seed, kind)) changed++;

        Assert.Equal(Panel.Length, changed);
    }

    [Fact]
    public void ShuffleIsStableAndRedrawIsNot()
    {
        // The whole difference between the two controls, asserted directly rather than inferred
        // from their outputs. Shuffle must answer the same question the same way every time;
        // redraw must not, or it is a second shuffle under another name.
        ProximityControl shuffle = new(ProximityControlKind.Shuffle, 42, [80, 100, 120]);
        ProximityControl redraw = new(ProximityControlKind.Redraw, 42, [80, 100, 120]);

        int first = shuffle.Substitute(1, 10, 20, 100);
        for (int i = 0; i < 20; i++) Assert.Equal(first, shuffle.Substitute(1, 10, 20, 100));

        // And it is symmetric: a distance that depends on which way it is asked is the defect the
        // board itself is verified against, and a control must not reintroduce it.
        Assert.Equal(first, shuffle.Substitute(1, 20, 10, 100));

        HashSet<int> seen = [];
        for (int i = 0; i < 60; i++) seen.Add(redraw.Substitute(1, 10, 20, 100));
        Assert.True(seen.Count > 1, "redraw returned one value sixty times; it is not redrawing");
    }

    [Fact]
    public void BothControlsDrawOnlyFromTheWorldsOwnRealisedProximities()
    {
        // Same distribution, same clamp exposure — that is what makes the comparison fair. A
        // control drawing from a uniform range would change the exposure of every downstream
        // clamp as well as the values, and the result would confound the two.
        int[] empirical = [64, 71, 100, 118, 133];

        foreach (ProximityControlKind kind in new[] { ProximityControlKind.Redraw, ProximityControlKind.Shuffle })
        {
            ProximityControl control = new(kind, 7, empirical);

            for (int i = 0; i < 200; i++)
                Assert.Contains(control.Substitute(i % 4 + 1, i, i + 1, 100), empirical);
        }
    }

    [Fact]
    public void AControlWorldSaysSoInItsHeaderAndInItsRecord()
    {
        Simulation sim = new(42, control: ProximityControlKind.Redraw);
        sim.Run(3);

        string header = JsonlIo.Header(42, sim.Log.Count, ProximityControl.NameOf(sim.Control));
        Assert.Contains("\"control\":\"redraw\"", header, StringComparison.Ordinal);

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(header);
        Assert.True(WorldHeader.Parse(doc.RootElement).IsControl);

        // And in the log itself, so a record carried away from its file still says what it is.
        Assert.Equal("redraw", sim.Log.Events[0].GetString("control"));
    }

    [Fact]
    public void ARealWorldCarriesNoControlFieldAtAll()
    {
        // Omitted rather than written empty, so a world with no control is not confusable with
        // one whose marking was lost — the same rule the rest of the header follows.
        Simulation sim = new(42);
        sim.Run(3);

        string header = JsonlIo.Header(42, sim.Log.Count);

        Assert.DoesNotContain("control", header, StringComparison.Ordinal);
        Assert.Null(sim.Log.Events[0].GetString("control"));
    }
}
