using System.Globalization;
using System.Text;
using System.Text.Json;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;

namespace WorldBuilder.Inference;

/// <summary>What kind of answer the question wants.</summary>
public enum QueryShape
{
    /// <summary>"Why did X happen" — walk the causal edges backward and show the steps.</summary>
    Causal,
    /// <summary>"Who has ruled X" — filter the log and list.</summary>
    Factual,
}

/// <summary>
/// A question turned into a retrieval instruction. The model plans; the engine executes.
/// Deliberately small — the model never names an event id, only a subject and a topic, because
/// ids it produces from memory would be exactly the fabrication this design exists to prevent.
/// </summary>
public sealed record QueryPlan
{
    public required QueryShape Shape { get; init; }

    /// <summary>The person, place or power the question is about, as written in the question.</summary>
    public required string Subject { get; init; }

    /// <summary>Event families worth retrieving, as dotted prefixes ("POLITY", "CONFLICT.RAID").</summary>
    public IReadOnlyList<string> Topics { get; init; } = [];

    public int FromYear { get; init; } = int.MinValue;
    public int ToYear { get; init; } = int.MaxValue;

    /// <summary>Resolved from <see cref="Subject"/> by the engine, never by the model.</summary>
    public EntityId Entity { get; init; } = EntityId.None;

    /// <summary>The original question, kept so retrieval can pick the record it is about.</summary>
    public string Question { get; init; } = "";

    public const string Schema = """
        {"type":"object","properties":{
          "shape":{"type":"string","enum":["causal","factual"]},
          "subject":{"type":"string"},
          "topics":{"type":"array","items":{"type":"string"}},
          "fromYear":{"type":"integer"},
          "toYear":{"type":"integer"}},
         "required":["shape","subject","topics"]}
        """;
}

public sealed record QueryResult
{
    public required string Question { get; init; }
    public required QueryPlan Plan { get; init; }
    public required IReadOnlyList<EventId> Retrieved { get; init; }
    public required string Answer { get; init; }
    public required FabricationReport Fabrication { get; init; }

    /// <summary>Set when the question assumed something the world does not contain.</summary>
    public string? FalsePremise { get; init; }

    /// <summary>Event ids the answer cites that were not in the retrieved set.</summary>
    public required IReadOnlyList<string> BadCitations { get; init; }

    /// <summary>
    /// Set where the query could not be run at all, as opposed to running and finding nothing.
    ///
    /// The two are opposite situations and conflating them is the same error the checker made
    /// with its own <c>unresolvable</c> bucket: "the lookup could not be performed" is a defect
    /// to be reported, and "the lookup was performed and the world holds nothing" is an answer.
    /// </summary>
    public string? Unresolvable { get; init; }

    /// <summary>The pack the answer was written from, where one was built.</summary>
    public ContextPack? Pack { get; init; }

    /// <summary>
    /// The prose the model produced, kept when the checker refused to let it be returned.
    ///
    /// Retained rather than discarded because it is the only diagnosable artefact of a failure
    /// of generation. <see cref="Answer"/> is what a caller may show.
    /// </summary>
    public string? Withheld { get; init; }

    /// <summary>Findings that made the prose unreturnable.</summary>
    public IReadOnlyList<Fabrication> Fatal { get; init; } = [];

    /// <summary>Why nothing was found, where nothing was.</summary>
    public EmptyReason Empty { get; init; } = EmptyReason.None;

    public bool Answered => Retrieved.Count > 0;
}

/// <summary>
/// Why a question came back with nothing.
///
/// Four situations that used to share one sentence — "The records do not cover that." — which
/// was log-referential in every case and, in one of them, false. The record covers who tried to
/// kill Sothkel Sald in 35 exactly: a failed attempt, by a named man, kept secret. Answering
/// that the records do not cover it is a statement about the world, and it is untrue.
///
/// <b>This is the unresolvable-versus-fired conflation one layer out</b> — absent and withheld
/// collapsed into one output, for the fifth time in this project. It also decides whether the
/// epistemic layer can be built on top of this at all: that layer's whole premise is that
/// not-known and not-true are different, and a query layer that cannot say which it means
/// cannot carry it.
/// </summary>
public enum EmptyReason
{
    /// <summary>Something was found, so this does not apply.</summary>
    None,

    /// <summary>The subject names nothing in this world.</summary>
    NoSuchEntity,

    /// <summary>The subject is real but did not exist at the time asked about.</summary>
    OutsideLifetime,

    /// <summary>Matching records exist and every one of them is secret.</summary>
    Withheld,

    /// <summary>Subject and window are good; nothing of that kind happened.</summary>
    NoOccurrence,
}

/// <summary>
/// Everything settled before a word is generated: the plan, what it retrieved, and the pack the
/// answer will be written from — or the reason there will not be one.
/// </summary>
/// <param name="Pack">Null where nothing was retrieved, or where the query was never run.</param>
/// <param name="FalsePremise">Set where the question assumed something untrue.</param>
/// <param name="Unresolvable">Set where the query could not be run at all.</param>
public sealed record QueryPreparation(
    QueryPlan Plan,
    List<EventId> Retrieved,
    ContextPack? Pack,
    string? FalsePremise,
    string? Unresolvable);

/// <summary>
/// Natural language over the log, answered by retrieval and then generation.
///
/// The model is never asked what happened. It is asked what to look for; the engine looks;
/// and the model then writes only from what was found. Stuffing the whole world into the
/// context window would work at fifty years and start inventing confidently the moment the
/// world outgrew it, so the retrieval step is the feature, not an optimisation.
/// </summary>
public sealed class QueryEngine(ILlmClient client, WorldView view)
{
    private readonly ILlmClient _client = client;
    private readonly WorldView _view = view;

    private const string PlannerSystem = """
        You turn a question about a historical archive into a retrieval instruction.
        You do not answer the question. You do not know the archive's contents.

        shape: "causal" if the question asks why something happened or what led to it;
               "factual" if it asks who, what, when, how many, or for a list.
        subject: the single person, place or power the question is about, copied from the
               question as written. No titles, no articles. If several are named, pick the
               one the question is chiefly about.
        topics: what kinds of thing to search for. BE AS SPECIFIC AS THE QUESTION ALLOWS —
               a broad family pulls back hundreds of unrelated things. Choose from:
                 POLITY.SUCCESSION, POLITY.CHALLENGE, POLITY.SECESSION, POLITY.PARTITION,
                 POLITY.EXILE, POLITY.COLLAPSE, POLITY.REVOLT, POLITY.APPOINTMENT,
                 POLITY.COUP_RESOLVED
                 CONFLICT.ASSASSINATION, CONFLICT.BATTLE, CONFLICT.RAID, CONFLICT.CONQUEST
                 DIPLO.WAR_DECLARED, DIPLO.PEACE_SIGNED, DIPLO.ALLIANCE_FORMED, DIPLO.INSULT
                 ECONOMY.FAMINE, ECONOMY.PLAGUE, ECONOMY.TRADE_PACT
                 LIFE.BIRTH, LIFE.DEATH_NATURAL, LIFE.DEATH_VIOLENT, LIFE.MARRIAGE
               Or a bare family (POLITY, CONFLICT, DIPLO, ECONOMY, LIFE) only when the
               question really is that broad. Empty means everything.
               "who ruled X"      -> POLITY.SUCCESSION, POLITY.CHALLENGE
               "who broke away"   -> POLITY.SECESSION, POLITY.PARTITION
               "attempts on X"    -> CONFLICT.ASSASSINATION
               "who conspired"    -> POLITY.COUP_RESOLVED  (a plot is known only once
                                    uncovered; there is no topic for the plotting itself)
               "who ruled X in N" -> POLITY.SUCCESSION, and set fromYear and toYear to N
        fromYear/toYear: only if the question names a period. Otherwise 0.
        """;

