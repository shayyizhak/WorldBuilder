using System.Globalization;
using System.Text.RegularExpressions;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// What counts as a cause, and what a causal answer is allowed to be.
///
/// <b>Two defects, one subject.</b> A causal walk used to step into the world's own genesis and stop
/// there, so *why did the Griwick Compact declare war over Threi Cut* was answered with the record of
/// Threi Cut coming into existence — a stopping condition presented as a finding. And the staged
/// answer to every why-question was a bare record id, which nothing can be held against: any response
/// mentioning `e:506` satisfies it, including one that names the wrong person.
///
/// Both are about the same thing — an answer that cannot be wrong is not an answer — and both are
/// fixed in the reading rather than in the record. The edge from a war to the genesis of the place it
/// was fought over is true, and <c>PerceptionPhase</c> writes it deliberately: the honest reason a
/// faction wants a mine is that the mine is there. What is wrong is offering that as the cause of the
/// war, so nothing was removed from the log.
/// </summary>
public class CausalTraceTests
{
    private static readonly ulong[] Panel = ReferencePanel.Current;

    private static WorldView World(ulong seed)
    {
        Simulation sim = new(seed);
        sim.Run(50);
        return WorldView.Build(sim.Log, seed, sim.State.Board);
    }

    /// <summary>
    /// A causal trace never returns a genesis row, except where one is the event asked about.
    ///
    /// <b>The product path, not just the staged material.</b> <c>QueryEngine</c> retrieves through
    /// <see cref="ContextPackBuilder.Trace"/> for any question the planner classifies causal, so this
    /// defect reached the model rather than only the facts sheet — the pack handed it a genesis row as
    /// the last link and asked it to explain a war with it.
    ///
    /// The tip is exempt on purpose: *when did Threi Cut come into existence* is a fair question with
    /// a real answer, and it is only as a **cause** that a genesis row says nothing.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void ACausalTraceNeverWalksBackIntoTheWorldsGenesis(ulong seed)
    {
        WorldView view = World(seed);

        int traced = 0;
        int reachedGenesis = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Causes.Count == 0) continue;

            List<EventId> chain = ContextPackBuilder.Trace(view, e.Id);
            if (chain.Count == 0) continue;

            traced++;

