using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Inference;
using Xunit;

namespace WorldBuilder.Tests;

/// <summary>
/// v1.2 generation: what the model is shown, and what is done with what it writes back.
///
/// <b>Every test here enters at <see cref="QueryEngine.AskAsync"/>.</b> That is not a style
/// preference. Two tests passed last round while the code failed, both because they fed an input
/// the production caller never produces: one hand-fed a topic the planner does not emit, the
/// other called <c>Ground</c> where <c>AskAsync</c> did not. A test that enters below the
/// outermost callable converts silence into confidence, which is worse than having no test.
///
/// <b>And every positive case asserts that the rule read something.</b> "No finding fired" is
/// the output of a working rule and of a rule that never ran, and rounds 11 to 14 were four
/// separate instances of the second wearing the face of the first.
/// </summary>
public class GenerationTests
{
    private static WorldView World()
    {
        Simulation sim = new(42);
        sim.Run(50);
        return WorldView.Build(sim.Log, 42);
    }

    /// <summary>
    /// A client that plans as instructed and then answers with fixed text. The planner call is
    /// the one carrying a schema, which is how the two are told apart.
    /// </summary>
    private sealed class Scripted(string plan, string answer) : ILlmClient
    {
        public string ModelTag => "scripted";

        public int Plans { get; private set; }
        public int Answers { get; private set; }

        public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            bool planning = request.Schema is { Length: > 0 };
            if (planning) Plans++; else Answers++;