    private const string AnswerSystem = """
        You answer questions from an archive of historical records, and from nothing else.

        RULES:
        - Use ONLY the records provided. They are the entire archive available to you.
        - No name, place, date, number, motive or action may appear in your answer unless it
          is in the material below. If it is not there, it does not exist.
        - Copy every name letter for letter from the list of people, places and powers. Do not
          shorten one, do not re-spell one, and do not write a name from memory — check it
          against that list as you write it, including the first word of a sentence. Write the
          name only: never the bracketed tag beside it, so "Hadale" and never "Hadale (p:2)".
        - If they do not answer the question, say plainly that the records do not say.
          Do not guess, and do not pad the answer with what they do say instead.
        - Cite the record that SUPPORTS each claim, in square brackets: [e:415]. Cite the
          record the fact comes from, not the record you are explaining.
        - Never state what anyone felt, feared or intended.
        - Absolute years only ("in 42"), never "the following year".
        - Date what you name. Every record carries a year; if you say a thing happened, say
          when, using that record's own year and no other.
        - Do no arithmetic. Any count or total is supplied under "WHAT THESE RECORDS ADD UP
          TO"; state those figures as given and invent no others.
        - A figure belongs to the thing it was counted for. Do not attach a figure given for a
          whole period to one year, one person, or one part of it.
        - Where you are told which side someone was on, use it. A record of something done to
          a person is not a record of something they did, and the two are never added up.
        - Lines under "HOW THINGS STOOD" are conditions, not events. They explain why something
          happened, and where they bear on the question you should use them. Never tell one as
          though it happened on a day, and never cite one.

        TWO RULES THAT PULL IN OPPOSITE DIRECTIONS. Hold both.

        RULE ONE — INVENT NO PARTICULAR. No person, place, date, number, motive, feeling,
        intention or action that is not in the material above. This is absolute.

        RULE TWO — DESCRIBE THE SHAPE OF WHAT IS THERE. You are not a list. Where the material
        shares a year, a target, an outcome or a cause, say so once as the thing it is, rather
        than repeating a sentence per record:
          "Three conspiracies against Paernmel Has were uncovered in 46, by Stonand Ker,
          Keithfal Naell and Throll Kell" — one sentence, carrying everything three
          near-identical sentences carried between them. Good.
          "X conspired in 46. Y conspired in 46. Z conspired in 46." — that is the material
          with the names substituted in, and it is worth less than the material.
        Comparing things you were given invents nothing. Do not explain the pattern, do not say
        what anyone thought of it, and never turn it into a number you worked out yourself.

        RULE TWO NEVER OVERRULES RULE ONE. Shape is how the sentence is built, not how much of
        the answer is left out. Naming the pattern and then dropping the members is a worse
        answer than the list you were avoiding:
          "Stonand Ker conspired against Paernmel Has in 46" — asked who conspired, when three
          men did. Two are missing and nothing says so. FORBIDDEN.
          "The Wurn League, the Griwick Compact and the Sworn Men of Meigate were destroyed" —
          the years were given and have been thrown away. FORBIDDEN.
        Every member the question asks for is named, and each keeps its own year.

        WHERE A COUNT IS THE ANSWER, NAME WHAT WAS COUNTED. A bare number answers the question
        and withholds everything the person asking wants next.
          "He was the target of four failed attempts" — the count and nothing else. Not enough.
          "Four attempts on him failed: by Stonand Ker in 43, Keithfal Naell in 45, Throll Kell
          in 46 and Drouldthas Stour in 49" — the count, and who and when. Good.
        Take the count from the figures you were given; take the names and years from the
        records themselves.

        A REIGN IS A SPAN, NOT A DATE. A year of accession alone does not say whether a man held
        the seat for sixteen years or for one. Where you are given a span, state it.

        NEVER ASSERT A CAUSE THE MATERIAL DOES NOT CARRY. A record says what caused it, in
        brackets after it. You may join two things only where that link is written down. A
        description attached to an event — how far a standing had fallen, how many followers
        were left — is part of that event, not a separate thing that something else caused.

        WORDS THAT MEAN ONE THING. "Collapse" and "destroyed" mean a power is finished and gone.
        A power whose standing has fallen is in decline; it has not collapsed, and saying so of
        a house that is still holding its places is false.
        - Write about the world, not about this archive. Never say "the records show",
          "N events matched", or "according to the log".
        - Answer all of the question. Where several things answer it, every one of them is part
          of the answer: three men who conspired are three men, and naming one of them is a
          wrong answer, not a short one.
        - Then be brief. Two or three sentences where that is enough. Brevity is what you do
          with the sentences, never what you leave out of them.
        - Give the answer only. Never think out loud, never write "wait" or "let me check",
          never explain what you are looking for. If something is missing, leave it out.
        """;

    /// <summary>
    /// A "why" question wants the sequence, not just the last link. The records are handed over
    /// oldest first with their causal edges marked, so the answer can walk them.
    /// </summary>
    private const string CausalHint = """

        The records below form a chain of cause and effect, oldest first. Answer by walking it:
        say what set it off and how it led to the thing asked about. Do not stop at the last step.
        """;