            foreach (EventId id in chain)
            {
                if (id == e.Id) continue;

                Assert.False(ContextPackBuilder.IsGenesis(view.Log.Get(id)),
                    $"seed {seed.ToString(CultureInfo.InvariantCulture)}: the trace of {e.Id} " +
                    $"({EventKinds.Name(e.Kind)} Y{e.Year}) returned {id}, which is " +
                    $"{EventKinds.Name(view.Log.Get(id).Kind)} — a stopping condition, not a cause");
            }
        }

        // Non-vacuous twice over: traces were run, and some of them are over events that really do
        // cite a genesis row, so the guard had something to refuse rather than passing on worlds
        // where the case never arises.
        Assert.True(traced > 50,
            $"only {traced.ToString(CultureInfo.InvariantCulture)} event(s) traced; the walk is not " +
            "covering the record");

        foreach (Event e in view.Log.Events)
            foreach (EventId cause in e.Causes)
                if (view.Log.TryGet(cause, out Event at) && ContextPackBuilder.IsGenesis(at)) reachedGenesis++;

        Assert.True(reachedGenesis > 0,
            "no event on this seed cites a genesis row, so the guard was never exercised");
    }

    /// <summary>
    /// Every staged causal answer states its cause in words, and cites no genesis row.
    ///
    /// <b>Read off the emitted answer, not the helper that builds it.</b> The property belongs to
    /// what a session reads out of `questions.md`, and a test on the formatter would pass just as
    /// happily if the product threw the formatter's output away.
    ///
    /// A bare id is the thing being ruled out. "The recorded causes, walked back: e:506" names a
    /// record and asserts nothing about it, so a layer citing that id while describing the wrong
    /// event scores as correct — which makes the question unable to fail, the same defect class as a
    /// boundary year in a seat question.
    /// </summary>
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(42UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void NoStagedCausalAnswerIsABareRecordId(ulong seed)
    {
        WorldView view = World(seed);
        QueryEngine engine = new(new CacheOnlyLlmClient("none"), view);

        RelationTrajectory.Report ties = RelationTrajectory.Of(view.Log, view.Seed, view.State.Board);
        List<ReferenceStaging.Candidate> made =
            ReferenceStaging.Questions(engine, view, new SeedHoldouts(seed, [], []), ties);

        int asked = 0;

        foreach (ReferenceStaging.Candidate c in made)
        {
            if (!c.Text.StartsWith("Why did", StringComparison.Ordinal)) continue;
            if (c.Category == ReferenceStaging.NegativePremise) continue;

            asked++;

            // Either it says the record gives none, or it says because-something. "The recorded
            // causes, walked back" is neither.
            bool answered = c.Answer.StartsWith("because ", StringComparison.Ordinal);
            bool declined = c.Answer.Contains("names no cause", StringComparison.Ordinal);

            Assert.True(answered || declined, $"'{c.Text}' answers with '{c.Answer}'");

            // Where it answers, the words have to be more than the citation: strip every record id
            // and there must still be a sentence left.
            if (answered)
            {
                string words = Regex.Replace(c.Answer, @"\(?`?e:\d+`?\)?", "").Trim();
                Assert.True(words.Length > 30,
                    $"'{c.Text}' answers with little more than a record id: '{c.Answer}'");
            }

            // And no genesis row is offered as support, whichever branch it took.
            foreach (EventId id in c.Records)
            {
                if (!view.Log.TryGet(id, out Event at)) continue;

                Assert.False(ContextPackBuilder.IsGenesis(at),
                    $"'{c.Text}' cites {id}, which is {EventKinds.Name(at.Kind)}");
            }
        }

        Assert.True(asked >= 3,
            $"only {asked.ToString(CultureInfo.InvariantCulture)} causal question(s) staged; the " +
            "check is not covering the category");
    }

    /// <summary>
    /// The staged answer and the retrieval path agree about what a cause is.
    ///
    /// <b>The half that would otherwise drift.</b> Two rules now say a genesis row is not a cause —
    /// one in <see cref="ContextPackBuilder.Trace"/> for the product, one in the staging that writes
    /// the facts sheet — and two copies of a rule is how the two stop matching. This asserts they
    /// give the same answer on the same records rather than trusting that they do.
    /// </summary>
    [Fact]
    public void TheStagedCausesAndTheTraceAgree()
    {
        WorldView view = World(42);

        int compared = 0;

        foreach (Event e in view.Log.Events)
        {
            if (e.Causes.Count == 0) continue;

            // A secret tip returns nothing at all — the walk refuses to open it, which is the
            // leak guard and not a disagreement about what a cause is.
            if (!ContextPackBuilder.IsRetrievable(e)) continue;

            List<EventId> traced = ContextPackBuilder.Trace(view, e.Id);
            List<EventId> direct =
                [.. e.Causes.Where(c => view.Log.TryGet(c, out Event at) && !ContextPackBuilder.IsGenesis(at))];

            // Every direct cause the staging would name is one the trace also reaches, unless the
            // trace stopped for its own reasons — a secret cause, or the depth cap.
            foreach (EventId cause in direct)
            {
                if (!view.Log.TryGet(cause, out Event at)) continue;
                if (!ContextPackBuilder.IsRetrievable(at)) continue;
                if (traced.Count >= 24) continue;

                compared++;
                Assert.Contains(cause, traced);
            }
        }

        Assert.True(compared > 100,
            $"only {compared.ToString(CultureInfo.InvariantCulture)} cause(s) compared; the two " +
            "rules were barely exercised against each other");
    }
}
