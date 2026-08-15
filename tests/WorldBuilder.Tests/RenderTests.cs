using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// The render layer, tested without a model running. Everything except the model call itself is
/// deterministic, so CI must never depend on a 24 GB set of weights being resident.
/// </summary>
public class RenderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"wb-render-{Guid.CreateVersion7()}");

    private static WorldView World(ulong seed = 42, int years = 50)
    {
        Simulation sim = new(seed);
        sim.Run(years);
        return WorldView.Build(sim.Log, seed);
    }

    private (Chronicler Chronicler, ScriptedLlmClient Client) Build(Func<LlmRequest, string> reply)
    {
        ScriptedLlmClient client = new(reply);
        Chronicler chronicler = new(
            client,
            new RenderStore(Path.Combine(_dir, "renders.json")),
            new RenderJournal(Path.Combine(_dir, "renders.jsonl")));
        return (chronicler, client);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // ---- packs ------------------------------------------------------------

    [Fact]
    public void PacksCarryTheirEventsCastAndCausalEdges()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        Assert.NotEmpty(pack.Events);
        Assert.NotEmpty(pack.Cast);
        Assert.Contains("EVENTS", pack.Body, StringComparison.Ordinal);
        Assert.Contains("PEOPLE, PLACES AND POWERS", pack.Body, StringComparison.Ordinal);
        Assert.All(pack.Events, id => Assert.Equal(24, view.Log.Get(id).Year));
    }

    [Fact]
    public void PackKeysAreContentDerivedAndStable()
    {
        WorldView a = World();
        WorldView b = World();

        Assert.Equal(
            ContextPackBuilder.Year(a, 30).Key,
            ContextPackBuilder.Year(b, 30).Key);

        Assert.NotEqual(
            ContextPackBuilder.Year(a, 30).Key,
            ContextPackBuilder.Year(a, 31).Key);
    }

    [Fact]
    public void PackKeysSurviveEventsBeingRenumbered()
    {
        // The failure this guards against: pack keys were hashed from EventId, which is just
        // the row number in the log. Any engine change shifts every id, so the whole render
        // cache was stranded and twelve finished passages could no longer be found — while the
        // events they described were word-for-word identical.
        //
        // Simulated here by prepending a synthetic event, which renumbers everything after it.
        Simulation sim = new(42);
        sim.Run(50);

        EventLog shifted = new();
        shifted.Append(EventFactory.Create(
            id: new EventId(1),
            year: sim.Log.Events[0].Year,
            kind: EventKind.GenesisWorld,
            participants: [],
            significance: Significance.Bookkeeping));

        foreach (Event e in sim.Log.Events)
            shifted.Append(e with { Id = new EventId(shifted.Count + 1) });

        WorldView original = WorldView.Build(sim.Log, 42);
        WorldView renumbered = WorldView.Build(shifted, 42);

        Assert.Equal(
            ContextPackBuilder.Year(original, 30).Key,
            ContextPackBuilder.Year(renumbered, 30).Key);
    }

    [Fact]
    public void ChainPacksRunOldestFirst()
    {
        WorldView view = World();
        IReadOnlyList<IReadOnlyList<EventId>> chains = CausalTrace.DeepestChains(view.Log, 1);
        Assert.NotEmpty(chains);

        ContextPack pack = ContextPackBuilder.Chain(view, chains[0][^1]);

        int previous = int.MinValue;
        foreach (EventId id in pack.Events)
        {
            int year = view.Log.Get(id).Year;
            Assert.True(year >= previous);
            previous = year;
        }

        // Every event carries its own absolute year. Elapsed-time markers were tried and
        // removed: the model used them to write relative expressions and still got them wrong.
        Assert.DoesNotContain("years pass", pack.Body, StringComparison.Ordinal);
    }

    // ---- fabrication ------------------------------------------------------

    [Fact]
    public async Task ATruncatedRenderIsRejectedAndNeverCached()
    {
        // A section that ended mid-sentence on "In 38, Throll" was cached and became canon.
        // Running out of tokens is a failure, not a passage.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path.Combine(_dir, "renders.json"));
        Chronicler chronicler = new(
            new ScriptedLlmClient(_ => "In 38, Throll", "scripted", stopReason: "length"),
            store,
            new RenderJournal(Path.Combine(_dir, "renders.jsonl")));

        await Assert.ThrowsAsync<RenderTruncatedException>(() => chronicler.RenderAsync(pack));

        Assert.Equal(0, store.Count);
        Assert.False(File.Exists(Path.Combine(_dir, "renders.jsonl")));
    }

    [Fact]
    public void ThePackTellsTheModelWhatItsEventsAddUpTo()
    {
        // Permitting pattern statements without supplying the arithmetic would invite the model
        // to estimate counts across sixty records — a wrong count being a new fabrication, not
        // a stylistic lapse. The engine counts; the model interprets.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2));

        Assert.True(pack.Digest.Years > 0);
        Assert.Contains("WHAT THESE YEARS ADD UP TO", pack.Body, StringComparison.Ordinal);

        // Figures offered for characterisation must also be accepted by the fabrication check.
        string claim = $"Across {pack.Digest.Years} years there were {pack.Digest.Battles} battles.";
        Assert.True(FabricationCheck.Check(pack, claim).Clean);
    }

    [Fact]
    public void TheDigestSpanIsInclusiveAndMatchesTheRequestedWindow()
    {
        // The digest previously reported the span of the *events* it happened to contain, so a
        // section headed 22–41 opened by calling it seventeen years. Off-by-one here becomes
        // canon, so the convention is fixed and stated in the prompt.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        Assert.Equal(22, pack.Digest.FromYear);
        Assert.Equal(41, pack.Digest.ToYear);
        Assert.Equal(20, pack.Digest.Years);
    }

    [Fact]
    public void SeatHoldersAreFoundByWatchingTheSeatChangeHands()
    {
        // Counting POLITY.SUCCESSION events missed rulers who took the seat by open challenge,
        // by secession, or at a founding — three of a house's five. Replaying the world and
        // watching the leader change cannot miss a path, because it never enumerates them.
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);
            if (digest.Tenures.Count == 0) continue;

            int successions = 0;
            foreach (EventId id in view.Log.ForEntity(f.Id))
                if (view.Log.Get(id).Kind == EventKind.PolitySuccession) successions++;

            // Every tenure is a real person, ordered, inside the window.
            int previous = int.MinValue;
            foreach (Tenure t in digest.Tenures)
            {
                Assert.True(t.From >= view.FirstYear && t.To <= view.LastYear);
                Assert.True(t.From >= previous);
                previous = t.From;
                Assert.False(string.IsNullOrWhiteSpace(t.Holder));
            }

            Assert.True(digest.Tenures.Count >= 1);
            _ = successions;
        }
    }

    [Fact]
    public void AssassinationCountsDistinguishVictimFromPerpetrator()
    {
        // A ruler was reported as having survived seven attempts on his life when two of those
        // seven were murders he ordered. Both roles are in the schema; the count must use them.
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);

            int asTarget = 0, againstOutsiders = 0;
            foreach (EventId id in view.Log.ForEntity(f.Id))
            {
                Event e = view.Log.Get(id);
                if (e.Kind != EventKind.ConflictAssassination) continue;
                if (e.Object == f.Id) { asTarget++; continue; }

                if (e.Faction == f.Id && e.Outcome == Outcome.Succeeded
                    && !view.Members.WasIn(e.Object, f.Id, e.Id))
                {
                    againstOutsiders++;
                }
            }

            Assert.Equal(asTarget, digest.AttemptsOnSubject);
            Assert.Equal(againstOutsiders, digest.KillingsOfOutsiders);
            Assert.True(digest.AttemptsOnSubjectFatal <= digest.AttemptsOnSubject);
        }
    }

    /// <summary>
    /// The round-1 undercount: a house reported four of its own people murdered from within
    /// when the true figure was six. Both misses were killings with no assassination behind
    /// them — a challenger cut down by the man he failed to unseat, and a ruler killed by his
    /// own successor — so a count keyed on <c>CONFLICT.ASSASSINATION</c> could not see them,
    /// and named one of the missing men as a violently-ended ruler two sentences later.
    /// </summary>
    [Fact]
    public void InternalKillingsCountDeathsNotOnlyAssassinations()
    {
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);

            int fromWithin = 0;
            foreach (Event e in view.Log.Events)
            {
                if (e.Kind != EventKind.LifeDeathViolent) continue;
                if (view.Members.WasIn(e.Subject, f.Id, e.Id) && view.Members.WasIn(e.Object, f.Id, e.Id))
                    fromWithin++;
            }

            Assert.Equal(fromWithin, digest.KillingsOfItsOwn);
        }
    }

    /// <summary>
    /// Every violently-ended ruler the seat history names must be inside the internal-killing
    /// count for that house, when the hand that killed them was also its own. The stat and the
    /// narrative are drawn from the same events, and the round-1 report had them disagreeing
    /// inside a single paragraph.
    /// </summary>
    [Fact]
    public void KilledRulersAreCountedAmongTheHousesOwnDead()
    {
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);

            int killedRulersByOwnHand = 0;
            foreach (Event e in view.Log.Events)
            {
                if (e.Kind != EventKind.LifeDeathViolent) continue;
                if (!view.Members.WasIn(e.Subject, f.Id, e.Id)) continue;
                if (!view.Members.WasIn(e.Object, f.Id, e.Id)) continue;

                foreach (Tenure t in digest.Tenures)
                    if (t.Ended == "killed" && t.Holder == view.State.NameOf(e.Subject)) killedRulersByOwnHand++;
            }

            Assert.True(killedRulersByOwnHand <= digest.KillingsOfItsOwn,
                $"{f.Name}: {killedRulersByOwnHand} rulers killed from within, " +
                $"but the count of its own dead is {digest.KillingsOfItsOwn}");
        }
    }

    /// <summary>
    /// Places changing hands must be attributed to the right side and clamped at both ends of
    /// the window. Two failures shared a root: the count ignored which end of the event the
    /// subject was on. A conquest counted as a gain whoever won it, and a secession counted as
    /// a loss for the polity it created — which reported a faction that never lost a place as
    /// having lost one, on the strength of its own founding.
    /// </summary>
    [Fact]
    public void HoldingsAreAttributedToTheRightSideAndClampedBothWays()
    {
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            const int from = 10;
            const int to = 30;
            PackDigest digest = PackDigest.Of(view, f.Id, from, to);

            foreach (HoldingChange h in digest.PlacesTaken)
            {
                Assert.InRange(h.Year, from, to);
                Assert.NotEqual(f.Name, h.Other);
            }

            foreach (HoldingChange h in digest.PlacesLost)
            {
                Assert.InRange(h.Year, from, to);
                Assert.NotEqual(f.Name, h.Other);
            }

            // A faction never loses the place that its own founding event names.
            foreach (Event e in view.Log.Events)
            {
                if (e.Kind != EventKind.PolitySecession) continue;
                if (e.Faction == f.Id) continue;    // the parent really did lose it

                string born = view.State.NameOf(e.Where);
                foreach (HoldingChange h in digest.PlacesLost)
                    Assert.False(h.Place == born && h.Year == e.Year,
                        $"{f.Name} is reported as losing {born}, the place a secession gave it");
            }
        }
    }

    /// <summary>
    /// Departure categories must partition the rulers and say the right thing about each. A
    /// ruler who lost a challenge and was exiled a moment later was filed as "replaced",
    /// because the event that moved the seat is not the event that says what became of him.
    /// </summary>
    [Fact]
    public void DepartureCategoriesPartitionTheRulersAndMatchWhatHappened()
    {
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);
            if (digest.Tenures.Count == 0) continue;

            int summed = 0;
            foreach ((string _, int count) in digest.HowRulesEnded) summed += count;
            Assert.Equal(digest.Tenures.Count, summed);

            foreach (Tenure t in digest.Tenures)
            {
                if (t.Ended is not "cast out") continue;

                bool exiled = false;
                foreach (Event e in view.Log.Events)
                {
                    if (e.Kind != EventKind.PolityExile || e.GetInt("outlaw") == 1) continue;
                    if (view.State.NameOf(e.Subject) == t.Holder && e.Year == t.To) exiled = true;
                }
                Assert.True(exiled, $"{t.Holder} is filed as cast out in {t.To} with no expulsion there");
            }

            // Nobody filed as "replaced" was in fact expelled in the same year.
            foreach (Tenure t in digest.Tenures)
            {
                if (t.Ended is not "replaced") continue;

                foreach (Event e in view.Log.Events)
                {
                    if (e.Kind != EventKind.PolityExile || e.GetInt("outlaw") == 1) continue;
                    Assert.False(view.State.NameOf(e.Subject) == t.Holder && e.Year == t.To,
                        $"{t.Holder} was cast out in {t.To} but is filed as replaced");
                }
            }
        }
    }

    /// <summary>
    /// A house may only cast out its own. Where the sentence lands on someone who has already
    /// taken service elsewhere it is an outlawing, and must not strip him of the house he is
    /// actually in — the old behaviour left men stateless by a body with no hold over them, and
    /// then recorded them "returning from exile" they were never in.
    /// </summary>
    [Fact]
    public void OnlyMembersAreCastOutAndOutlawriesLeaveAllegianceAlone()
    {
        WorldView view = World();

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolityExile) continue;

            EntityId before = view.Members.Before(e.Subject, e.Id);
            EntityId after = view.Members.After(e.Subject, e.Id);

            if (e.GetInt("outlaw") == 1)
            {
                Assert.NotEqual(e.Faction, before);
                Assert.Equal(before, after);
            }
            else
            {
                Assert.Equal(e.Faction, before);
                Assert.True(after.IsNone);
            }
        }
    }

    [Fact]
    public void RaidsAreSplitByWhoLaunchedThem()
    {
        // One number counted raids launched and raids suffered together, then a faction section
        // presented the total as though the faction had ridden out on all of them.
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);

            int launched = 0, beatenOff = 0, suffered = 0;
            foreach (EventId id in view.Log.ForEntity(f.Id))
            {
                Event e = view.Log.Get(id);
                if (e.Kind != EventKind.ConflictRaid) continue;

                if (e.Faction == f.Id)
                {
                    launched++;
                    if (e.Outcome == Outcome.Failed) beatenOff++;
                }
                // Role, not proximity. The old fallback counted any raid with a place attached
                // as one suffered, which is every raid in the log.
                else if (e.Object == f.Id) suffered++;
            }

            Assert.Equal(launched, digest.RaidsLaunched);
            Assert.Equal(beatenOff, digest.RaidsLaunchedBeatenOff);
            Assert.Equal(suffered, digest.RaidsSuffered);
        }
    }

    [Fact]
    public void ARulerWhoTookTheSeatEarlierKeepsTheirRealStartDate()
    {
        // "Paernmel had held the seat since 51" — he took it in 39. Clamping the start to the
        // window rewrote when he came to power.
        WorldView view = World();
        PackDigest digest = PackDigest.Of(view, EntityId.Faction(2), 42, 51);

        foreach (Tenure t in digest.Tenures)
        {
            if (!t.BeganEarlier) continue;
            Assert.True(t.From < 42, $"{t.Holder} is marked as having begun earlier but starts at {t.From}");
        }
    }

    [Fact]
    public void ShortPeriodsGetNoStatisticsAtAll()
    {
        // A one-year reign rendered as "One person held the seat and was killed; one person held
        // the seat and remained holding it." A distribution needs a population.
        WorldView view = World();
        PackDigest tiny = PackDigest.Of(view, EntityId.Faction(2), 50, 51);

        Assert.False(tiny.WorthSummarising);
        string block = tiny.ToPromptBlock();
        Assert.DoesNotContain("held the seat:", block, StringComparison.Ordinal);
        Assert.DoesNotContain("average", block, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCheckCatchesTheStonandKerFabrication()
    {
        // The exact sentence that survived two fix rounds, verbatim in both its forms. Stonand
        // Ker killed the ruler but never held the seat; Le Vild took it by setting aside Kou
        // Peis. Every name is real, which is why proper-noun checking could not see it.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        foreach (string claim in new[]
        {
            "Ska was murdered by Stonand Ker, who was in turn set aside by Le Vild.",
            "Ska was killed by Stonand Ker, who was succeeded by Le Vild.",
        })
        {
            FabricationReport report = FabricationCheck.Check(pack, claim);
            Assert.True(
                report.Findings.Any(f => f.Kind is "never-held-the-seat" or "false-succession"
                                         or "unshared-pair"),
                $"not caught: {claim}");
        }
    }

    /// <summary>
    /// The round-6 inversion, verbatim. Meastouth challenged Dreld in 23, lost, and was killed
    /// for it; Dreld ruled two more years and died at Gatros Hearn's hands in 25. The passage
    /// reported the loser as having ended the winner's rule, and then contradicted itself one
    /// sentence later. Every name is real and every pair really met, so nothing before this
    /// check could see it.
    /// </summary>
    [Fact]
    public void TheCheckCatchesARuleEndedByTheWrongMan()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        Assert.True(pack.RuleEnders.TryGetValue("dreld", out string? ender));
        Assert.Equal("hearn", ender);

        FabricationReport report = FabricationCheck.Check(pack,
            "The rule of Weallhous Dreld ended when he was beaten in an open challenge by " +
            "Saern Meastouth, who was then killed by Dreld.");

        Assert.Contains(report.Findings, f => f.Kind == "wrong-ender");

        // And the true sentence must pass, or the check is a tax rather than a guard.
        Assert.DoesNotContain(
            FabricationCheck.Check(pack,
                "The rule of Weallhous Dreld ended in 25 when he was killed by Gatros Hearn.").Findings,
            f => f.Kind == "wrong-ender");
    }

    /// <summary>
    /// The round-6 direction error, verbatim. The Compact's own raid on Griwick failed; the
    /// prose has someone raiding the Compact and being repelled, which turns the defeat that
    /// cost it Hadale into a success and leaves the secession looking arbitrary.
    /// </summary>
    [Fact]
    public void TheCheckCatchesARaidPointedTheWrongWay()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        FabricationReport report = FabricationCheck.Check(pack,
            "Hadale broke away from the Compact to form the Hadale Commune after a raid on " +
            "the Compact was beaten off.");

        Assert.Contains(report.Findings, f => f.Kind is "wrong-direction" or "ambiguous-short-name");

        // A raid that really was aimed where the prose says must pass.
        Assert.DoesNotContain(
            FabricationCheck.Check(pack, "A raid on Griwick was beaten off.").Findings,
            f => f.Kind == "wrong-direction");
    }

    /// <summary>
    /// "Four people were murdered from within, including A, B, C and D" tells a reader there
    /// were more. There were not. A chronicle whose lists cannot be told from samples is not
    /// usable as a reference.
    /// </summary>
    [Fact]
    public void TheCheckCatchesIncludingBeforeAnExhaustiveList()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        List<string> names = [];
        foreach (Tenure t in pack.Digest.Tenures)
        {
            string surname = ContextPackBuilder.Surname(t.Holder);
            if (!names.Contains(surname)) names.Add(surname);
            if (names.Count == 3) break;
        }
        Assert.Equal(3, names.Count);

        FabricationReport report = FabricationCheck.Check(pack,
            $"Three people were killed, including {names[0]}, {names[1]} and {names[2]}.");

        Assert.Contains(report.Findings, f => f.Kind == "hedged-exhaustive-list");

        // Naming three of eleven is a sample, and "including" is then correct.
        Assert.DoesNotContain(
            FabricationCheck.Check(pack,
                $"Eleven people held the seat, including {names[0]}, {names[1]} and {names[2]}.").Findings,
            f => f.Kind == "hedged-exhaustive-list");
    }

    /// <summary>
    /// A reign is a spell in one seat, not a property of a person. Heth Fal held Kebarrow
    /// 33–35 and Laehiford 39 onward; keyed on the actor that came out as a single scope, which
    /// rendered the Kebarrow reign under the Laehiford title and described it with Laehiford's
    /// plague and raids.
    /// </summary>
    [Fact]
    public void AManWhoHeldTwoSeatsProducesTwoReigns()
    {
        WorldView view = World();

        EntityId hethFal = EntityId.None;
        foreach (Actor a in view.State.Actors)
            if (a.Name.EndsWith(" Fal", StringComparison.Ordinal)) hethFal = a.Id;
        Assert.False(hethFal.IsNone, "seed 42 should contain Heth Fal");

        List<ReignSpell> spells = ContextPackBuilder.Reigns(view, hethFal);
        Assert.Equal(2, spells.Count);
        Assert.NotEqual(spells[0].Faction, spells[1].Faction);

        // No event of one reign appears in the other, and every event of a reign names its seat.
        ContextPack first = ContextPackBuilder.Reign(view, spells[0]);
        ContextPack second = ContextPackBuilder.Reign(view, spells[1]);

        foreach (EventId id in first.Events) Assert.DoesNotContain(id, second.Events);

        foreach ((ContextPack pack, ReignSpell spell) in new[] { (first, spells[0]), (second, spells[1]) })
        {
            foreach (EventId id in pack.Events)
            {
                Event e = view.Log.Get(id);
                Assert.InRange(e.Year, spell.From, spell.To);

                bool namesTheSeat = false;
                foreach (Participant p in e.Participants)
                    if (p.Id == spell.Faction) namesTheSeat = true;
                Assert.True(namesTheSeat, $"{view.Summarise(id)} does not name {spell.Faction}");
            }
        }
    }

    /// <summary>
    /// A breakaway installs its first ruler at the secession and emits no succession event, so
    /// the log has to name him there. Without it a man who held a seat for twelve of his
    /// faction's thirty-two years appeared only as a member being cast out at the end of them.
    /// </summary>
    [Fact]
    public void ASecessionNamesTheRulerItInstalls()
    {
        WorldView view = World();

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySecession || e.Subject.IsNone) continue;

            string line = view.Describe(e.Id);
            Assert.Contains(view.State.NameOf(e.Subject), line, StringComparison.Ordinal);
            Assert.Contains("seat", line, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The founding ruler of every breakaway faction is in its seat history, which is what the
    /// digest and the reign scopes are both built from.
    /// </summary>
    [Fact]
    public void EveryBreakawayHasItsFoundingRulerInTheSeatHistory()
    {
        WorldView view = World();
        Dictionary<EntityId, List<Tenure>> histories = PackDigest.AllSeatHistories(view);

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySecession || e.Subject.IsNone) continue;

            // The faction born here is the one whose seat is the place that broke away.
            EntityId born = EntityId.None;
            foreach (Faction f in view.State.Factions)
                if (f.Seat == e.Where && f.Name == e.GetString("name")) born = f.Id;
            if (born.IsNone) continue;

            List<Tenure> spells = histories.GetValueOrDefault(born, []);
            Assert.NotEmpty(spells);
            Assert.Equal(view.State.NameOf(e.Subject), spells[0].Holder);
            Assert.Equal(e.Year, spells[0].From);
        }
    }

    /// <summary>
    /// A battle must be findable from both sides. Recording the loser only in a payload field
    /// made every defeat invisible to lookups about the power that suffered it — no dossier, no
    /// pack, no statistic — so a faction's history held its wins and none of its losses.
    /// </summary>
    [Fact]
    public void ABattleIsIndexedAgainstTheSideThatLostIt()
    {
        WorldView view = World();
        int battles = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.ConflictBattle) continue;
            battles++;

            EntityId loser = e.GetEntity("loserFaction");
            Assert.False(loser.IsNone);
            Assert.Contains(e.Id, view.Log.ForEntity(loser));
            Assert.Contains(e.Id, view.Log.ForEntity(e.Faction));
        }

        Assert.True(battles > 0, "seed 42 should contain battles");
    }

    /// <summary>
    /// Wins and losses counted apart, and enumerated. One combined figure let a passage keep
    /// the victories and drop the defeats.
    /// </summary>
    [Fact]
    public void BattlesAreSplitByWhichSideWonThem()
    {
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            PackDigest digest = PackDigest.Of(view, f.Id, view.FirstYear, view.LastYear);

            int won = 0, lost = 0;
            foreach (Event e in view.Log.Events)
            {
                if (e.Kind != EventKind.ConflictBattle) continue;
                if (e.Faction == f.Id) won++;
                else if (e.GetEntity("loserFaction") == f.Id) lost++;
            }

            Assert.Equal(won, digest.BattlesWon);
            Assert.Equal(lost, digest.BattlesLost);
            Assert.Equal(won + lost, digest.BattleList.Count);

            foreach (BattleRecord b in digest.BattleList)
                Assert.NotEqual(f.Name, b.Other);
        }
    }

    /// <summary>
    /// Each stricken year with its own dead and displaced. Given only the totals, a passage
    /// wrote "killing hundreds and driving many away over the next two years" for three years
    /// of plague whose figures the engine had already computed.
    /// </summary>
    [Fact]
    public void EachStrickenYearCarriesItsOwnFigures()
    {
        WorldView view = World();
        PackDigest digest = PackDigest.Of(view, EntityId.Faction(3), 24, 36);

        Assert.NotEmpty(digest.Disasters);

        int summed = 0;
        HashSet<int> years = [];
        foreach (DisasterRecord d in digest.Disasters)
        {
            summed += d.Dead;
            years.Add(d.Year);
            Assert.InRange(d.Year, 24, 36);
        }

        Assert.Equal(digest.DisasterDeaths, summed);
        Assert.Equal(digest.StrickenYears, years.Count);

        // And they reach the prose block, where the model can copy rather than summarise.
        string block = digest.ToPromptBlock();
        foreach (DisasterRecord d in digest.Disasters)
            Assert.Contains($"{d.Kind} at {d.Place} in {d.Year}", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two outcomes of a disputed succession must be distinguishable without knowing which
    /// is commoner. "Upheld" happens twice in fifteen, so a renderer that guesses the majority
    /// case scores well and inverts the rare one — which is what happened to Kourn and Dourn.
    /// </summary>
    [Fact]
    public void BothOutcomesOfADisputedSuccessionNameBothParties()
    {
        WorldView view = World();
        int upheld = 0, setAside = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.PolitySuccession) continue;
            if (e.GetString("decision") != "claim") continue;

            string line = view.Describe(e.Id);
            string winner = view.State.NameOf(e.Subject);
            string loser = view.State.NameOf(e.Object);

            Assert.Contains(winner, line, StringComparison.Ordinal);
            Assert.Contains(loser, line, StringComparison.Ordinal);

            if (e.GetString("reason") == "the named heir's claim upheld") upheld++; else setAside++;
        }

        Assert.True(upheld > 0 && setAside > 0, "seed 42 should contain both outcomes");
    }

    /// <summary>A peace made because a power was destroyed must say which power.</summary>
    [Fact]
    public void APeaceAfterACollapseNamesThePowerThatCollapsed()
    {
        WorldView view = World();
        int checkedPeaces = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Kind != EventKind.DiploPeaceSigned) continue;
            if (e.GetString("reason") is not { } reason) continue;
            if (!reason.Contains("collapse", StringComparison.Ordinal)) continue;

            checkedPeaces++;

            // The named power is one of the two signatories, and it really did end.
            bool namesASignatory =
                reason.Contains(view.State.NameOf(e.Faction), StringComparison.Ordinal)
                || reason.Contains(view.State.NameOf(e.GetEntity("with")), StringComparison.Ordinal);

            Assert.True(namesASignatory, $"'{reason}' names neither side of the peace");
        }

        Assert.True(checkedPeaces > 0, "seed 42 should contain a peace after a collapse");
    }

    /// <summary>
    /// The round-8 priority case, verbatim. Turaer Danpa killed Heillvar Maer in 21; Bu Rumpirn
    /// killed Befu Seirn in 23, and Danpa took the seat that opened. The section's opening
    /// summary fused the killing with the succession, and its own body two paragraphs later got
    /// it right — so the passage contradicted itself.
    ///
    /// Nothing before this could see it: every name is real, and the two men share a causal
    /// edge, because the death Danpa did not commit is what opened the seat he took.
    /// </summary>
    [Fact]
    public void TheCheckCatchesAKillingAttributedToTheWrongMan()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(3), 4, 23);

        FabricationReport report = FabricationCheck.Check(pack,
            "The period ended with Turaer Danpa holding the seat after killing Befu Seirn.");

        Assert.Contains(report.Findings, f => f.Kind == "wrong-killer");

        // Both true statements — the summary's and the body's — must pass.
        foreach (string claim in new[]
        {
            "Turaer Danpa had Heillvar Maer murdered in year 21.",
            "Bu Rumpirn had Befu Seirn murdered in year 23, and Turaer Danpa took the seat.",
            "Befu Seirn was killed by Bu Rumpirn.",
        })
        {
            Assert.DoesNotContain(
                FabricationCheck.Check(pack, claim).Findings, f => f.Kind == "wrong-killer");
        }
    }

    /// <summary>
    /// The same check does the work of comparing a summary against its body: two different
    /// killers for one victim cannot both match the record, so the false one is reported.
    /// </summary>
    [Fact]
    public void ASummaryThatContradictsItsBodyIsCaught()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(3), 4, 23);

        FabricationReport report = FabricationCheck.Check(pack,
            "The period ended with Turaer Danpa holding the seat after killing Befu Seirn. " +
            "Bu Rumpirn had Befu Seirn murdered in year 23, and Turaer Danpa took the seat by " +
            "the strongest claim.");

        Assert.Contains(report.Findings, f => f.Kind == "wrong-killer" && f.Token.Contains("danpa", StringComparison.Ordinal));
        Assert.False(report.Truthful);
    }

    /// <summary>
    /// The round-8 collapse inversion, verbatim. The Wurn League was destroyed; the section
    /// reported the Kebarrow Compact — the power that destroyed it — as having collapsed.
    /// </summary>
    [Fact]
    public void TheCheckCatchesACollapseAttributedToTheWrongPower()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 2, 21);

        FabricationReport report = FabricationCheck.Check(pack,
            "The Compact took Hadale from the Wurn League in year 20, but peace was made with " +
            "the Wurn League in year 21 as the Kebarrow Compact collapsed.");

        Assert.Contains(report.Findings, f => f.Kind == "wrong-collapse");

        // The true version passes.
        Assert.DoesNotContain(
            FabricationCheck.Check(pack,
                "Taking Hadale in 20 left the Wurn League landless, and the Wurn League " +
                "collapsed in 20.").Findings,
            f => f.Kind is "wrong-collapse" or "wrong-year");
    }

    /// <summary>
    /// The round-8 vagueness case. 474 dead and 504 driven out, over three years, all supplied
    /// — and written as "hundreds" and "many". Reported, but not as a falsehood.
    /// </summary>
    [Fact]
    public void TheCheckCatchesFiguresThrownAwayForAnAdjective()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(3), 24, 36);

        FabricationReport report = FabricationCheck.Check(pack,
            "A plague broke out at Griwick in 26, killing hundreds and driving many away.");

        Assert.Contains(report.Findings, f => f.Kind == "vague-quantity");
        Assert.DoesNotContain(report.Blocking, f => f.Kind == "vague-quantity");

        // "over the next two years" is a span counted rather than read, and blocks.
        Assert.Contains(
            FabricationCheck.Check(pack, "The sickness ran on over the next two years.").Blocking,
            f => f.Kind == "relative-time");
    }

    /// <summary>
    /// The round-8 incomplete list: seven exile returns stated, six named. The one dropped had
    /// returned and been cast out in the same year, having lost the seat to the man the section
    /// is about — a story, not a stray fact.
    /// </summary>
    [Fact]
    public void TheCheckCatchesAListShorterThanItsOwnCount()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        List<string> names = [];
        foreach (Tenure t in pack.Digest.Tenures)
        {
            string surname = ContextPackBuilder.Surname(t.Holder);
            if (!names.Contains(surname)) names.Add(surname);
            if (names.Count == 3) break;
        }

        FabricationReport report = FabricationCheck.Check(pack,
            $"Five men were cast out: {names[0]}, {names[1]} and {names[2]}.");

        Assert.Contains(report.Findings, f => f.Kind == "incomplete-enumeration");

        // A list that matches its count passes.
        Assert.DoesNotContain(
            FabricationCheck.Check(pack,
                $"Three men were cast out: {names[0]}, {names[1]} and {names[2]}.").Findings,
            f => f.Kind == "incomplete-enumeration");
    }

    /// <summary>
    /// The round-8 date collapse in a new place: two men courted away a year apart, both dated
    /// to the later year. The year validation had only ever covered killings.
    /// </summary>
    [Fact]
    public void TheCheckCoversYearsOnCourtedDefections()
    {
        WorldView view = World();
        ReignSpell spell = ContextPackBuilder.Reigns(view, ActorNamed(view, " Fal"))[^1];
        ContextPack pack = ContextPackBuilder.Reign(view, spell);

        Assert.True(pack.Claims.Knows(ClaimIndex.WonAway, "mam"),
            "Baedros Mam should be courted away inside this reign");

        FabricationReport report = FabricationCheck.Check(pack,
            "Voudreirn Wer won Baedros Mam away from the ruler in 49.");

        Assert.Contains(report.Findings, f => f.Kind == "wrong-year");

        Assert.DoesNotContain(
            FabricationCheck.Check(pack, "Voudreirn Wer won Baedros Mam away from the ruler in 48.").Findings,
            f => f.Kind == "wrong-year");
    }

    /// <summary>
    /// True sentences the round-8 checks must not flag. Both read as defects on their first
    /// outing and both are correct: an elided list of killings, and a collapse sentence that
    /// names the power which did the destroying alongside the one destroyed.
    /// </summary>
    [Fact]
    public void TheRoundEightChecksLeaveTrueSentencesAlone()
    {
        WorldView view = World();

        // An elided list of agents: each victim takes the killer that follows their own name.
        ContextPack griwick = ContextPackBuilder.Faction(view, EntityId.Faction(3), 4, 23);
        Assert.DoesNotContain(
            FabricationCheck.Check(griwick,
                "Pouldrir Ho was killed by Math Ham, Heillvar Maer by Turaer Danpa, and Befu " +
                "Seirn by Bu Rumpirn.").Findings,
            f => f.Kind == "wrong-killer");

        // A collapse belongs to the power named nearest before it, not to every power present.
        ContextPack meigate = ContextPackBuilder.Faction(view, EntityId.Faction(4), 19, 51);

        foreach (string claim in new[]
        {
            "The Sworn Men of Meigate and the Vea Lode Covenant fought two battles between " +
            "them, and the Sworn Men of Meigate was finished in 50.",

            // An anaphoric subject names no power at all, so there is nothing to judge — and
            // certainly not the power that did the destroying, which is the nearest name.
            "The Sworn Men of Meigate and the Vea Lode Covenant fought two battles between " +
            "them, and the power ceased to exist after losing its land.",
        })
        {
            Assert.DoesNotContain(
                FabricationCheck.Check(meigate, claim).Findings,
                f => f.Kind is "wrong-collapse" or "wrong-year");
        }
    }

    private static EntityId ActorNamed(WorldView view, string suffix)
    {
        foreach (Actor a in view.State.Actors)
            if (a.Name.EndsWith(suffix, StringComparison.Ordinal)) return a.Id;
        return EntityId.None;
    }

    /// <summary>
    /// The round-7 elision failure, verbatim. One verb carried across four men, two of whom
    /// were killed rather than cast out — in a section whose own totals said so.
    /// </summary>
    [Fact]
    public void TheCheckCatchesOneVerbCarriedAcrossDifferentFates()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        FabricationReport report = FabricationCheck.Check(pack,
            "Le Vild was cast out in 33, Heth Fal in 35, Nael War in 37, and Paernrom Sir in 38, " +
            "before Kondruth Tru was cast out in 39.");

        Assert.Contains(report.Findings, f => f.Kind == "wrong-fate" && f.Token == "war");
        Assert.Contains(report.Findings, f => f.Kind == "wrong-fate" && f.Token == "sir");

        // The two who really were cast out must not be flagged.
        Assert.DoesNotContain(report.Findings, f => f.Kind == "wrong-fate" && f.Token == "vild");
        Assert.DoesNotContain(report.Findings, f => f.Kind == "wrong-fate" && f.Token == "tru");
    }

    /// <summary>
    /// True sentences the fate check must not flag. A checker whose findings now push passages
    /// out of canon cannot afford to be approximately right: each of these read as a defect on
    /// its first outing and each is correct.
    /// </summary>
    [Fact]
    public void TheFateCheckLeavesTrueSentencesAlone()
    {
        WorldView view = World();
        ContextPack kebarrow = ContextPackBuilder.Faction(view, EntityId.Faction(2), 42, 51);

        foreach ((ContextPack pack, string claim) in new (ContextPack, string)[]
        {
            // The name after "by" is the hand, not another victim.
            (ContextPackBuilder.Faction(view, EntityId.Faction(7), 29, 48),
                "Stald Gearngoll was killed in 45 by Kou Peis."),

            // The name between the subject and its verb is the target of the attempt.
            (kebarrow,
                "In 49, Drouldthas Stour attempted on Paernmel Has, was cast out, and his prior " +
                "conspiracy was uncovered."),

            // The name after a transitive verb is its object, not the subject of what follows.
            (ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41),
                "Gatros Hearn took the seat in 25 after killing Weallhous Dreld, but was cast out in 27."),

            // A following name with no year of its own is not an elided item in the list; the
            // sentence has moved on to what that person did.
            (ContextPackBuilder.Faction(view, EntityId.Faction(2), 2, 21),
                "Thra Bround was murdered in year 18, and Krir Nur similarly took the seat by " +
                "setting aside a claim."),

            // A relative pronoun binds the verb to the name before it, whatever governs that name.
            (ContextPackBuilder.Faction(view, EntityId.Faction(2), 2, 21),
                "Thra Bround then took the seat by setting aside the claim of Deargund Keirem, " +
                "who was cast out."),

            // An infinitive governs its object: the four members were cast out, not their target.
            (kebarrow,
                "Simultaneously, four members attempted to assassinate Paernmel Has and were cast out."),

            // "had X murdered" and "was murdered by Y" put victim and killer on opposite sides
            // of the verb; guessing which named the killer as the victim three times.
            (ContextPackBuilder.Faction(view, EntityId.Faction(3), 4, 23),
                "Turaer Danpa had Heillvar Maer murdered in year 21, and Befu Seirn took the seat."),

            (ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41),
                "Theald Va was murdered in 29 by Wilwound Ska, who then set aside a claim."),

            // A year past the clause boundary belongs to the next claim, not to this victim.
            (ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41),
                "He ordered the murder of Leimmil Theall in 25 but was himself cast out in 27."),
        })
        {
            FabricationReport report = FabricationCheck.Check(pack, claim);
            Assert.DoesNotContain(report.Findings, f => f.Kind is "wrong-fate" or "wrong-year");
        }
    }

    /// <summary>
    /// A year that belongs to the end of a rule must not be read as the year it began. "Took
    /// the seat by election and held it until year 15" is true and was reported as false.
    /// </summary>
    [Fact]
    public void TheSeatCheckReadsOnlyTheYearAttachedToTheTaking()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 2, 21);

        FabricationReport report = FabricationCheck.Check(pack,
            "Reweld Wul took the seat by election and held it until year 15, when he was killed.");

        Assert.DoesNotContain(report.Findings, f => f.Kind == "wrong-year");
    }

    /// <summary>
    /// The round-7 invented particular, verbatim. The count of three was right; the third item
    /// was assembled from a power, a town and a year that never met in a raid. Every word of it
    /// is in vocabulary, which is why nothing before this caught it.
    /// </summary>
    [Fact]
    public void TheCheckCatchesARaidAssembledFromRealNouns()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        FabricationReport report = FabricationCheck.Check(pack,
            "The Compact suffered three raids: one by the Sworn Men of Meigate on Hadale in 23, " +
            "one by the Sworn Men of Laehiford on Kebarrow in 23, and one by the Griwick Compact " +
            "on Kebarrow in 32.");

        Assert.Contains(report.Findings, f => f.Kind == "no-such-event");

        // The two real ones pass.
        Assert.DoesNotContain(
            FabricationCheck.Check(pack, "A raid on Hadale in 23 was beaten off.").Findings,
            f => f.Kind == "no-such-event");
    }

    /// <summary>
    /// The round-7 date collapse, verbatim: two killings a year apart sharing one date. Neither
    /// victim is the subject of the sentence, so the fate check cannot see it.
    /// </summary>
    [Fact]
    public void TheCheckCatchesTwoKillingsCollapsedOntoOneYear()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 42, 51);

        // Both forms of the collapse: two explicit years both wrong, and one leading year
        // silently governing a second victim it does not fit.
        foreach (string claim in new[]
        {
            "Paernmel Has ordered the murder of Veillpea Dourn at Vea Lode in 46 and the murder " +
            "of Thres Thrild at Griwick in 46.",

            "In 46, he ordered the murder of Veillpea Dourn at Vea Lode and Thres Thrild at Griwick.",
        })
        {
            FabricationReport report = FabricationCheck.Check(pack, claim);
            Assert.Contains(report.Findings,
                f => f.Kind == "wrong-year" && f.Token.Contains("thrild", StringComparison.Ordinal));
        }

        // Both stated correctly must pass, in either construction.
        foreach (string claim in new[]
        {
            "Paernmel Has ordered the murder of Veillpea Dourn at Vea Lode in 46 and the murder " +
            "of Thres Thrild at Griwick in 47.",

            "He ordered the murder of Veillpea Dourn at Vea Lode in 46 and Thres Thrild at Griwick in 47.",
        })
        {
            Assert.DoesNotContain(
                FabricationCheck.Check(pack, claim).Findings, f => f.Kind == "wrong-year");
        }
    }

    /// <summary>
    /// The round-7 date collapse at source: the digest now hands over each killing with its own
    /// year, so there is nothing to reconstruct.
    /// </summary>
    [Fact]
    public void TheDigestGivesEachOrderedKillingItsOwnYear()
    {
        WorldView view = World();
        PackDigest digest = PackDigest.Of(view, EntityId.Faction(2), 42, 51);

        Assert.Equal(digest.KillingsOfOutsiders, digest.Killings.Count);

        HashSet<int> years = [];
        foreach (KillingRecord k in digest.Killings) years.Add(k.Year);
        Assert.True(years.Count > 1, "seed 42's 42–51 killings are in different years");

        foreach (KillingRecord k in digest.Killings)
            Assert.Contains($"{k.Victim} at {k.Place} in {k.Year}", digest.ToPromptBlock(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A passage that fails the check is written again, with the findings handed back. The
    /// check used to report and cache anyway, which made it an observation rather than a guard.
    /// </summary>
    [Fact]
    public async Task AFailedCheckIsRetriedWithTheFindingsHandedBack()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        int calls = 0;
        (Chronicler chronicler, ScriptedLlmClient client) = Build(_ =>
            ++calls == 1
                ? "Cardinal Ravensburg took the citadel."   // an invented name
                : "The year passed.");

        RenderOutcome outcome = await chronicler.RenderAsync(pack);

        Assert.Equal(2, client.Calls);
        Assert.True(outcome.Fabrication.Clean);
        Assert.Equal(RenderStatus.Generated, outcome.Render.Status);
        Assert.Equal("The year passed.", outcome.Render.Text);
    }

    /// <summary>
    /// Where the second attempt fails too, the passage is kept — the run has to stay
    /// reproducible and the failure inspectable — but it is marked Suspect and is not canon.
    /// </summary>
    [Fact]
    public async Task APassageThatFailsTwiceIsStoredAsSuspect()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        (Chronicler chronicler, ScriptedLlmClient client) = Build(
            _ => "Cardinal Ravensburg took the citadel.");

        RenderOutcome outcome = await chronicler.RenderAsync(pack);

        Assert.Equal(2, client.Calls);
        Assert.False(outcome.Fabrication.Clean);
        Assert.Equal(RenderStatus.Suspect, outcome.Render.Status);
    }

    /// <summary>
    /// Stub prose that satisfies the whole-section rules: the seat-holders named, the places
    /// that changed hands named, and the end of a power said out loud if one ended.
    ///
    /// Tests about cache identity and finding tiers should not also be tests of how lively
    /// their stub is. The Chronicler now retries a shapeless section, so a stub that says
    /// "telling number 1" costs two calls and makes a call count mean something else.
    /// </summary>
    private static string Roster(ContextPack pack)
    {
        List<string> names = [];
        foreach (Tenure t in pack.Digest.Tenures)
        {
            string surname = ContextPackBuilder.Surname(t.Holder);
            if (!names.Contains(surname)) names.Add(surname);
        }

        List<string> places = [];
        foreach (HoldingChange h in pack.Digest.PlacesTaken)
            if (!places.Contains(h.Place)) places.Add(h.Place);
        foreach (HoldingChange h in pack.Digest.PlacesLost)
            if (!places.Contains(h.Place)) places.Add(h.Place);

        string text = names.Count == 0 ? "" : string.Join(", ", names) + " each held the seat. ";
        if (places.Count > 0) text += string.Join(", ", places) + " changed hands. ";

        return text + "A power collapsed.";
    }

    /// <summary>
    /// A readability finding is reported but neither retried nor held out of canon. Canon has to
    /// be true; requiring it to be flawless suppressed six true sections over the word "the
    /// Compact", which trades a readability problem for a missing history.
    /// </summary>
    [Fact]
    public async Task AReadabilityFindingDoesNotCostARetryOrCanon()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);
        Assert.NotEmpty(pack.AmbiguousShortNames);

        string prose = $"The Compact fought three battles. {Roster(pack)}";
        FabricationReport check = FabricationCheck.Check(pack, prose, wholeSection: true);

        Assert.False(check.Clean);
        Assert.True(check.Truthful);
        Assert.Contains(check.Findings, f => f.Kind == "ambiguous-short-name" && !f.BlocksCanon);

        // The point of the finding's tier: not false, and not worth an inference call either.
        Assert.DoesNotContain(check.Retryable, f => f.Kind == "ambiguous-short-name");

        (Chronicler chronicler, ScriptedLlmClient client) = Build(_ => prose);
        RenderOutcome outcome = await chronicler.RenderAsync(pack);

        Assert.Equal(1, client.Calls);
        Assert.Equal(RenderStatus.Generated, outcome.Render.Status);
    }

    /// <summary>
    /// A cached passage keeps its text but not its verdict. A section held out of the document
    /// by an earlier, coarser checker would otherwise stay held out forever, even once the
    /// finding against it had been shown to be spurious.
    /// </summary>
    [Fact]
    public async Task ACachedPassageIsJudgedAgainByTheCurrentChecker()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path.Combine(_dir, "renders.json"));
        Chronicler chronicler = new(
            new ScriptedLlmClient(_ => "unused"),
            store,
            new RenderJournal(Path.Combine(_dir, "renders.jsonl")));

        // A passage that is in fact clean, stored under a stale Suspect verdict.
        store.Put(new Render
        {
            PackKey = pack.Key,
            PromptVersion = Prompts.VersionFor(pack.Kind),
            Model = "scripted",
            Text = "The year passed.",
            Year = pack.ToYear,
            Status = RenderStatus.Suspect,
        });

        RenderOutcome outcome = await chronicler.RenderAsync(pack);

        Assert.True(outcome.FromCache);
        Assert.Equal("The year passed.", outcome.Render.Text);
        Assert.Equal(RenderStatus.Generated, outcome.Render.Status);
    }

    /// <summary>
    /// A human verdict is not a machine verdict and must survive re-reading. Rejected means a
    /// person rejected it, and no amount of re-checking makes that Generated again.
    /// </summary>
    [Fact]
    public async Task AHumanVerdictOnACachedPassageIsNotOverwritten()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path.Combine(_dir, "renders.json"));
        Chronicler chronicler = new(
            new ScriptedLlmClient(_ => "unused"),
            store,
            new RenderJournal(Path.Combine(_dir, "renders.jsonl")));

        store.Put(new Render
        {
            PackKey = pack.Key,
            PromptVersion = Prompts.VersionFor(pack.Kind),
            Model = "scripted",
            Text = "The year passed.",
            Year = pack.ToYear,
            Status = RenderStatus.Rejected,
        });

        RenderOutcome outcome = await chronicler.RenderAsync(pack);
        Assert.Equal(RenderStatus.Rejected, outcome.Render.Status);
    }

    /// <summary>
    /// Changing one scope's instruction must not discard the others' passages. A single global
    /// version made every such change cost an hour of inference to re-earn work that was
    /// already correct, which is a strong incentive not to fix the scope that is wrong.
    /// </summary>
    [Fact]
    public void PromptVersionsAreScopedToThePackKindTheyGovern()
    {
        Assert.NotEqual(
            Prompts.VersionFor(PackKind.Reign),
            Prompts.VersionFor(PackKind.FactionArc));

        // The shared rules version is still a prefix of every one of them, so a change to the
        // rules themselves invalidates the whole book, as it must.
        foreach (PackKind kind in Enum.GetValues<PackKind>())
            Assert.StartsWith(Prompts.Version, Prompts.VersionFor(kind), StringComparison.Ordinal);
    }

    /// <summary>A clean passage costs one call, as it always did.</summary>
    [Fact]
    public async Task ACleanPassageIsNotRetried()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        (Chronicler chronicler, ScriptedLlmClient client) = Build(_ => "The year passed.");
        RenderOutcome outcome = await chronicler.RenderAsync(pack);

        Assert.Equal(1, client.Calls);
        Assert.Equal(RenderStatus.Generated, outcome.Render.Status);
    }

    [Fact]
    public void TheCheckAcceptsARealSuccession()
    {
        // The counterpart: a true succession must not be flagged, or the check is useless.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);
        Assert.True(pack.Tenures().Count >= 2, "need at least two consecutive holders");

        string predecessor = pack.Digest.Tenures[0].Holder;
        string successor = pack.Digest.Tenures[1].Holder;

        FabricationReport report = FabricationCheck.Check(
            pack, $"{predecessor} was succeeded by {successor}.");

        Assert.DoesNotContain(report.Findings, f => f.Kind is "never-held-the-seat" or "false-succession");
    }

    [Fact]
    public void TheCheckCatchesARelationshipBetweenTwoPeopleWhoNeverMet()
    {
        // "Ska was murdered by Ker, who was in turn set aside by Le Vild" — Ker never held the
        // seat, and Le Vild set aside someone else. Both names real; the link invented.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);
        Assert.NotEmpty(pack.ActorPairs);

        // Two real people from the pack who share no event.
        HashSet<string> names = [];
        foreach (string pair in pack.ActorPairs)
            foreach (string n in pair.Split('|')) names.Add(n);

        string? left = null, right = null;
        foreach (string a in names)
        {
            foreach (string b in names)
            {
                if (a == b || pack.ActorPairs.Contains(ContextPackBuilder.Pair(a, b))) continue;
                left = a; right = b;
                break;
            }
            if (left is not null) break;
        }

        Assert.NotNull(left);
        FabricationReport report = FabricationCheck.Check(pack, $"{left} was set aside by {right}.");
        Assert.Contains(report.Findings, f => f.Kind == "unshared-pair");
    }

    [Fact]
    public void DepartureCategoriesAccountForEveryRuler()
    {
        // "Four killed, three replaced, three cast out" summed to ten against eleven rulers —
        // the one still holding the seat at the end fell into no category and vanished.
        WorldView view = World();

        foreach ((int from, int to) in new[] { (view.FirstYear, view.LastYear), (2, 21), (22, 41), (42, 51) })
        {
            PackDigest digest = PackDigest.Of(view, EntityId.Faction(2), from, to);

            int counted = 0;
            foreach ((string _, int n) in digest.HowRulesEnded) counted += n;

            Assert.Equal(digest.Tenures.Count, counted);
        }
    }

    [Fact]
    public void MeanTenureCannotExceedThePeriodItIsMeasuredIn()
    {
        // Measuring tenures from their true start — which may predate the window — made eleven
        // rulers across twenty years average 1.9 rather than 1.8.
        WorldView view = World();
        PackDigest digest = PackDigest.Of(view, EntityId.Faction(2), 22, 41);

        Assert.True(digest.Tenures.Count > 0);
        int meanTenths = digest.MeanTenureTenths;

        Assert.InRange(meanTenths, 0, digest.Years * 10);
        Assert.InRange(meanTenths * digest.Tenures.Count, 0, digest.Years * 10 + 10);
    }

    [Fact]
    public void TheDigestNeverStatesAnImpossibleAverage()
    {
        // Both of these reached the prose and were stated as fact. Wrong engine figures are
        // worse than model guesses: they carry full confidence and are cached as canon.
        WorldView view = World();

        foreach (Faction f in view.State.Factions)
        {
            foreach ((int from, int to) in new[] { (view.FirstYear, view.LastYear), (22, 41), (42, 51) })
            {
                PackDigest digest = PackDigest.Of(view, f.Id, from, to);
                string block = digest.ToPromptBlock();

                // "average 0.0 years each" — meaningless, and it was printed.
                Assert.DoesNotContain("average 0.0", block, StringComparison.Ordinal);

                // For two values the median IS the mean; reporting "average 4.5, median 9"
                // is arithmetically impossible.
                if (digest.Tenures.Count == 2)
                    Assert.Equal(digest.MeanTenureTenths, digest.MedianTenureTenths);

                // A median must always sit inside the range of the values it summarises.
                if (digest.Tenures.Count > 0)
                {
                    // Measured inside the window, as the digest measures them: a spell that
                    // began before the period contributes only the part of it the period saw,
                    // so the averages partition the window rather than overflowing it.
                    int shortest = int.MaxValue, longest = 0;
                    foreach (Tenure t in digest.Tenures)
                    {
                        int inside = Math.Max(0, t.To - Math.Max(t.From, from));
                        shortest = Math.Min(shortest, inside);
                        longest = Math.Max(longest, inside);
                    }
                    Assert.InRange(digest.MedianTenureTenths, shortest * 10, longest * 10);
                    Assert.InRange(digest.MeanTenureTenths, shortest * 10, longest * 10);
                }
            }
        }
    }

    [Fact]
    public void ATenureClippedByTheWindowDoesNotReportHowItEndedOutsideIt()
    {
        // Within 22–41 a man killed in 51 was simply still holding. Reporting "killed" there
        // dates his death twenty years early.
        WorldView view = World();
        PackDigest digest = PackDigest.Of(view, EntityId.Faction(2), 22, 41);

        foreach (Tenure t in digest.Tenures)
        {
            // From deliberately keeps its real value, which may predate the window — clamping
            // it rewrote when a ruler came to power. Only the end is clipped.
            Assert.InRange(t.To, 22, 41);
            if (t.From < 22) Assert.True(t.BeganEarlier);
            if (t.To == 41) Assert.Contains("still holding", t.Ended, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheDigestNeverTalksAboutTheArchiveItself()
    {
        // "64 recorded events" is a fact about a log file, and the model wrote it straight into
        // the prose. Statistics must describe the world.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2));

        foreach (string banned in new[] { "recorded events", "records show", "log", "entries", "data" })
            Assert.DoesNotContain(banned, pack.Digest.ToPromptBlock(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecretEventsNeverReachARenderPack()
    {
        // Conspiracies and unattributed killings are flagged hidden in the log, and the renderer
        // was narrating them as public history — permanently, since accepted renders are canon.
        // v3's epistemic layer would then have had to unpick leaks baked into the world's text.
        WorldView view = World();

        int secretsInLog = 0;
        foreach (Event e in view.Log.Events)
            if (e.Scope == Visibility.Secret) secretsInLog++;
        Assert.True(secretsInLog > 0, "seed has no secret events, so this proves nothing");

        List<ContextPack> packs = [ContextPackBuilder.Faction(view, EntityId.Faction(1))];
        for (int year = view.FirstYear; year <= view.LastYear; year++)
            packs.Add(ContextPackBuilder.Year(view, year));

        foreach (ContextPack pack in packs)
            foreach (EventId id in pack.Events)
                Assert.NotEqual(Visibility.Secret, view.Log.Get(id).Scope);
    }

    [Fact]
    public void BookkeepingNeverReachesARenderPackButItsCausesSurvive()
    {
        // The yearly accounts are state, not history. Left in, they were narrated faithfully —
        // "a harvest count at Meigate revealed a grain shortage" — which reads as invention but
        // was the renderer doing exactly as told. Their own causes are taken in their place, so
        // the chain of explanation is not broken by removing them.
        WorldView view = World();

        for (int year = view.FirstYear; year <= view.LastYear; year++)
        {
            ContextPack pack = ContextPackBuilder.Year(view, year);
            foreach (EventId id in pack.Events)
                Assert.True(view.Log.Get(id).Significance >= Significance.Minor);
        }
    }

    [Fact]
    public void PacksNeverCiteAnEventTheyDoNotContain()
    {
        // A reference the reader cannot resolve is a hole, whether or not the underlying graph
        // is sound. The pack body is the renderer's whole universe, so a cause named in it must
        // also be present in it.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(1));

        HashSet<string> present = [];
        foreach (EventId id in pack.Events) present.Add(id.ToString());

        foreach (string line in pack.Body.Split('\n'))
        {
            int at = line.IndexOf("(because ", StringComparison.Ordinal);
            if (at < 0) continue;

            string refs = line[(at + 9)..].TrimEnd(')');
            foreach (string cited in refs.Split(',', StringSplitOptions.TrimEntries))
                Assert.Contains(cited, present);
        }
    }

    [Fact]
    public void TheFabricationCheckCatchesAnElectionRenderedAsACoup()
    {
        // The most serious error in the first chronicle: a succession by election described as
        // "his violent seizure of power". Same nouns, inverted meaning.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        FabricationReport report = FabricationCheck.Check(
            pack, "He took the seat by coup, a violent seizure of power.");

        Assert.Contains(report.Findings, f => f.Kind == "unsupported-manner");
    }

    [Fact]
    public void TheFabricationCheckCatchesProseAboutTheArchive()
    {
        // The war sections regressed into telemetry — "the conflict involved six recorded
        // events" — which is a fact about a log file rather than about the world.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        FabricationReport report = FabricationCheck.Check(
            pack, "The war involved six recorded events and the records show little else.");

        Assert.Contains(report.Findings, f => f.Kind == "describes-the-archive");
    }

    [Fact]
    public void TheFabricationCheckCatchesInventedMotivation()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        FabricationReport report = FabricationCheck.Check(
            pack, "His paranoia grew amid years of simmering resentment.");

        Assert.Contains(report.Findings, f => f.Kind == "invented-mind");
    }

    [Fact]
    public void FabricationCheckPassesAPassageDrawnOnlyFromTheRecords()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        string name = view.State.NameOf(pack.Cast[0]);
        string passage = $"In {pack.FromYear} little of note occurred. {name} endured the year.";

        Assert.True(FabricationCheck.Check(pack, passage).Clean);
    }

    [Fact]
    public void FabricationCheckCatchesAnInventedName()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        FabricationReport report = FabricationCheck.Check(pack, "Cardinal Ravensburg seized the citadel.");

        Assert.False(report.Clean);
        Assert.Contains(report.Findings, f => f.Token.Contains("Ravensburg", StringComparison.Ordinal));
    }

    [Fact]
    public void FabricationCheckCatchesAnInventedDateWrittenInWords()
    {
        // The gap that let the first real render through: the model wrote "in year twelve",
        // and a checker looking only at digits saw nothing at all.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        FabricationReport report = FabricationCheck.Check(pack, "He had taken the seat in year ninety-nine.");

        Assert.False(report.Clean);
        Assert.Contains(report.Findings, f => f.Kind == "number-in-words");
    }

    [Fact]
    public void PossessivesAreNotMistakenForInventedNames()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        string name = view.State.NameOf(pack.Cast[0]).Split(' ')[0];
        Assert.True(FabricationCheck.Check(pack, $"{name}’s claim was refused.").Clean);
        Assert.True(FabricationCheck.Check(pack, $"{name}'s claim was refused.").Clean);
    }

    // ---- cache and journal ------------------------------------------------

    [Fact]
    public async Task ASecondRenderComesFromTheCacheAndDoesNotCallTheModel()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);
        (Chronicler chronicler, ScriptedLlmClient client) = Build(_ => "A quiet year.");

        RenderOutcome first = await chronicler.RenderAsync(pack);
        RenderOutcome second = await chronicler.RenderAsync(pack);

        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.Equal(1, client.Calls);
        Assert.Equal(first.Render.Text, second.Render.Text);
    }

    [Fact]
    public async Task OneInputProducesOneCacheEntryEvenWhenForced()
    {
        // The requirement, stated plainly: the same events under the same scope must never
        // produce two entries. A forced re-render replaces; it does not accumulate.
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        RenderStore store = new(Path.Combine(_dir, "renders.json"));
        RenderJournal journal = new(Path.Combine(_dir, "renders.jsonl"));

        // Shaped like a real section, because the Chronicler now retries a shapeless one and a
        // retry would make the call count say something other than what this test is about.
        string cast = Roster(pack);

        int call = 0;
        Chronicler chronicler = new(
            new ScriptedLlmClient(_ => $"Telling number {++call}. {cast}"), store, journal);

        await chronicler.RenderAsync(pack);
        await chronicler.RenderAsync(pack);                    // cache hit, no second call
        await chronicler.RenderAsync(pack, force: true);       // replaces, does not append

        Assert.Single(store.ForPack(pack.Key));
        Assert.Equal(2, call);

        // And the pack itself is a pure function of its inputs: rebuilt from scratch, same key.
        ContextPack again = ContextPackBuilder.Faction(World(), EntityId.Faction(2), 22, 41);
        Assert.Equal(pack.Key, again.Key);
    }

    [Fact]
    public async Task RenderingTheSameScopeTwiceGivesIdenticalText()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Faction(view, EntityId.Faction(2), 22, 41);

        RenderStore store = new(Path.Combine(_dir, "renders.json"));
        RenderJournal journal = new(Path.Combine(_dir, "renders.jsonl"));
        Chronicler chronicler = new(new ScriptedLlmClient(_ => "A settled telling."), store, journal);

        RenderOutcome first = await chronicler.RenderAsync(pack);
        RenderOutcome second = await chronicler.RenderAsync(pack, force: true);

        Assert.Equal(first.Render.Text, second.Render.Text);
    }

    [Fact]
    public async Task CachedProseSurvivesAPromptOrModelChangeInsteadOfBeingRewritten()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);

        RenderStore store = new(Path.Combine(_dir, "renders.json"));
        RenderJournal journal = new(Path.Combine(_dir, "renders.jsonl"));

        await new Chronicler(new ScriptedLlmClient(_ => "First telling.", "model-a"), store, journal)
            .RenderAsync(pack);
        await new Chronicler(new ScriptedLlmClient(_ => "Second telling.", "model-b"), store, journal)
            .RenderAsync(pack);

        // Both survive. A model swap must never silently rewrite history that already exists.
        Assert.Equal(2, store.ForPack(pack.Key).Count);
        Assert.True(store.TryGet(pack.Key, pack.InputHash, Prompts.VersionFor(pack.Kind), "model-a", out Render a));
        Assert.Equal("First telling.", a.Text);
    }

    [Fact]
    public async Task AnEditIsStoredBesideTheModelsOwnWords()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);
        (Chronicler chronicler, _) = Build(_ => "Raw output.");

        await chronicler.RenderAsync(pack);
        chronicler.Judge(pack, RenderStatus.Edited, "Polished output.");

        RenderStore reopened = new(Path.Combine(_dir, "renders.json"));
        Assert.True(reopened.TryGet(pack.Key, pack.InputHash, Prompts.VersionFor(pack.Kind), "scripted", out Render stored));

        Assert.Equal("Polished output.", stored.Text);
        Assert.Equal("Raw output.", stored.Original);
        Assert.Equal(RenderStatus.Edited, stored.Status);
    }

    [Fact]
    public async Task EveryRenderIsJournalledForTheTrainingCorpus()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);
        (Chronicler chronicler, _) = Build(_ => "A quiet year.");

        await chronicler.RenderAsync(pack);

        string[] lines = File.ReadAllLines(Path.Combine(_dir, "renders.jsonl"));
        Assert.Single(lines);
        Assert.Contains("\"prompt\":", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"output\":", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"model\":", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntityCodesAreStrippedFromProse()
    {
        WorldView view = World();
        ContextPack pack = ContextPackBuilder.Year(view, 24);
        (Chronicler chronicler, _) = Build(_ => "The Wurn League (f:1) fought Hadale (p:2) and won. See e:415.");

        RenderOutcome outcome = await chronicler.RenderAsync(pack);

        Assert.DoesNotContain("f:1", outcome.Render.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("p:2", outcome.Render.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("e:415", outcome.Render.Text, StringComparison.Ordinal);
    }

    // ---- the determinism guarantee ---------------------------------------

    [Fact]
    public async Task RenderingChangesNothingAboutTheWorld()
    {
        // The whole reason renders are cached rather than regenerated: the engine is
        // reproducible and the model is not. If prose could reach world state, replay would
        // stop reproducing the world and the seed would no longer describe it.
        Simulation sim = new(42);
        sim.Run(50);

        int eventsBefore = sim.Log.Count;
        int actorsBefore = sim.State.Actors.Count;
        string fingerprint = string.Join('|', sim.State.Factions.Select(f => $"{f.Id}{f.Leader}{f.Legitimacy}"));

        WorldView view = WorldView.Build(sim.Log, 42);
        (Chronicler chronicler, _) = Build(_ => "Something happened, allegedly.");

        await chronicler.RenderAsync(ContextPackBuilder.Year(view, 24));
        await chronicler.RenderAsync(ContextPackBuilder.Faction(view, EntityId.Faction(1)));

        Assert.Equal(eventsBefore, sim.Log.Count);
        Assert.Equal(actorsBefore, sim.State.Actors.Count);
        Assert.Equal(fingerprint, string.Join('|', sim.State.Factions.Select(f => $"{f.Id}{f.Leader}{f.Legitimacy}")));
    }
}