    /// <summary>
    /// Everything the engine does before the model is asked to write: plan, validate, retrieve,
    /// and build the pack.
    ///
    /// Separated so it can be inspected without generation — <c>wb ask --pack</c> and the suite's
    /// retrieval mode both run exactly this and stop. Not a second path: <see cref="AskAsync"/>
    /// calls it, so what is inspected is what is used.
    /// </summary>
    public async Task<QueryPreparation> PrepareAsync(string question, CancellationToken ct = default)
    {
        QueryPlan plan = await PlanAsync(question, ct);

        // The planner's own copies of the question's words, checked before they are acted on.
        // Subject resolution already falls back to the question when the planner mistypes a
        // name; a year has no such recovery, because a wrong one resolves perfectly and simply
        // asks about the wrong stretch of history.
        if (YearNotInQuestion(plan) is { } invented)
        {
            return new QueryPreparation(plan, [], null, null,
                $"The question could not be read reliably: it was searched against year {invented}, " +
                "which the question does not name.");
        }

        // Checked before anything is retrieved. A model asked why X happened will explain why X
        // happened, so a question carrying a false premise has to be stopped at the door rather
        // than handed to the model with the hope that it notices.
        if (FalsePremiseIn(question, plan) is { } wrong)
            return new QueryPreparation(plan, [], null, wrong, null);

        List<EventId> retrieved = Retrieve(plan);
        if (retrieved.Count == 0) return new QueryPreparation(plan, retrieved, null, null, null);

        // Built through the same pack machinery the chronicle uses, so a query answer gets the
        // engine-computed figures rather than counting retrieved events for itself.
        ContextPack pack = ContextPackBuilder.FromEvents(
            _view,
            plan.Shape == QueryShape.Causal ? PackKind.CausalChain : PackKind.Year,
            $"records concerning {plan.Subject}",
            retrieved,
            plan.Entity,
            plan.FromYear,
            plan.ToYear);

        return new QueryPreparation(plan, retrieved, pack, null, null);
    }

    public async Task<QueryResult> AskAsync(string question, CancellationToken ct = default)
    {
        QueryPreparation prepared = await PrepareAsync(question, ct);
        QueryPlan plan = prepared.Plan;

        if (prepared.Unresolvable is { } cannot)
            return Plain(question, plan, [], cannot) with { Unresolvable = cannot };

        if (prepared.FalsePremise is { } wrong)
            return Plain(question, plan, [], wrong) with { FalsePremise = wrong };

        // Nothing found, and so nothing to generate. The model is not called: a question that
        // retrieved nothing is precisely the one it would answer best from memory. What is said
        // instead depends on why there was nothing, which is four different facts about the
        // world and was one sentence about a filing system.
        if (prepared.Pack is not { } pack)
        {
            EmptyReason why = WhyEmpty(plan);
            return Plain(question, plan, prepared.Retrieved, Nothing(plan, why)) with { Empty = why };
        }

        List<EventId> retrieved = prepared.Retrieved;

        string answer = await WriteAsync(question, plan, pack, "", attempt: 0, ct);

        // An answer is a fragment, not a finished section. The completeness rules ask whether
        // everything that had to be told was told, which of three sentences answering one
        // question is not a question that can be asked.
        FabricationReport report = Verify(pack, answer);
        List<string> bad = UnresolvableCitations(answer, retrieved);
        List<Fabrication> fatal = Fatal(report, bad);

        // More attempts, each told what was wrong with the last.
        //
        // The chronicle has retried since round 4 and the query layer did not, which made
        // withholding the first resort rather than the last. Most of what fires here is a slip
        // rather than a misunderstanding — a place name a letter out, a citation punctuated in
        // a way the engine could not read back — and a slip is what a second pass repairs.
        //
        // Two retries rather than the chronicle's one, and the difference is the disposal. A
        // chronicle that gives up prints a note where the section stood and keeps its other
        // fourteen; a query that gives up has failed the only thing it was asked. Both are
        // still bounded: a defect that survives two corrections is not a slip.
        for (int attempt = 1; attempt <= Retries && fatal.Count > 0; attempt++)
        {
            string retried = await WriteAsync(question, plan, pack, Correction(fatal), attempt, ct);
            FabricationReport again = Verify(pack, retried);
            List<string> badAgain = UnresolvableCitations(retried, retrieved);
            List<Fabrication> stillFatal = Fatal(again, badAgain);

            if (stillFatal.Count == 0)
            {
                return new QueryResult
                {
                    Question = question,
                    Plan = plan,
                    Retrieved = retrieved,
                    Answer = retried,
                    Fabrication = again,
                    BadCitations = badAgain,
                    Pack = pack,
                };
            }

            // Kept only where it is an improvement, so a retry can never trade a small defect
            // for a larger one — the same rule the chronicle applies to its own second pass.
            if (stillFatal.Count > fatal.Count) continue;

            answer = retried;
            report = again;
            bad = badAgain;
            fatal = stillFatal;
        }

        // A chronicle excludes a passage and prints a note where it stood; it has fifteen
        // sections and losing one is survivable. A query has one answer and nowhere to put a
        // warning, so prose carrying a known falsehood is not returned however hedged it is.
        if (fatal.Count > 0)
        {
            return Plain(question, plan, retrieved, Facts(retrieved)) with
            {
                Fabrication = report,
                BadCitations = bad,
                Pack = pack,
                Withheld = answer,
                Fatal = fatal,
            };
        }

        return new QueryResult
        {
            Question = question,
            Plan = plan,
            Retrieved = retrieved,
            Answer = answer,
            Fabrication = report,
            BadCitations = bad,
            Pack = pack,
        };
    }

    /// <summary>
    /// Everything checked about one answer: the fabrication rules over its prose, and the causal
    /// rule over its citations.
    ///
    /// The two see different text on purpose. The fabrication check is given prose with the
    /// citations stripped, because an id is not a word and reporting "e" as an invented name
    /// helps nobody; the causal check reads nothing but the citations. Both land in one coverage
    /// record, so the answer path reports as one checker rather than two.
    /// </summary>
    private FabricationReport Verify(ContextPack pack, string answer)
    {
        FabricationReport report = FabricationCheck.Check(pack, StripCitations(answer));

        List<Fabrication> extra = CausalLinks.Check(_view, pack, answer, report.Coverage);
        extra.AddRange(CausalLinks.Terminology(pack, StripCitations(answer), report.Coverage));

        if (extra.Count == 0) return report;

        foreach (Fabrication f in extra) report.Coverage.Fired(RuleNames.Of(f.Kind));

        return report with { Findings = [.. report.Findings, .. extra] };
    }

    /// <summary>
    /// One generation pass over a pack, with an optional correction from the last.
    ///
    /// <paramref name="attempt"/> moves the sampling seed, so a second pass is a second draw
    /// rather than the first one repeated. Without it the correction was being read by a model
    /// that then produced the same sentence anyway.
    /// </summary>
    private async Task<string> WriteAsync(
        string question, QueryPlan plan, ContextPack pack, string correction, int attempt,
        CancellationToken ct)
    {
        LlmResult result = await _client.CompleteAsync(new LlmRequest
        {
            System = AnswerSystem + (plan.Shape == QueryShape.Causal ? CausalHint : ""),
            Prompt = $"QUESTION: {question}\n\n{pack.Body}\n{correction}" +
                     "Answer the question now, citing record ids.",
            MaxTokens = 350,
            SeedOffset = attempt,

            // The first pass is greedy, as everything else in this project is. A retry is not:
            // greedy decoding re-derives the answer it was just told was wrong, whatever the
            // correction says. Low enough that the second answer is still the same answer,
            // high enough that a single stuck token is not fated to come back.
            TemperatureCentis = attempt == 0 ? null : RetryTemperatureCentis(attempt),
        }, ct);

        return Tidy(result.Text);
    }