            return Task.FromResult(new LlmResult { Text = planning ? plan : answer, Model = ModelTag });
        }
    }

    private static string Plan(string shape, string subject, string topics, string years = "") =>
        $$"""{"shape":"{{shape}}","subject":"{{subject}}","topics":[{{topics}}]{{years}}}""";

    // ---- step 1: the pack carries the causes it used to discard ------------

    /// <summary>
    /// The bookkeeping behind Hadale's secession reaches the model, as state.
    ///
    /// Retrieval finds the two rows recording the Kebarrow Compact's standing eroding away, and
    /// the pack builder re-applied the render filter and dropped both — retrieval reading the
    /// record and the pack re-imposing the readable view over the top of it. The rows must
    /// arrive, and must arrive as a condition rather than as an event, because a measurement
    /// narrated as an event is the other half of the same mistake.
    /// </summary>
    [Fact]
    public async Task TheRowsBehindASecessionReachTheModelAsState()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("causal", "Hadale", "\"POLITY.SECESSION\""),
            "Hadale broke away in 27 [e:454].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        ContextPack pack = Assert.IsType<ContextPack>(result.Pack);

        // The rows arrived. Retrieval found them and the pack kept them.
        Assert.NotEmpty(pack.Causes);
        Assert.Contains(pack.Causes,
            c => c.Contains("standing of the Kebarrow Compact", StringComparison.Ordinal));

        // And arrived as state, not as records: nothing here is citable, and nothing here is
        // in the event list the passage is allowed to narrate.
        foreach (string cause in pack.Causes)
            Assert.DoesNotContain("e:", cause, StringComparison.Ordinal);

        foreach (EventId id in pack.Events)
            Assert.True(ContextPackBuilder.IsRenderable(view.Log.Get(id)),
                $"{id} is bookkeeping and belongs in the causes, not in the event list");
    }

    /// <summary>
    /// The failed raid that caused the secession is carried with its outcome, because the
    /// direction of that raid is the whole answer.
    ///
    /// The Compact's own attack on Griwick was beaten off. Rendered without the outcome it reads
    /// as an attack repelled, which turns a legitimacy-losing failure into a success and makes
    /// the secession it caused look arbitrary — an inversion this project has made three times.
    /// </summary>
    [Fact]
    public async Task AFailedAttackIsCarriedAsFailedRatherThanLeftToTheSentence()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("causal", "Hadale", "\"POLITY.SECESSION\""),
            "Hadale broke away in 27 [e:454].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        ContextPack pack = Assert.IsType<ContextPack>(result.Pack);
        Assert.Contains("outcome failed", pack.Body, StringComparison.Ordinal);
    }

    // ---- step 2: role and outcome as fields -------------------------------

    /// <summary>
    /// Seven records name Paernmel Has and the answer is four, which needs role and outcome to
    /// be separately readable.
    ///
    /// Role alone gives five: it drops the two killings he ordered and keeps the one that
    /// succeeded. Outcome alone gives six. Only both together give four, and neither is
    /// recoverable from the sentences with any reliability — so the pack states them.
    /// </summary>
    [Fact]
    public async Task RoleAndOutcomeAreBothCarriedForAnActorOnTwoSidesOfTheSameKindOfEvent()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "Paernmel Has", "\"CONFLICT.ASSASSINATION\""),
            "Four attempts on him failed [e:822].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("How many times was Paernmel Has the target of a failed attempt?");

        ContextPack pack = Assert.IsType<ContextPack>(result.Pack);

        Assert.Equal(7, pack.Events.Count);

        // The two distinctions, stated rather than left to be inferred.
        Assert.Contains("as the one it was done to: 5 records — 4 failed, 1 succeeded",
            pack.Body, StringComparison.Ordinal);
        Assert.Contains("as the one who acted: 2 records", pack.Body, StringComparison.Ordinal);

        // And on the records themselves, so a passage reading one line has both.
        Assert.Contains("Paernmel Has is the one it was done to; outcome failed",
            pack.Body, StringComparison.Ordinal);
        Assert.Contains("Paernmel Has is the one who acted", pack.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The figures are counted over the retrieved records and not over the log.
    ///
    /// The digest has never had a secrecy filter, because while its only caller counted for a
    /// chronicle built from the same walk it never needed one. Counting from the log on the
    /// answer path would let a total include the very record retrieval withheld, and would let
    /// an answer state a total its own records contradict.
    /// </summary>
    [Fact]
    public async Task FiguresAreCountedOverTheRecordsTheAnswerWasGiven()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "Paernmel Has", "\"CONFLICT.ASSASSINATION\""),
            "Four attempts failed [e:822].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("How many times was Paernmel Has the target of a failed attempt?");

        ContextPack pack = Assert.IsType<ContextPack>(result.Pack);

        // Five attempts on him among the retrieved records, and the block says five.
        int aimedAtHim = 0;
        foreach (EventId id in pack.Events)
        {
            Event e = view.Log.Get(id);
            if (e.Kind == EventKind.ConflictAssassination && e.Object == result.Plan.Entity) aimedAtHim++;
        }

        Assert.Equal(5, aimedAtHim);
        Assert.Contains("attempts made on this person's life: 5 in total",
            pack.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing secret reaches the model through any part of the pack, including the two parts
    /// this round added.
    ///
    /// Both are new surfaces. The causes section states what a withheld row established, which
    /// would leak a conspiracy in a form no proper-noun check could see; the figures block used
    /// to be counted from the whole log, and the digest has never had a secrecy filter of its
    /// own. A chronicle that declines to narrate a plot is worth nothing if a question totals it.
    /// </summary>
    [Fact]
    public async Task NothingSecretReachesThePackByAnyRoute()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "Paernmel Has", "\"POLITY.COUP_RESOLVED\""),
            "Three men conspired against him [e:901].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Who conspired against Paernmel Has?");

        ContextPack pack = Assert.IsType<ContextPack>(result.Pack);

        foreach (EventId id in pack.Events)
            Assert.NotEqual(Visibility.Secret, view.Log.Get(id).Scope);

        // The two plotters never uncovered. Their names are in the log and must not be anywhere
        // in what the model is shown — not in the events, not in the causes, not in a total.
        foreach (string hidden in new[] { "Drouldthas Stour", "Wuldweald Valdrith" })
            Assert.DoesNotContain(hidden, pack.Body, StringComparison.Ordinal);

        Assert.Equal(3, pack.Events.Count);
    }

    // ---- step 3: verbatim fields the planner copied -----------------------

    /// <summary>
    /// A year the planner invented stops the query rather than narrowing it.
    ///
    /// This is the failure with no symptom. A mistyped subject resolves to nothing and the
    /// question comes back empty, which is visible; a mistyped year resolves perfectly and
    /// returns a confident, fluent answer about a stretch of history nobody asked about.
    /// </summary>
    [Fact]
    public async Task AYearTheQuestionDoesNotNameStopsTheQuery()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "the Hadale Commune", "\"POLITY.SUCCESSION\"", ",\"fromYear\":41,\"toYear\":41"),
            "Somebody ruled it.");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Who ruled the Hadale Commune in year 51?");

        Assert.NotNull(result.Unresolvable);
        Assert.Contains("41", result.Unresolvable, StringComparison.Ordinal);
        Assert.Empty(result.Retrieved);

        // Planned, and then never asked to write. Running the query and discarding the answer
        // would leave the wrong window in play for anything that reads the retrieved set.
        Assert.Equal(1, client.Plans);
        Assert.Equal(0, client.Answers);
    }

    /// <summary>
    /// And a year the question does name is acted on exactly as before, which is the half of
    /// this that a validator can quietly break.
    /// </summary>
    [Fact]
    public async Task AYearTheQuestionDoesNameIsUsed()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "the Hadale Commune", "\"POLITY.SUCCESSION\"", ",\"fromYear\":51,\"toYear\":51"),
            "Durnrin Drar held it [e:927].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Who ruled the Hadale Commune in year 51?");

        Assert.Null(result.Unresolvable);
        Assert.NotEmpty(result.Retrieved);
        Assert.Contains(result.Retrieved,
            id => view.Describe(id).Contains("Durnrin Drar", StringComparison.Ordinal));
    }

    // ---- step 5: the checker on the answer path ---------------------------

    /// <summary>
    /// An answer asserting a killing the records do not hold is a fabrication, and fires.
    ///
    /// This is the split. The lookup was made — the passage named a person and a murder, and the
    /// index was asked — and it came back empty. Recording that as "unresolvable" was a quiet
    /// miss in a chronicle; in an answer to a direct question it is a wrong answer returned with
    /// the checker reporting nothing wrong.
    /// </summary>
    [Fact]
    public async Task AnAssertedKillingTheRecordsDoNotHoldFiresRatherThanGoingUnresolved()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "the Vea Lode Covenant", "\"POLITY.SUCCESSION\""),
            "The rise of the house followed the murder of Kou Peis [e:878].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Who ruled the Vea Lode Covenant?");

        Assert.Contains(result.Fatal, f => f.Kind == "no-such-killing");

        // The rule read the sentence rather than merely failing to object to it.
        RuleCounts counts = result.Fabrication.Coverage.Rules[RuleNames.Succession];
        Assert.True(counts.Extracted > 0, "the succession rule extracted nothing from a murder claim");
        Assert.Equal(counts.Extracted, counts.Checked + counts.Unresolvable);
    }

    /// <summary>
    /// A rejected answer is asked for again before it is thrown away.
    ///
    /// Withholding was the first resort rather than the last, and most of what fires here is a
    /// slip rather than a misunderstanding: the first live run opened an answer with "Hdale",
    /// one letter off a real place, and everything else about it was right. The chronicle has
    /// retried since round 4; the query layer inherited the checker and not the second chance.
    /// </summary>
    [Fact]
    public async Task ARejectedAnswerIsRetriedBeforeItIsWithheld()
    {
        WorldView view = World();

        // Wrong the first time, right the second — which is what a correction is for.
        Retrying client = new(
            Plan("causal", "Hadale", "\"POLITY.SECESSION\""),
            "Hdale broke from the Kebarrow Compact in 27 [e:454].",
            "Hadale broke from the Kebarrow Compact in 27 [e:454].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        Assert.Equal(2, client.Answers);
        Assert.Empty(result.Fatal);
        Assert.Null(result.Withheld);
        Assert.Contains("Hadale broke from the Kebarrow Compact", result.Answer, StringComparison.Ordinal);

        // The correction named the offending word rather than restating the rule.
        Assert.Contains("Hdale", client.LastPrompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Retrying is bounded. A defect that survives two corrections is not a slip, and a fourth
    /// attempt is a loop rather than a repair.
    /// </summary>
    [Fact]
    public async Task RetryingStopsAndTheProseIsWithheld()
    {
        WorldView view = World();
        const string wrong = "Hdale broke from the Kebarrow Compact in 27 [e:454].";

        Retrying client = new(Plan("causal", "Hadale", "\"POLITY.SECESSION\""), wrong, wrong);

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        Assert.Equal(3, client.Answers);
        Assert.NotEmpty(result.Fatal);
        Assert.Equal(wrong, result.Withheld);
    }

    /// <summary>A client that answers differently the second time it is asked.</summary>
    private sealed class Retrying(string plan, string first, string second) : ILlmClient
    {
        public string ModelTag => "scripted";

        public int Answers { get; private set; }
        public string LastPrompt { get; private set; } = "";

        public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            if (request.Schema is { Length: > 0 })
                return Task.FromResult(new LlmResult { Text = plan, Model = ModelTag });

            LastPrompt = request.Prompt;
            Answers++;
            return Task.FromResult(new LlmResult { Text = Answers == 1 ? first : second, Model = ModelTag });
        }
    }

    /// <summary>
    /// A fatal finding costs the prose. A chronicle can drop one of fifteen sections and print a
    /// note where it stood; an answer has one of one, and nowhere to put a warning.
    /// </summary>
    [Fact]
    public async Task ProseCarryingAFabricationIsNotReturned()
    {
        WorldView view = World();
        const string invented = "The rise of the house followed the murder of Kou Peis [e:878].";

        Scripted client = new(Plan("factual", "the Vea Lode Covenant", "\"POLITY.SUCCESSION\""), invented);

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Who ruled the Vea Lode Covenant?");

        Assert.NotEmpty(result.Fatal);
        Assert.NotEqual(invented, result.Answer);
        Assert.DoesNotContain("murder of Kou Peis", result.Answer, StringComparison.Ordinal);

        // Kept, because it is the only diagnosable artefact of a bad generation — but kept
        // somewhere a caller cannot mistake for the answer.
        Assert.Equal(invented, result.Withheld);

        // What is returned instead is the engine's own account of the records, which is
        // template-generated and so cannot itself carry an invention. The question is still
        // answerable from it: every ruler the records name is there to be read.
        Assert.Contains("Stald Gearngoll", result.Answer, StringComparison.Ordinal);
        Assert.Contains("Herpeim Raern", result.Answer, StringComparison.Ordinal);
    }

    /// <summary>
    /// A misspelled place name at the start of an answer is a place the world does not contain.
    ///
    /// The sentence-start exemption exists for good reason — chasing capitalised ordinary
    /// English with a stopword list was a losing game — but it waved this through: "Hdale broke
    /// from the Kebarrow Compact" opens an answer with a name nowhere in the record, and in
    /// three sentences there is no second use of the word to mark it as a name. What gives it
    /// away is that it is one letter from a real one.
    /// </summary>
    [Fact]
    public async Task AMisspelledNameOpeningAnAnswerIsCaught()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("causal", "Hadale", "\"POLITY.SECESSION\""),
            "Hdale broke from the Kebarrow Compact in 27 [e:454].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        Assert.Contains(result.Fatal, f => f.Kind == "name" && f.Token == "Hdale");
        Assert.NotNull(result.Withheld);

        // And the rule looked at the answer's tokens rather than reaching this by accident.
        Assert.True(result.Fabrication.Coverage.Rules[RuleNames.Naming].Extracted > 0);
    }

    /// <summary>
    /// The same exemption still holds for ordinary English, which is what it is for. A word that
    /// opens a sentence and is not near any name must not be reported as an invented one.
    /// </summary>
    [Fact]
    public async Task AnOrdinaryWordOpeningASentenceIsStillNotAName()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("causal", "Hadale", "\"POLITY.SECESSION\""),
            "Hadale broke away in 27 [e:454]. Consequently the Hadale Commune was founded [e:454].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        Assert.DoesNotContain(result.Fabrication.Findings,
            f => f.Kind == "name" && f.Token == "Consequently");
    }

    /// <summary>
    /// A citation to a record the engine never supplied is fatal too. An id produced from
    /// memory is precisely the failure retrieval exists to make impossible.
    /// </summary>
    [Fact]
    public async Task ACitationOutsideTheRetrievedSetIsFatal()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "the Vea Lode Covenant", "\"POLITY.SUCCESSION\""),
            "Stald Gearngoll took the seat in 29 [e:99999].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Who ruled the Vea Lode Covenant?");

        Assert.Contains("e:99999", result.BadCitations);
        Assert.Contains(result.Fatal, f => f.Kind == "unsupported-citation");
        Assert.NotNull(result.Withheld);
    }

    /// <summary>
    /// A finding that is a defect of style rather than of fact leaves the answer standing.
    ///
    /// The opposite disposal would be worse than the finding: an answer held back because two
    /// powers in the world share a last word has traded a readability problem for no answer at
    /// all, and that trade cost seven true sections at round 10.
    /// </summary>
    [Fact]
    public async Task ANonFatalFindingIsLoggedAndTheAnswerStands()
    {
        WorldView view = World();
        const string loose = "The Compact lost Hadale in 27 [e:454].";

        Scripted client = new(Plan("causal", "Hadale", "\"POLITY.SECESSION\""), loose);

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        Assert.Contains(result.Fabrication.Findings, f => f.Kind == "ambiguous-short-name");
        Assert.Empty(result.Fatal);
        Assert.Equal(loose, result.Answer);
        Assert.Null(result.Withheld);
    }

    /// <summary>
    /// Every rule accounts for everything it read, on every answer the suite produces.
    ///
    /// <c>extracted == checked + unresolvable</c> is the whole of it: a rule that drops an
    /// assertion by a path recording neither presents as a rule that found nothing wrong.
    /// </summary>
    [Theory]
    [InlineData("Who ruled the Vea Lode Covenant?", "factual", "the Vea Lode Covenant", "\"POLITY.SUCCESSION\"")]
    [InlineData("Why did Hadale break from the Kebarrow Compact?", "causal", "Hadale", "\"POLITY.SECESSION\"")]
    [InlineData("How many died in the plague at Griwick?", "factual", "Griwick", "\"ECONOMY.PLAGUE\"")]
    public async Task EveryRuleAccountsForWhatItRead(string question, string shape, string subject, string topics)
    {
        WorldView view = World();
        Scripted client = new(
            Plan(shape, subject, topics),
            "Stald Gearngoll took the seat of the Vea Lode Covenant in 29, and 474 died at Griwick.");

        QueryResult result = await new QueryEngine(client, view).AskAsync(question);

        Assert.Empty(result.Fabrication.Coverage.Unaccounted());

        // And the checker read the prose at all, rather than being handed an empty passage and
        // reporting a clean bill for it.
        Assert.True(result.Fabrication.Coverage.Rules[RuleNames.Naming].Extracted > 0,
            "the checker examined no token of an answer that contains prose");
    }

    /// <summary>
    /// The pack's entity tags do not reach the answer.
    ///
    /// Every name in a pack carries one so two similar names can be told apart, and the model
    /// copies them into prose perhaps a third of the time. Told not to, it stops for a run and
    /// then does it again — and a tag is not a fabrication, so nothing fires, nothing retries,
    /// and "the Wurn League (f:1) was finished in 20" is returned to a reader. It is punctuation
    /// from another document, and removing it is the engine's job rather than the model's.
    /// </summary>
    [Fact]
    public async Task EntityTagsDoNotReachTheAnswer()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "the Vea Lode Covenant", "\"POLITY.SUCCESSION\""),
            "Stald Gearngoll (a:80) took the seat of the Vea Lode Covenant (f:7) in 29 [e:506].");

        QueryResult result = await new QueryEngine(client, view).AskAsync("Who ruled the Vea Lode Covenant?");

        Assert.DoesNotContain("(a:80)", result.Answer, StringComparison.Ordinal);
        Assert.DoesNotContain("(f:7)", result.Answer, StringComparison.Ordinal);

        // The names survive, and so does the one reference an answer is meant to carry.
        Assert.Contains("Stald Gearngoll took the seat", result.Answer, StringComparison.Ordinal);
        Assert.Contains("[e:506]", result.Answer, StringComparison.Ordinal);
    }

    // ---- causal answers may only join what the record joins ----------------

    /// <summary>
    /// An answer may assert a link only where the record carries one.
    ///
    /// Every name and year can be right and the sentence still false, because what is invented
    /// is the relation between them. This is the same class as the succession links the
    /// chronicle fabricated for five rounds, arriving now through a "why" question — and the
    /// engine stores causality as an edge, so it is the one fabrication that can be checked
    /// exactly rather than approximately.
    /// </summary>
    [Fact]
    public async Task AnAssertedCauseWithNoEdgeBehindItFires()
    {
        WorldView view = World();

        // The plague at Griwick and the secession of Hadale: both real, both in this pack's
        // reach, and neither causes the other.
        Scripted client = new(
            Plan("causal", "Hadale", "\"POLITY.SECESSION\""),
            "Hadale broke away because of the raid [e:454], which led to the secession [e:448].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        // e:448 causes e:454, so this pair IS linked and must not fire — the rule has to be
        // right about the true case before its firing means anything.
        Assert.DoesNotContain(result.Fabrication.Findings, f => f.Kind == "unsupported-link");

        RuleCounts action = result.Fabrication.Coverage.Rules[RuleNames.Action];
        Assert.True(action.Extracted > 0, "the causal rule read no link from a sentence asserting one");
    }

    /// <summary>
    /// "Collapse" means a power is finished. A house whose standing has fallen is in decline,
    /// and the neighbouring answer in this suite uses "destroyed" for the other thing.
    /// </summary>
    [Fact]
    public async Task CollapseIsNotUsedForADecline()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("causal", "Threi Cut", "\"POLITY.REVOLT\""),
            "Threi Cut rose in 51 [e:1035]. This collapse followed the killing of Keithfal Naell [e:999].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Threi Cut rise against the Vea Lode Covenant in 51?");

        Assert.Contains(result.Fatal, f => f.Kind == "wrong-collapse");
        Assert.NotNull(result.Withheld);
    }

    // ---- a rule switched off must not read as a rule that found nothing ----

    /// <summary>
    /// The completeness rules do not report as inert on the answer path, because they were never
    /// offered the input.
    ///
    /// Inert is the one signal that says a rule never saw what it was meant to read, and it is
    /// only worth anything if it is scarce. Registering two rules that are deliberately gated
    /// off spent it against every answer the query layer will ever produce — and a signal that
    /// fires on every case is indistinguishable from no signal at all.
    ///
    /// This family has now appeared five times in this project, each instance silent and each
    /// found by accident. Pinned so the sixth is not.
    /// </summary>
    [Fact]
    public async Task RulesGatedOffForAnAnswerDoNotReportAsInert()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "the Vea Lode Covenant", "\"POLITY.SUCCESSION\""),
            "Stald Gearngoll held the seat from 29 to 45 [e:506].");

        QueryResult result = await new QueryEngine(client, view).AskAsync("Who ruled the Vea Lode Covenant?");

        IReadOnlyDictionary<string, RuleCounts> rules = result.Fabrication.Coverage.Rules;

        Assert.DoesNotContain(RuleNames.Coverage, rules.Keys);
        Assert.DoesNotContain(RuleNames.Shape, rules.Keys);

        // And the rules that do run are still registered, so a genuine zero is still visible.
        Assert.Contains(RuleNames.Succession, rules.Keys);
        Assert.Contains(RuleNames.Tenure, rules.Keys);
    }

    /// <summary>
    /// A finding is counted against the rule that produced it.
    ///
    /// The vocabulary scan records its reading under <c>naming</c> and raised its verdicts under
    /// <c>name</c> and <c>number</c>, which were mapped to nothing — so the coverage table
    /// showed a rule that read a hundred tokens and objected to none beside a rule that had read
    /// nothing and objected once. Both lines were wrong and the pair was self-contradictory,
    /// which is worse than either.
    /// </summary>
    [Fact]
    public async Task AFindingIsCountedAgainstTheRuleThatProducedIt()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("causal", "Hadale", "\"POLITY.SECESSION\""),
            "Hdale broke from the Kebarrow Compact in 27 [e:454].");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Hadale break from the Kebarrow Compact?");

        Assert.Contains(result.Fatal, f => f.Kind == "name");

        IReadOnlyDictionary<string, RuleCounts> rules = result.Fabrication.Coverage.Rules;

        // No rule called "name" or "number" exists; the scan that reads them is "naming".
        Assert.DoesNotContain("name", rules.Keys);
        Assert.DoesNotContain("number", rules.Keys);

        RuleCounts naming = rules[RuleNames.Naming];
        Assert.True(naming.Extracted > 0, "the scan reported no reading");
        Assert.True(naming.Fired > 0, "the scan's own finding was counted against something else");
    }

    // ---- step 6: nothing found, and false premises ------------------------

    /// <summary>
    /// Nothing retrieved means the model is never asked. A question that found no records is
    /// exactly the one it would answer best from memory.
    /// </summary>
    [Fact]
    public async Task NothingRetrievedNeverReachesTheModel()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "Drelthorn League", "\"POLITY\""),
            "The Drelthorn League fell in 30.");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("What happened to the Drelthorn League?");

        Assert.Empty(result.Retrieved);
        Assert.Equal(0, client.Answers);
        Assert.DoesNotContain("30", result.Answer, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing found is four different facts about the world, and they were one sentence about a
    /// filing system.
    ///
    /// The withheld case is the one that made it urgent: the record holds a failed attempt on
    /// Sothkel Sald in 35, by a named man, kept secret — and "the records do not cover that" is
    /// a false statement about the world, made in order to keep a secret the world does hold.
    /// </summary>
    [Theory]
    [InlineData("What happened to the Drelthorn League?", "factual", "Drelthorn League", "\"POLITY\"",
        "", EmptyReason.NoSuchEntity, "no Drelthorn League")]
    [InlineData("Who ruled the Sworn Men of Meigate in year 5?", "factual", "the Sworn Men of Meigate",
        "\"POLITY.SUCCESSION\"", ",\"fromYear\":5,\"toYear\":5", EmptyReason.OutsideLifetime, "did not exist until 19")]
    [InlineData("Who attempted to kill Sothkel Sald in year 35?", "factual", "Sothkel Sald",
        "\"CONFLICT.ASSASSINATION\"", ",\"fromYear\":35,\"toYear\":35", EmptyReason.Withheld, "never found out")]
    public async Task NothingFoundBranchesOnWhyThereIsNothing(
        string question, string shape, string subject, string topics, string years,
        EmptyReason expected, string says)
    {
        WorldView view = World();
        Scripted client = new(Plan(shape, subject, topics, years), "Something invented.");

        QueryResult result = await new QueryEngine(client, view).AskAsync(question);

        Assert.Equal(expected, result.Empty);
        Assert.Empty(result.Retrieved);
        Assert.Equal(0, client.Answers);
        Assert.Contains(says, result.Answer, StringComparison.OrdinalIgnoreCase);

        // Every branch speaks about the world. A reader asking about a place is owed a fact
        // about the place, not a report on a search that failed.
        foreach (string clerical in new[] { "record", "log", "retrieved", "data", "entry" })
            Assert.DoesNotContain(clerical, result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The withheld branch says an attempt was made and never says by whom. Naming the man is a
    /// leak; denying the attempt is a lie. "Never found out" is neither.
    /// </summary>
    [Fact]
    public async Task TheWithheldBranchNamesNobody()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("factual", "Sothkel Sald", "\"CONFLICT.ASSASSINATION\"", ",\"fromYear\":35,\"toYear\":35"),
            "unused");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Who attempted to kill Sothkel Sald in year 35?");

        // Gatros Hearn made that attempt, and the record keeping it secret is the only record
        // of it. Neither he nor anyone else may be named.
        //
        // Compared word by word, because a substring test is not a name test: this world holds
        // a "Ho", and "whoever" contains it.
        HashSet<string> words = [.. result.Answer.ToLowerInvariant()
            .Split([' ', ',', '.', '\''], StringSplitOptions.RemoveEmptyEntries)];

        foreach (Actor a in view.State.Actors)
            Assert.DoesNotContain(ContextPackBuilder.Surname(a.Name), words);
    }

    /// <summary>
    /// A false premise is stated plainly and not explained around — and, like an empty result,
    /// never reaches the model, which asked why X happened will explain why X happened.
    /// </summary>
    [Fact]
    public async Task AFalsePremiseIsRejectedWithoutGeneration()
    {
        WorldView view = World();
        Scripted client = new(
            Plan("causal", "Stonand Ker", "\"POLITY.SUCCESSION\""),
            "He lost the seat after a challenge.");

        QueryResult result = await new QueryEngine(client, view)
            .AskAsync("Why did Stonand Ker lose the seat of the Kebarrow Compact?");

        Assert.NotNull(result.FalsePremise);
        Assert.Equal(0, client.Answers);
        Assert.Contains("never held a seat", result.Answer, StringComparison.Ordinal);
    }
}