    /// <summary>
    /// Warm enough to escape a stuck token, cool enough to stay the same answer, and warmer on
    /// the second retry because the first one plainly did not escape it.
    /// </summary>
    private static int RetryTemperatureCentis(int attempt) => 20 + (attempt * 25);

    /// <summary>How many second chances an answer gets before its prose is withheld.</summary>
    private const int Retries = 2;

    /// <summary>
    /// What was wrong with the last attempt, in the words of the findings themselves.
    ///
    /// Naming the offending span rather than restating the rule. Told only "do not invent
    /// names", a second pass rewrites the sentence it was already happy with and leaves the
    /// invented name where it was; told "you wrote Hdale", it fixes that.
    /// </summary>
    private static string Correction(List<Fabrication> fatal)
    {
        StringBuilder sb = new();
        sb.Append("\nYOUR LAST ANSWER WAS REJECTED. Fix exactly these and change nothing else:\n");

        foreach (Fabrication f in fatal.Take(6))
        {
            // Say what is wrong with the word, not merely where it was.
            //
            // The context alone is the sentence quoted back, and quoting a sentence to a model
            // that has just written it tells it nothing: handed "Hdale broke from the Kebarrow",
            // the second pass rewrote the rest of the sentence and left the misspelling where it
            // stood. A name is rejected for a reason the model can act on, so the reason is what
            // it is given.
            sb.Append("  - ").Append(f.Kind is "name" or "number"
                ? $"\"{f.Token}\" appears nowhere in the material above. Every name and number " +
                  $"must be copied from it letter for letter — check \"{f.Token}\" against the " +
                  "list of people, places and powers and write it exactly as it is spelled there"
                : f.Context).Append('\n');
        }

        sb.Append("Every name, number and record id must be copied exactly as it appears above.\n\n");
        return sb.ToString();
    }

    /// <summary>
    /// The findings that make an answer unreturnable: anything that makes it false, and any
    /// citation the engine cannot resolve back to a record it supplied.
    /// </summary>
    private static List<Fabrication> Fatal(FabricationReport report, List<string> bad)
    {
        List<Fabrication> fatal = [.. report.Blocking];

        foreach (string id in bad)
        {
            fatal.Add(new Fabrication(id, "unsupported-citation",
                $"the answer cites {id}, which is not one of the records it was given"));
        }

        return fatal;
    }

    /// <summary>An answer the engine wrote itself, with nothing for a checker to disagree with.</summary>
    private static QueryResult Plain(
        string question, QueryPlan plan, IReadOnlyList<EventId> retrieved, string answer) =>
        new()
        {
            Question = question,
            Plan = plan,
            Retrieved = retrieved,
            Answer = answer,
            Fabrication = new FabricationReport { Findings = [], CheckedTokens = 0, Coverage = new Coverage() },
            BadCitations = [],
        };

    /// <summary>
    /// The retrieved records stated plainly, for when the prose cannot be returned.
    ///
    /// The engine's own sentences, which are template-generated from the events and so cannot
    /// carry a fabrication. Less readable than the answer it replaces and true, which is the
    /// right way round: the alternative on offer is a fluent answer containing something the
    /// world does not contain.
    /// </summary>
    private string Facts(IReadOnlyList<EventId> retrieved)
    {
        StringBuilder sb = new();
        sb.Append("That cannot be answered in prose without asserting something the records do ")
          .Append("not support. What they hold is:");

        foreach (EventId id in retrieved)
        {
            if (!ContextPackBuilder.IsRenderable(_view.Log.Get(id))) continue;
            sb.Append("\n  in ").Append(_view.Log.Get(id).Year.ToString(CultureInfo.InvariantCulture))
              .Append(", ").Append(_view.Describe(id)).Append(" [").Append(id).Append(']');
        }

        return sb.ToString();
    }

    // ---- nothing found, and why -------------------------------------------

    /// <summary>
    /// Which of the four empty cases this is.
    ///
    /// Ordered from the most specific outwards. Secrecy is asked before absence, because a
    /// withheld record and a missing one look identical from the retrieved set — which is the
    /// whole reason the four collapsed into one answer in the first place.
    /// </summary>
    private EmptyReason WhyEmpty(QueryPlan plan)
    {
        if (plan.Entity.IsNone) return EmptyReason.NoSuchEntity;

        // Everything the question would have matched had nothing been hidden. This is the one
        // place in the query layer that reads past IsRetrievable, and it reads only the count:
        // no id, no description and no participant escapes this method.
        bool secretsMatched = false;

        foreach (EventId id in _view.Log.ForEntity(plan.Entity))
        {
            Event e = _view.Log.Get(id);
            if (e.Year < plan.FromYear || e.Year > plan.ToYear) continue;
            if (!MatchesTopic(e, plan.Topics)) continue;
            if (e.Scope == Visibility.Secret) secretsMatched = true;
        }

        if (secretsMatched) return EmptyReason.Withheld;

        // A power asked about before it existed. The question is well formed and the answer is
        // a date, not a denial — "who ruled the Sworn Men of Meigate in year 5" was answered
        // with silence when the house was founded in 19 and the founding is on the record.
        if (plan.ToYear != int.MaxValue && FirstYearOf(plan.Entity) is int born && plan.ToYear < born)
            return EmptyReason.OutsideLifetime;

        return EmptyReason.NoOccurrence;
    }

    /// <summary>The first year this entity appears in the record at all.</summary>
    private int? FirstYearOf(EntityId id)
    {
        foreach (EventId found in _view.Log.ForEntity(id))
        {
            Event e = _view.Log.Get(found);
            if (ContextPackBuilder.IsRetrievable(e)) return e.Year;
        }
        return null;
    }

    /// <summary>
    /// What to say when there is nothing, said about the world.
    ///
    /// No sentence here mentions a record, a log or a search. A reader asking about a place is
    /// owed a fact about the place; "the records do not cover that" describes a filing system,
    /// and describing the filing system is the failure the chronicle spent five rounds removing
    /// from its own prose.
    /// </summary>
    private string Nothing(QueryPlan plan, EmptyReason why)
    {
        string name = plan.Entity.IsNone ? Named(plan) : _view.State.NameOf(plan.Entity);

        switch (why)
        {
            case EmptyReason.NoSuchEntity:
                return $"There is no {name} in this world.";

            case EmptyReason.OutsideLifetime:
            {
                int born = FirstYearOf(plan.Entity) ?? 0;
                return $"{Capital(name)} did not exist until {born.ToString(CultureInfo.InvariantCulture)}.";
            }

            case EmptyReason.Withheld:
                // True, and it names nobody. The alternative was a denial, which is worse than
                // silence: it is a false statement about the world made in order to keep a
                // secret the world does in fact hold.
                return AsksWhoAttacked(plan)
                    ? "Whoever made that attempt was never found out."
                    : $"Whatever passed there was never made public.";

            default:
                return plan.ToYear != int.MaxValue
                    ? $"Nothing of that kind befell {name} in " +
                      $"{plan.ToYear.ToString(CultureInfo.InvariantCulture)}."
                    : $"Nothing of that kind is recorded of {name}.";
        }
    }

    /// <summary>The subject as the question wrote it, for a name that resolved to nothing.</summary>
    private static string Named(QueryPlan plan)
    {
        string subject = plan.Subject.Trim();
        if (subject.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) subject = subject[4..];
        return subject.Length == 0 ? "such thing" : subject;
    }

    private static string Capital(string name) =>
        name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    private static readonly string[] AttackWords =
        ["attempt", "kill", "murder", "assassinat", "attack", "poison"];

    private bool AsksWhoAttacked(QueryPlan plan) =>
        Mentions(plan.Question.ToLowerInvariant(), AttackWords);

    // ---- what the planner copied ------------------------------------------

    /// <summary>
    /// A year the planner emitted that the question does not contain, if there is one.
    ///
    /// The planner is told to set a year only where the question names a period, which makes
    /// every year it emits a claim about the question's own text — and the question's text is
    /// the one thing in this pipeline that cannot be mistyped. It got the subject wrong in three
    /// of sixteen on a field it was told to copy verbatim, and a subject at least fails loudly:
    /// it resolves to nothing and the question comes back empty.
    ///
    /// A year fails silently. There is no resolver to miss and no empty set to notice — a plan
    /// searching 41 instead of 51 runs perfectly and returns a confident answer about the wrong
    /// decade. So the query is refused rather than run. A miss is recoverable; a fluent answer
    /// to a question nobody asked is not.
    /// </summary>
    private static int? YearNotInQuestion(QueryPlan plan)
    {
        if (plan.FromYear == int.MinValue && plan.ToYear == int.MaxValue) return null;

        HashSet<int> written = NumbersIn(plan.Question);

        if (plan.FromYear != int.MinValue && !written.Contains(plan.FromYear)) return plan.FromYear;
        if (plan.ToYear != int.MaxValue && !written.Contains(plan.ToYear)) return plan.ToYear;
        return null;
    }

    /// <summary>
    /// The numbers a question actually writes, as whole runs of digits.
    ///
    /// Whole runs, because a substring test lets the 5 in a mistyped plan pass on the strength of
    /// the 51 in the question — which is the mistyping this is here to catch, admitted by the
    /// very laxity meant to catch it.
    /// </summary>
    private static HashSet<int> NumbersIn(string question)
    {
        HashSet<int> found = [];

        for (int i = 0; i < question.Length; i++)
        {
            if (!char.IsAsciiDigit(question[i])) continue;

            int start = i;
            while (i < question.Length && char.IsAsciiDigit(question[i])) i++;

            if (int.TryParse(question.AsSpan(start, i - start), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value))
            {
                found.Add(value);
            }
        }

        return found;
    }

    // ---- planning ---------------------------------------------------------

    private async Task<QueryPlan> PlanAsync(string question, CancellationToken ct)
    {
        LlmResult result = await _client.CompleteAsync(new LlmRequest
        {
            System = PlannerSystem,
            Prompt = $"QUESTION: {question}",
            Schema = QueryPlan.Schema,
            MaxTokens = 200,
        }, ct);

        // The question is attached before grounding, not alongside it. Ground falls back to
        // reading the subject out of the question when the planner's own copy of it does not
        // resolve, and an object initialiser that sets both at once hands it an empty question
        // — so the fallback existed, was tested, and never ran on the path the product takes.
        return Ground(Parse(result.Text, question) with { Question = question });
    }

    /// <summary>
    /// Fills in kinds the planner is likely to miss.
    ///
    /// The seat changes hands two ways — succession and open challenge — and asking only for
    /// successions returns a list of rulers with holes in it. This is the same double-path trap
    /// that produced a three-ruler count for a five-ruler house, arriving now through
    /// retrieval instead of through statistics; it is closed in the engine rather than trusted
    /// to the planner.
    /// </summary>
    private static List<string> Complete(IReadOnlyList<string> topics)
    {
        List<string> full = [.. topics];

        void Pair(string a, string b)
        {
            if (full.Contains(a) && !full.Contains(b)) full.Add(b);
        }

        Pair("POLITY.SUCCESSION", "POLITY.CHALLENGE");
        Pair("POLITY.CHALLENGE", "POLITY.SUCCESSION");
        Pair("POLITY.SECESSION", "POLITY.PARTITION");
        Pair("DIPLO.WAR_DECLARED", "DIPLO.PEACE_SIGNED");

        // A secession names the seat-holder of the power it creates, and is the third source of
        // rulers after successions and won challenges. Leaving it out of a ruler question drops
        // every founding holder — the Vea Lode list came back with five of its six, missing the
        // man who took the seat at the founding. The chronicle learned this at round 8 and the
        // retrieval layer had to learn it again.
        Pair("POLITY.SUCCESSION", "POLITY.SECESSION");
        Pair("POLITY.CHALLENGE", "POLITY.SECESSION");

        // A conspiracy is secret until it is uncovered, and the uncovering is the only public
        // record of it. Asking for the plots and matching only the plotting returns the secret
        // events, which are filtered, and therefore nothing.
        Pair("POLITY.COUP_PLOTTED", "POLITY.COUP_RESOLVED");

        return full;
    }

    private static QueryPlan Parse(string json, string fallbackSubject)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            List<string> topics = [];
            if (root.TryGetProperty("topics", out JsonElement t) && t.ValueKind == JsonValueKind.Array)
                foreach (JsonElement e in t.EnumerateArray())
                    if (e.GetString() is { Length: > 0 } s) topics.Add(s.ToUpperInvariant());

            int from = root.TryGetProperty("fromYear", out JsonElement f) && f.TryGetInt32(out int fv) && fv > 0
                ? fv : int.MinValue;
            int to = root.TryGetProperty("toYear", out JsonElement o) && o.TryGetInt32(out int ov) && ov > 0
                ? ov : int.MaxValue;

            return new QueryPlan
            {
                Shape = root.GetProperty("shape").GetString() == "causal" ? QueryShape.Causal : QueryShape.Factual,
                Subject = root.TryGetProperty("subject", out JsonElement s2) ? s2.GetString() ?? "" : "",
                Topics = topics,
                FromYear = from,
                ToYear = to,
            };
        }
        catch (JsonException)
        {
            // Schema-constrained output should make this impossible; if it happens, fall back
            // to a broad factual search rather than failing the question outright.
            return new QueryPlan { Shape = QueryShape.Factual, Subject = fallbackSubject };
        }
    }

    /// <summary>
    /// Matches a name from the question to an entity. Done by the engine against the actual
    /// world, never by the model — an id the model produced would be a guess, and a plausible
    /// wrong id is worse than no answer.
    /// </summary>
    /// <summary>A plan with its subject resolved against the world, and its topics completed.</summary>
    public QueryPlan Ground(QueryPlan plan) => plan with
    {
        Entity = Resolve(plan.Subject) is { IsNone: false } named
            ? named
            : ResolveFromQuestion(plan.Question),
        Topics = Complete(plan.Topics),
    };

    /// <summary>
    /// The entity the question names, found in the question itself.
    ///
    /// The planner is told to copy the subject from the question as written and does not always
    /// manage it: asked who ruled the Hadale Commune, it returned "Hade Commune", which resolved
    /// to nothing and made a well-formed question about a real power look like a question about
    /// a power that never existed.
    ///
    /// Fuzzy matching the planner's string would be the obvious repair and the wrong one — it
    /// trades a miss for the chance of resolving to the wrong power, which is worse. The
    /// question is right there and cannot be mistyped, so the names are matched against it.
    /// Longest first, because "the Hadale Commune" must win over "Hadale".
    /// </summary>
    private EntityId ResolveFromQuestion(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return EntityId.None;

        string lower = question.ToLowerInvariant();
        EntityId best = EntityId.None;
        int longest = 0;

        foreach (EntityId id in Candidates())
        {
            string name = _view.State.NameOf(id).ToLowerInvariant();
            if (name.StartsWith("the ", StringComparison.Ordinal)) name = name[4..];

            if (name.Length <= longest) continue;
            if (!lower.Contains(name, StringComparison.Ordinal)) continue;

            longest = name.Length;
            best = id;
        }

        return best;
    }

    private EntityId Resolve(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return EntityId.None;

        // A prefix, not a character set. TrimStart(char[]) strips any of 't', 'h', 'e' and
        // space from the front, so a subject the planner happened to lowercase — "the hadale
        // commune" — arrived as "adale commune" and matched nothing. The question then returned
        // nothing and looked like a world that did not contain the Hadale Commune.
        string needle = subject.Trim().ToLowerInvariant();
        if (needle.StartsWith("the ", StringComparison.Ordinal)) needle = needle[4..];
        EntityId best = EntityId.None;
        int bestScore = 0;

        foreach (EntityId id in Candidates())
        {
            string name = _view.State.NameOf(id).ToLowerInvariant();
            int score =
                name == needle ? 100 :
                name.Contains(needle, StringComparison.Ordinal) ? 60 :
                needle.Contains(name, StringComparison.Ordinal) ? 50 :
                SharesWord(name, needle) ? 30 : 0;

            if (score > bestScore) { bestScore = score; best = id; }
        }

        return bestScore >= 30 ? best : EntityId.None;
    }

    private IEnumerable<EntityId> Candidates()
    {
        foreach (Actor a in _view.State.Actors) yield return a.Id;
        foreach (Place p in _view.State.Places) yield return p.Id;
        foreach (Faction f in _view.State.Factions) yield return f.Id;
    }

    /// <summary>
    /// Words that describe a *kind* of polity rather than name one. Matching on these made
    /// "the Drelthorn League" — a faction that does not exist — resolve to the Wurn League on
    /// the strength of the word "league", and produce a confident answer about the wrong thing.
    /// A question about something that is not there must find nothing.
    /// </summary>
    private static readonly HashSet<string> GenericWords = ContextPackBuilder.GenericPolityWords;

    private static bool SharesWord(string name, string needle)
    {
        foreach (string word in needle.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length <= 3 || GenericWords.Contains(word)) continue;
            if (name.Contains(word, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    // ---- presuppositions --------------------------------------------------

    private static readonly string[] SeatWords =
        ["seat", "throne", "ruled", "rule of", "in power", "led the", "leadership", "reign"];

    private static readonly string[] ConquestWords = ["conquer", "conquered", "captured", "seized"];

    /// <summary>
    /// Checks what a question takes for granted before any answer is attempted.
    ///
    /// Deliberately targeted rather than general: it covers the two premises that actually
    /// arise — that a named person held a seat, and that a named power took a named place.
    /// A general presupposition engine is a research problem; these two are the ones the world
    /// can be asked about cheaply and definitively, and they are the ones that have bitten.
    /// </summary>
    private string? FalsePremiseIn(string question, QueryPlan plan)
    {
        string lower = question.ToLowerInvariant();

        if (plan.Entity.Kind == EntityKind.Actor && Mentions(lower, SeatWords))
        {
            if (!EverHeldASeat(plan.Entity))
            {
                return $"{_view.State.NameOf(plan.Entity)} never held a seat, so the question " +
                       "does not arise.";
            }
        }

        if (Mentions(lower, ConquestWords) && plan.Entity.Kind == EntityKind.Faction)
        {
            EntityId place = PlaceNamedIn(lower, plan.Entity);
            if (!place.IsNone && !EverTook(plan.Entity, place))
            {
                return $"{_view.State.NameOf(plan.Entity)} never took {_view.State.NameOf(place)}.";
            }
        }

        return null;
    }

    private static bool Mentions(string text, string[] words)
    {
        foreach (string w in words)
            if (text.Contains(w, StringComparison.Ordinal)) return true;
        return false;
    }

    private bool EverHeldASeat(EntityId actor)
    {
        foreach (EventId id in _view.Log.ForEntity(actor))
        {
            Event e = _view.Log.Get(id);
            bool tookIt = e.Kind == EventKind.PolitySuccession
                          || (e.Kind == EventKind.PolityChallenge && e.Outcome == Outcome.Succeeded)
                          || e.Kind is EventKind.PolitySecession or EventKind.PolityPartition;
            if (tookIt && e.Subject == actor) return true;
        }
        return false;
    }

    private bool EverTook(EntityId faction, EntityId place)
    {
        foreach (EventId id in _view.Log.ForEntity(faction))
        {
            Event e = _view.Log.Get(id);
            if (e.Kind == EventKind.ConflictConquest && e.Faction == faction && e.Where == place) return true;
        }
        return false;
    }

    /// <summary>
    /// A place named in the question that is not simply part of the subject's own name — the
    /// Kebarrow Compact is seated at Kebarrow, and matching that reported "the Kebarrow Compact
    /// never took Kebarrow" for a question about Griwick.
    /// </summary>
    private EntityId PlaceNamedIn(string lower, EntityId subject)
    {
        string subjectName = subject.IsNone ? "" : _view.State.NameOf(subject).ToLowerInvariant();

        foreach (Place p in _view.State.Places)
        {
            string name = p.Name.ToLowerInvariant();
            if (!lower.Contains(name, StringComparison.Ordinal)) continue;
            if (subjectName.Contains(name, StringComparison.Ordinal)) continue;
            return p.Id;
        }
        return EntityId.None;
    }

    // ---- retrieval --------------------------------------------------------

    /// <summary>
    /// The events a plan selects, as a pure function of the plan and the world.
    ///
    /// Public so the suite can score retrieval without a model in the loop. A wrong answer is
    /// usually a wrong retrieval, and a list of event lines shows that in a second where fluent
    /// prose hides it for a round — so this is the boundary worth testing directly.
    /// </summary>
    public List<EventId> Retrieve(QueryPlan plan)
    {
        // A question with no subject and a question with an unknown subject are different, and
        // conflating them answered "which powers were destroyed" with nothing.
        //
        // "Which powers broke away" names no one because it is about the world; "what happened
        // to the Drelthorn League" names someone who does not exist. The first should retrieve
        // by kind across the record, the second should retrieve nothing — and the difference is
        // whether the subject is a category or a name.
        if (plan.Entity.IsNone)
            return IsWorldScoped(plan.Subject) ? WorldWide(plan) : [];

        // "Who ruled X in year N" asks for a state, not for the events of that year.
        //
        // Read as a window it retrieves whatever happened in N and finds nothing, because a
        // seat changes hands rarely and almost never in the year asked about. Durnrin Drar took
        // the Hadale Commune in 47 and still held it in 51, and the question about 51 came back
        // empty — which reads exactly like a world in which nobody ruled it.
        if (AsksWhoHeldTheSeat(plan) && plan.ToYear != int.MaxValue)
            return SeatAt(plan.Entity, plan.ToYear);

        // The record, not the readable view.
        //
        // IsRetrievable rather than IsRenderable: secrecy still excludes, and bookkeeping no
        // longer does. The narratable view hides roughly a third of the record — the yearly
        // accounts — and that is where the economy's influence on everything else lives. A
        // question about a famine wants the harvest.
        List<EventId> matched = [];
        foreach (EventId id in _view.Log.ForEntity(plan.Entity))
        {
            Event e = _view.Log.Get(id);
            if (e.Year < plan.FromYear || e.Year > plan.ToYear) continue;
            if (!ContextPackBuilder.IsRetrievable(e)) continue;
            if (!MatchesTopic(e, plan.Topics)) continue;
            matched.Add(id);
        }

        if (matched.Count == 0) return matched;

        // A causal question wants the steps, not the list — but only for the *right* event.
        // Taking the latest significant record answered "why did they break away?" with the
        // ancestry of an unrelated murder thirty years later, then honestly reported that the
        // records did not say. The question itself picks the target.
        if (plan.Shape == QueryShape.Causal)
        {
            // Ranked over the narratable events only. A causal question is about something that
            // happened, and a bookkeeping row is never the thing being asked about even when it
            // is the reason for it — picking one as the target explains an accounting entry.
            List<EventId> narratable = [.. matched.Where(id => ContextPackBuilder.IsRenderable(_view.Log.Get(id)))];

            EventId target = BestMatch(narratable.Count > 0 ? narratable : matched, plan.Question);
            return ContextPackBuilder.Trace(_view, target, maxDepth: 16);
        }

        // Factual answers stay bounded — handing over the world is the thing this design
        // exists to avoid. But the cap must keep the *most relevant* events, not the most
        // recent: taking the tail answered "which factions broke away" with forty court
        // appointments and none of the secessions.
        const int cap = 40;
        if (matched.Count <= cap) return matched;

        List<(EventId Id, int Score)> ranked = [];
        foreach (EventId id in matched) ranked.Add((id, Relevance(id, plan.Question)));

        ranked.Sort(static (a, b) => b.Score != a.Score
            ? b.Score.CompareTo(a.Score)
            : a.Id.Value.CompareTo(b.Id.Value));

        List<EventId> best = [];
        for (int i = 0; i < cap && i < ranked.Count; i++) best.Add(ranked[i].Id);
        best.Sort(static (a, b) => a.Value.CompareTo(b.Value));
        return best;
    }

    /// <summary>A question about who held a power's seat, rather than about its events.</summary>
    private bool AsksWhoHeldTheSeat(QueryPlan plan) =>
        plan.Shape == QueryShape.Factual
        && plan.Entity.Kind == EntityKind.Faction
        && Mentions(plan.Question.ToLowerInvariant(), SeatWords);

    /// <summary>
    /// Who held a seat in a given year, as the events that put them there and the one that
    /// ended them if it has happened.
    ///
    /// The latest seat-taking at or before the year, not the events of the year. Successions,
    /// won challenges and the secession that founded the power all count — the three sources,
    /// the third of which the chronicle had to be taught twice.
    /// </summary>
    private List<EventId> SeatAt(EntityId faction, int year)
    {
        EventId took = EventId.None;

        foreach (EventId id in _view.Log.ForEntity(faction))
        {
            Event e = _view.Log.Get(id);
            if (e.Year > year || !ContextPackBuilder.IsRetrievable(e)) continue;

            bool establishes = e.Kind switch
            {
                EventKind.PolitySuccession => true,
                EventKind.PolitySecession => true,
                EventKind.PolityChallenge => e.Outcome == Outcome.Succeeded,
                _ => false,
            };

            if (establishes) took = id;
        }

        if (took.IsNone) return [];

        // The end of that tenure too, where the record has one, so an answer can say whether
        // they still held it. Without it the reader cannot tell "ruled in 51" from "ruled until
        // 48", and the events are three lines apart in the log.
        List<EventId> found = [took];

        foreach (EventId id in _view.Log.ForEntity(faction))
        {
            Event e = _view.Log.Get(id);
            if (e.Year <= _view.Log.Get(took).Year || e.Year > year) continue;
            if (!ContextPackBuilder.IsRetrievable(e)) continue;
            if (e.Kind is EventKind.PolityCollapse) found.Add(id);
        }

        return found;
    }

    /// <summary>
    /// Whether the subject names a category rather than a particular thing.
    ///
    /// Kept to a short list of the words this world's questions use for "all of them". A name
    /// that is not on it and does not resolve is an unknown name, and the right answer to a
    /// question about an unknown name is nothing at all — so guessing wide here would turn
    /// "what happened to the Drelthorn League" into a tour of the world.
    /// </summary>
    private static bool IsWorldScoped(string subject)
    {
        string s = subject.Trim().ToLowerInvariant();
        if (s.StartsWith("the ", StringComparison.Ordinal)) s = s[4..];

        return s.Length == 0 || WorldWords.Contains(s);
    }

    private static readonly HashSet<string> WorldWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "powers", "power", "factions", "faction", "houses", "realms", "polities",
        "places", "settlements", "everyone", "anyone", "all", "world", "them",
        "rulers", "people", "wars", "battles", "raids",
    };

    /// <summary>
    /// Retrieval for a question about the world rather than about anything in it.
    ///
    /// By kind, over the record, with no entity to hang on. The cap is the same one the
    /// entity-scoped path uses and for the same reason: handing over the world is the thing
    /// this design exists to avoid. Where a question of this shape overflows the cap, the
    /// answer needs a statistic rather than an enumeration, and that is the digest's job.
    /// </summary>
    private List<EventId> WorldWide(QueryPlan plan)
    {
        if (plan.Topics.Count == 0) return [];

        List<EventId> matched = [];

        foreach (Event e in _view.Log.Events)
        {
            if (e.Year < plan.FromYear || e.Year > plan.ToYear) continue;
            if (!ContextPackBuilder.IsRetrievable(e)) continue;
            if (!MatchesTopic(e, plan.Topics)) continue;
            matched.Add(e.Id);
        }

        return matched.Count <= WorldWideCap ? matched : [];
    }

    /// <summary>
    /// Above this a world-wide question is not an enumeration and must not pretend to be one.
    ///
    /// Returning the first forty of two hundred secessions would produce a confident, complete
    /// looking answer that is missing most of its subject — the failure mode this whole design
    /// is built against. Better to retrieve nothing and say so.
    /// </summary>
    private const int WorldWideCap = 60;

    /// <summary>
    /// The record the question is actually about, by word overlap with its description, broken
    /// towards the more consequential. Lexical and deterministic on purpose: asking the model
    /// which event to explain would mean trusting it to name an id, which is the one thing the
    /// retrieval design exists to avoid.
    /// </summary>
    /// <summary>
    /// How well one event answers the question, by word overlap with its description, with
    /// consequential events preferred. Lexical and deterministic — the same reason the causal
    /// target is chosen this way rather than by asking the model to name an id.
    /// </summary>
    private int Relevance(EventId id, string question)
    {
        Event e = _view.Log.Get(id);
        string text = (_view.Describe(id) + " " + EventKinds.Name(e.Kind)).ToLowerInvariant();

        int score = e.Significance >= Significance.Major ? 2 : 0;
        foreach (string word in question.ToLowerInvariant()
                     .Split([' ', '?', ',', '.', '\''], StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length < 4) continue;
            if (text.Contains(word, StringComparison.Ordinal)) score += 5;
        }
        return score;
    }

    private EventId BestMatch(List<EventId> candidates, string question)
    {
        string[] words = question.ToLowerInvariant()
            .Split([' ', '?', ',', '.', '\'', '"'], StringSplitOptions.RemoveEmptyEntries);

        EventId best = candidates[^1];
        int bestScore = int.MinValue;

        foreach (EventId id in candidates)
        {
            Event e = _view.Log.Get(id);
            string text = (_view.Describe(id) + " " + EventKinds.Name(e.Kind)).ToLowerInvariant();

            int score = e.Significance >= Significance.Major ? 3 : 0;
            foreach (string word in words)
            {
                if (word.Length < 4) continue;
                if (text.Contains(word, StringComparison.Ordinal)) score += 5;
            }

            if (score > bestScore) { bestScore = score; best = id; }
        }

        return best;
    }

    private static bool MatchesTopic(Event e, IReadOnlyList<string> topics)
    {
        if (topics.Count == 0) return true;

        string name = EventKinds.Name(e.Kind);
        foreach (string topic in topics)
            if (name.StartsWith(topic, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ---- checking ---------------------------------------------------------

    /// <summary>
    /// Citations pointing at records that were not retrieved. A confident answer citing an id
    /// the engine never supplied is the exact failure mode retrieval is meant to prevent, so it
    /// is measured rather than assumed away.
    /// </summary>
    private static List<string> UnresolvableCitations(string answer, List<EventId> retrieved)
    {
        HashSet<string> allowed = [];
        foreach (EventId id in retrieved) allowed.Add(id.ToString());

        List<string> bad = [];
        int at = 0;
        while ((at = answer.IndexOf("[e:", at, StringComparison.Ordinal)) >= 0)
        {
            int close = answer.IndexOf(']', at);
            if (close < 0) break;

            // One bracket may hold several ids — "[e:822, e:869, e:892]" — and reading the whole
            // group as a single citation reported a perfectly good answer as fabricated.
            //
            // Semicolons separate them too, which cost the ruler list its answer. Six correct
            // names with six correct ids came back as one unresolvable citation reading
            // "e:506; e:878; e:907; …", and the disposal rule then did exactly what it is for
            // and withheld a faultless answer. A separator the model chooses is not a fact
            // about the world, so every separator it plausibly chooses has to be read.
            foreach (string part in answer[(at + 1)..close]
                         .Split([',', ';'], StringSplitOptions.TrimEntries))
            {
                if (part.Length == 0) continue;
                if (!allowed.Contains(part) && !bad.Contains(part)) bad.Add(part);
            }
            at = close + 1;
        }
        return bad;
    }

    /// <summary>
    /// Cuts an answer at the point the model starts deliberating in the open.
    ///
    /// Asked a question whose answer was partly missing, it wrote "Wait, the summary says…
    /// I need to find the event for Paernmel Has" into the reply. Reasoning is not an answer,
    /// and everything after the first such marker is discarded rather than shown.
    /// </summary>
    private static string Tidy(string text)
    {
        string answer = Untagged(text.Trim());

        foreach (string marker in new[]
        {
            "  Wait,", "\nWait,", "Wait, the", "Let me ", "Let's ", "I need to ",
            "I should ", "Looking at the", "Actually,", "Hmm,",
        })
        {
            int at = answer.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (at > 0) answer = answer[..at];
        }

        return answer.Trim();
    }

    /// <summary>
    /// Strips the pack's own entity tags out of prose: "the Wurn League (f:1)" becomes "the Wurn
    /// League".
    ///
    /// Every name in the pack carries one so the model can tell two similar names apart, and the
    /// model copies them into the answer perhaps a third of the time. Told not to, it stops for
    /// a run and then does it again — and it is not a fabrication, so nothing fires and nothing
    /// retries. It is punctuation in the wrong document, and the engine can simply remove it.
    ///
    /// Square-bracket citations are left alone. Those are the one reference an answer is
    /// supposed to carry.
    /// </summary>
    private static string Untagged(string text)
    {
        StringBuilder sb = new(text.Length);

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '(' && Tag(text, i) is int close)
            {
                // The space before it goes too, or "League (f:1) was" leaves two spaces.
                if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
                i = close;
                continue;
            }

            sb.Append(text[i]);
        }

        return sb.ToString();
    }

    /// <summary>The index of the closing bracket where this opens an entity tag, or null.</summary>
    private static int? Tag(string text, int open)
    {
        int close = text.IndexOf(')', open);
        if (close < 0 || close - open is < 4 or > 8) return null;

        string inside = text[(open + 1)..close];
        if (inside.Length < 3 || inside[1] != ':') return null;
        if (!"apfer".Contains(char.ToLowerInvariant(inside[0]), StringComparison.Ordinal)) return null;

        foreach (char c in inside[2..])
            if (!char.IsAsciiDigit(c)) return null;

        return close;
    }

    /// <summary>Citations are ids, not prose — removed before the fabrication check sees them.</summary>
    private static string StripCitations(string answer)
    {
        StringBuilder sb = new(answer.Length);
        bool inCitation = false;

        foreach (char c in answer)
        {
            if (c == '[') inCitation = true;
            else if (c == ']') inCitation = false;
            else if (!inCitation) sb.Append(c);
        }
        return sb.ToString();
    }
}
