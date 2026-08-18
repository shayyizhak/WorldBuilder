using System.Globalization;
using System.Text;
using WorldBuilder.Core;
using WorldBuilder.Core.Analysis;
using WorldBuilder.Core.Geography;
using WorldBuilder.Core.Rendering;
using WorldBuilder.Core.Rules;
using WorldBuilder.Core.Serialization;
using WorldBuilder.Inference;

namespace WorldBuilder.Cli;

/// <summary>
/// The whole product surface for v0: run a world, then interrogate the log it produced.
/// Query commands deliberately load the <c>.jsonl</c> file rather than re-running the
/// simulation, so every invocation re-proves that the log is a complete record.
/// </summary>
public static class CommandLine
{
    private const string DefaultOutputDirectory = "out";

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            Args parsed = Args.Parse(args[1..]);
            return args[0] switch
            {
                "run" => CmdRun(parsed),
                "why" => CmdWhy(parsed),
                "what" => CmdWhat(parsed),
                "who" => CmdWho(parsed),
                "faction" => CmdFaction(parsed),
                "atlas" => CmdAtlas(parsed),
                "stats" => CmdStats(parsed),
                "audit" => CmdAudit(parsed),
                "plots" => CmdPlots(parsed),
                "outcomes" => CmdOutcomes(parsed),
                "chains" => CmdChains(parsed),
                "render" => CmdRender(parsed).GetAwaiter().GetResult(),
                "chronicle" => CmdChronicle(parsed).GetAwaiter().GetResult(),
                "book" => CmdBook(parsed).GetAwaiter().GetResult(),
                "ask" => CmdAsk(parsed).GetAwaiter().GetResult(),
                "suite" => CmdSuite(parsed).GetAwaiter().GetResult(),
                "log" => CmdLog(parsed),
                "verify" => CmdVerify(parsed),
                "test" => CmdTest(parsed),
                "map" => CmdMap(parsed),
                "bundle" => CmdBundle(parsed),
                "baseline" => CmdBaseline(parsed),
                "geometry" => CmdGeometry(parsed),
                "panel" => CmdPanel(parsed),
                "reference" => CmdReference(parsed),
                "seats" => CmdSeats(parsed),
                "schema" => CmdSchema(parsed),
                "holdouts" => CmdHoldouts(parsed),
                "floors" => CmdFloors(parsed),
                "ties" => CmdTies(parsed),
                "standing" => CmdStanding(parsed),
                "divergence" => CmdDivergence(parsed),
                "keyshift" => CmdKeyShift(parsed),
                "discriminate" => CmdDiscriminate(parsed),
                "warpanel" => CmdWarPanel(parsed),
                _ => Fail($"unknown command '{args[0]}'"),
            };
        }
        catch (Exception ex) when (ex is FileNotFoundException or FormatException or ArgumentException)
        {
            return Fail(ex.Message);
        }
        catch (BundleIntegrityException ex)
        {
            return Fail(ex.Message);
        }
        catch (LlmUnavailableException ex)
        {
            return Fail(ex.Message);
        }
    }

    // ---- run --------------------------------------------------------------

    private static int CmdRun(Args args)
    {
        ulong seed = args.ULong("seed", 42);
        int years = args.Int("years", 50);
        string directory = args.Text("out", DefaultOutputDirectory);
        bool verbose = args.Flag("verbose");

        Directory.CreateDirectory(directory);

        // A control replaces what the rules are told about distance with a synthetic value, so
        // the resulting world is a diagnostic artefact rather than a history. It is marked in the
        // header and in the genesis event, and `wb baseline cut` refuses it.
        ProximityControlKind control = ProximityControl.Parse(args.Text("control", ""));

        // The second kind of diagnostic run: a subset of the ruleset's termination rules, so an
        // effect can be attributed to a rule rather than to the three that shipped together.
        // Marked the same way and refused by `wb baseline cut` for the same reason.
        TerminationArm arm = TerminationArms.Parse(args.Text("arm", ""));

        Simulation sim = new(seed, control: control, arm: arm);
        sim.Run(years);

        string stem = Path.Combine(directory, $"world-{seed.ToString(CultureInfo.InvariantCulture)}");
        string jsonlPath = stem + ".jsonl";
        string logPath = stem + ".log";

        JsonlIo.Write(jsonlPath, sim.Log, seed, ProximityControl.NameOf(control),
            TerminationArms.NameOf(arm));

        if (control != ProximityControlKind.None)
        {
            Console.WriteLine($"CONTROL RUN — distances are synthetic ({ProximityControl.NameOf(control)}).");
            Console.WriteLine("  This world is a diagnostic artefact. It is not canon, it is not to be");
            Console.WriteLine("  sealed or rendered, and the header says so.");
        }

        if (arm != TerminationArm.All)
        {
            Console.WriteLine($"ARM RUN — only some relations can end ({TerminationArms.NameOf(arm)}).");
            Console.WriteLine("  This world ran under a subset of its own ruleset. It is a diagnostic");
            Console.WriteLine("  artefact, it is not canon, and the header says so.");
        }

        Significance minimum = verbose ? Significance.Bookkeeping : Significance.Minor;
        IReadOnlyList<string> lines = LogFormatter.Render(sim.Log, seed, minimum);
        File.WriteAllLines(logPath, lines);

        Console.WriteLine($"seed {seed}, {years} years, {sim.Log.Count} events");
        Console.WriteLine($"  {jsonlPath}");
        Console.WriteLine($"  {logPath}  ({lines.Count} lines)");

        if (args.Flag("print"))
        {
            Console.WriteLine();
            foreach (string line in lines) Console.WriteLine(line);
        }

        return 0;
    }

    // ---- the board --------------------------------------------------------

    /// <summary>
    /// The map artefact: import one, make one where there is nothing to import, or describe the
    /// one that is stored.
    ///
    /// All three are one-time acts, and none of them is reachable from the simulation. That is
    /// the whole shape of the decision §2 settled: a generator supplies a board once, the board
    /// becomes an artefact, and thereafter the artefact is read. A map generator is not
    /// reproducible across its own versions — the same finding as the model's run-to-run variance
    /// and answered the same way — so <c>world_seed</c> does not reproduce this file and nothing
    /// in <c>wb run</c> can rebuild it.
    /// </summary>
    private static int CmdMap(Args args)
    {
        string what = args.Positional(0) ?? "show";
        string to = args.Text("to", Boards.StoredPath);

        switch (what)
        {
            case "import":
            {
                string from = args.Positional(1)
                    ?? throw new ArgumentException("usage: wb map import <azgaar-export.json> [--to maps/board-1.wbmap.json]");

                if (!File.Exists(from)) return Fail($"no such export: {from}");

                Board imported = AzgaarImport.Parse(File.ReadAllText(from), $"azgaar export {Path.GetFileName(from)}");
                return Store(imported, to);
            }

            case "make":
            {
                // The fallback path, and it announces itself as one. A board made here is a real
                // board — cells, adjacency, costs, the same format — but it is not somebody
                // else's map, and a world should always be able to say which kind it ran against.
                Board made = BoardMaker.Make(
                    args.Int("width", BoardMaker.DefaultWidth),
                    args.Int("height", BoardMaker.DefaultHeight),
                    args.ULong("seed", 1));

                return Store(made, to);
            }

            case "show":
            {
                string path = args.Positional(1) ?? Boards.Locate();
                Board board = BoardIo.Read(path);

                Console.WriteLine($"{path}");
                Console.WriteLine($"  format     {board.Format}");
                Console.WriteLine($"  source     {board.Source}");
                Console.WriteLine($"  generator  {board.Generator}");
                Console.WriteLine($"  cells      {board.Count}");
                Console.WriteLine($"  sha256     {WorldBundle.HashOf(path)}");

                Dictionary<Terrain, int> terrain = [];
                foreach (BoardCell cell in board.Cells)
                    terrain[cell.Terrain] = terrain.GetValueOrDefault(cell.Terrain) + 1;

                foreach ((Terrain kind, int n) in terrain.OrderByDescending(static t => t.Value))
                    Console.WriteLine($"    {kind,-10} {n,4}  ({n * 100 / board.Count}%)");

                // The scale every rule is calibrated against, said out loud. A proximity of 100
                // means "this far apart", and a reader who cannot see the number cannot judge
                // whether a mechanic's distance term is doing anything.
                Console.WriteLine($"  median separation between land cells: {board.ReferenceCost} " +
                                  "(the cost at which proximity reads 100)");
                return 0;
            }

            default:
                return Fail($"unknown map command '{what}' — try import, make or show");
        }

        int Store(Board board, string path)
        {
            // Create-only, for the reason the baseline archive is: the property wanted is "this
            // cannot move by rerun", and a gate that depends on somebody remembering to be
            // careful is weaker than one that depends on the filesystem. A world's distances are
            // all downstream of this file.
            if (File.Exists(path))
            {
                Console.Error.WriteLine($"wb: {path} already exists.");
                Console.Error.WriteLine(
                    "    A stored board is create-only: every world simulated against it records " +
                    "its hash, and replacing it in place would leave those worlds naming a map " +
                    "that no longer exists. Move it aside under a new name first.");
                return 1;
            }

            BoardIo.Write(path, board);

            Console.WriteLine($"{path}");
            Console.WriteLine($"  {board.Count} cells, source: {board.Source}");
            Console.WriteLine($"  sha256 {WorldBundle.HashOf(path)}");
            return 0;
        }
    }

    /// <summary>
    /// A world and the artefacts it cannot be read without, written and verified as one thing.
    ///
    /// This is the Stage 3 carry-forward landing: the header's artefact hashes and render-cache
    /// fingerprint were deferred because nothing yet produced a bundle for them to describe. A
    /// map is that artefact — not derivable from the seed, decisive for every distance the
    /// simulation measures, and undetectably wrong if it is the wrong one.
    /// </summary>
    private static int CmdBundle(Args args)
    {
        string what = args.Positional(0) ?? "verify";
        ulong seed = args.ULong("seed", 42);
        string directory = args.Text("out", DefaultOutputDirectory);
        string worldPath = WorldBundle.WorldPath(directory, seed);

        if (!File.Exists(worldPath)) return Fail($"no world at '{worldPath}' — run `wb run --seed {seed}` first.");

        switch (what)
        {
            case "write":
            {
                (EventLog log, ulong storedSeed) = JsonlIo.Read(worldPath);

                List<string> artefacts = [];
                foreach (string name in new[] { WorldBundle.BoardName })
                    if (File.Exists(Path.Combine(directory, name))) artefacts.Add(name);

                string fingerprint = "";
                string renders = Path.Combine(directory, WorldBundle.RenderCacheName);
                if (File.Exists(renders)) fingerprint = new RenderStore(renders).Fingerprint();

                WorldBundle.Write(directory, log, storedSeed, artefacts, fingerprint);

                Console.WriteLine($"{worldPath}");
                foreach (string name in artefacts)
                    Console.WriteLine($"  {name}  {WorldBundle.HashOf(Path.Combine(directory, name))}");
                if (fingerprint.Length > 0) Console.WriteLine($"  renders  {fingerprint}  (cache fingerprint)");
                if (artefacts.Count == 0 && fingerprint.Length == 0)
                    Console.WriteLine("  no artefacts beside it; the header records none");
                return 0;
            }

            case "verify":
            {
                WorldHeader? header = JsonlIo.ReadHeader(worldPath);
                if (header is null) return Fail($"'{worldPath}' has no header, so it names no artefacts.");

                if (header.Artefacts.Count == 0 && header.RenderCacheFingerprint.Length == 0)
                {
                    Console.WriteLine($"{worldPath}: the header names no artefacts. Nothing to verify.");
                    return 0;
                }

                List<string> failures = WorldBundle.Verify(worldPath, header);

                foreach (StoredArtefact artefact in header.Artefacts)
                    Console.WriteLine($"  {artefact.Name}  {artefact.Sha256}");
                if (header.RenderCacheFingerprint.Length > 0)
                    Console.WriteLine($"  renders  {header.RenderCacheFingerprint}  (cache fingerprint)");

                foreach (string failure in failures) Console.WriteLine($"  FAIL {failure}");

                Console.WriteLine(failures.Count == 0
                    ? $"  {header.Artefacts.Count} artefact(s) match the header"
                    : $"  {failures.Count} mismatch(es)");

                return failures.Count == 0 ? 0 : 1;
            }

            default:
                return Fail($"unknown bundle command '{what}' — try write or verify");
        }
    }

    // ---- geometry ---------------------------------------------------------

    /// <summary>
    /// What distance actually decided, per seed, and what shape of world it had to work with.
    ///
    /// Three figures, and the third is the one that matters. How far apart a world's places are
    /// and what proximities its rules saw are context; the <b>discriminating share</b> — of the
    /// decisions distance had any room to move, the share where holding proximity flat changes
    /// the outcome — is the measurement. A mechanic can consult distance on every decision it
    /// makes and be changed by it on none.
    ///
    /// Simulated rather than loaded, so the panel does not depend on files existing, and against
    /// <c>--board</c> where a different geometry is being tried.
    /// </summary>
    private static int CmdGeometry(Args args)
    {
        string[] seeds = args.Text("seeds", "7,42,99,1234,2025")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        int years = args.Int("years", 50);
        string boardPath = args.Text("board", "");
        Board? board = boardPath.Length > 0 ? BoardIo.Read(boardPath) : null;

        Console.WriteLine(board is null
            ? $"the stored board — {Boards.Stored().Count} cells, median land separation {Boards.Stored().ReferenceCost}"
            : $"{boardPath} — {board.Count} cells, median land separation {board.ReferenceCost}");
        Console.WriteLine();

        // Every dispersion figure below carries its kind, and the header no longer has to explain
        // which one "spread" meant. Two figures written as a spread, meaning max − min, were
        // carried into a decision rule that read them as standard deviations; both readings of the
        // verdict aborted, so the conclusion survived, but it survived by luck.
        Console.WriteLine("  how far apart the places are, in the board's own cost units");
        Console.WriteLine($"    {"seed",-6} {"pairs",5} {"median",7}   " +
                          "separation range, and the coefficient of variation");

        List<(ulong Seed, GeographyProbe Probe, SeparationProfile Sep, List<int> Proximities)> panel = [];

        foreach (string raw in seeds)
        {
            if (!ulong.TryParse(raw.Trim(), out ulong seed)) continue;

            GeographyProbe probe = new();
            Simulation sim = new(seed, board: board) { Probe = probe };
            sim.Run(years);

            SeparationProfile sep = GeographyAudit.Separations(sim.State);

            // Realised proximities: what the world's places actually looked like to a rule,
            // rather than the range the board could theoretically produce.
            List<int> proximities = [];
            List<Place> sited = [.. sim.State.Places.Where(static p => p.IsSited)];
            for (int a = 0; a < sited.Count; a++)
                for (int b = a + 1; b < sited.Count; b++)
                    proximities.Add(sim.State.Geo!.BetweenPlaces(sited[a].Id, sited[b].Id));
            proximities.Sort();

            panel.Add((seed, probe, sep, proximities));

            Console.WriteLine($"    {seed,-6} {sep.Pairs,5} {sep.Median,7}   {sep.Range.Padded(30)} {sep.Cv}");
        }

        Console.WriteLine();
        Console.WriteLine("  the proximities those places actually presented");
        Console.WriteLine($"    {"seed",-6} {"median",7}   range");

        foreach ((ulong seed, _, _, List<int> proximities) in panel)
        {
            if (proximities.Count == 0) continue;
            Console.WriteLine($"    {seed,-6} {proximities[proximities.Count / 2],7}   " +
                              $"{Dispersion.Range(proximities)}");
        }

        Console.WriteLine();
        Console.WriteLine("  discriminating share — of the decisions distance could have moved, the share it did");
        Console.WriteLine($"    {"seed",-6} {"consulted",10} {"open",6} {"moved",6} {"share",7}   by mechanic");

        foreach ((ulong seed, GeographyProbe probe, _, _) in panel)
        {
            DiscriminationSummary all = probe.Overall();

            string detail = string.Join("  ", probe.Summarise()
                .Select(static s => $"{s.Mechanic} {s.SharePct}% ({s.Discriminated}/{s.Open})"));

            Console.WriteLine($"    {seed,-6} {all.Consulted,10} {all.Open,6} {all.Discriminated,6} " +
                              $"{all.SharePct,6}%   {detail}");
        }

        // The panel median of the discriminating share, which step 1's decision rule is stated
        // against. Printed rather than left to be worked out, because a rule that needs a
        // calculation done by hand is a rule that gets done differently twice.
        // Where a clamp downstream of the distance term can swallow it whole. Asked of the
        // mechanism rather than of a sample: "alliance moved 0 of 13" will not resolve at any
        // seed count worth spending, and this needs no sample at all.
        Console.WriteLine();
        Console.WriteLine("  absorbed — evaluations where no distance value in the world's realised");
        Console.WriteLine("  range could have changed the figure the rule actually uses");

        Dictionary<string, (int Total, int Absorbed)> pooled = new(StringComparer.Ordinal);

        foreach ((ulong seed, GeographyProbe probe, _, _) in panel)
        {
            foreach ((string mechanic, (int total, int absorbed)) in probe.Absorbed)
            {
                (int t, int a) = pooled.GetValueOrDefault(mechanic);
                pooled[mechanic] = (t + total, a + absorbed);
                Console.WriteLine($"    seed {seed,-6} {mechanic,-16} {absorbed}/{total}");
            }
        }

        foreach ((string mechanic, (int total, int absorbed)) in pooled)
        {
            Console.WriteLine($"    {"pooled",-11} {mechanic,-16} {absorbed}/{total}" +
                              (total == 0 ? "" : $"  ({absorbed * 100 / total}%)"));
        }

        List<int> shares = [.. panel.Select(static p => p.Probe.Overall().SharePct)];
        shares.Sort();

        Console.WriteLine();
        if (shares.Count > 0)
        {
            Console.WriteLine($"  panel median discriminating share: {shares[shares.Count / 2]}%  " +
                              $"(half of it is {shares[shares.Count / 2] / 2}%)");
        }

        List<(ulong Seed, Dispersion Cv, int Share)> ranked =
            [.. panel.Select(p => (p.Seed, p.Sep.Cv, p.Probe.Overall().SharePct))];

        Console.WriteLine("  rank on separation cv, lowest first: " +
            string.Join(" < ", ranked.OrderBy(static r => r.Cv.Figure).Select(static r => $"{r.Seed}({r.Cv})")));
        Console.WriteLine("  rank on discriminating share, lowest first: " +
            string.Join(" < ", ranked.OrderBy(static r => r.Share).Select(static r => $"{r.Seed}({r.Share}%)")));

        return 0;
    }

    // ---- the measurement panel --------------------------------------------

    /// <summary>
    /// The measurement panel: four arms, paired, on a large panel of seeds that nobody will ever
    /// read.
    ///
    /// <b>The reference panel is not the measurement panel.</b> Five seeds exist because hand
    /// verification is expensive — five worlds is about as much prose as a person will read
    /// against a record. A statistical comparison needs no hand verification at all: headless
    /// simulation, zero model calls, every figure computed here. Sizing the second by the cost of
    /// the first is how five seeds became a statistical claim, and it is why a result that could
    /// not survive its own control stood for two phases.
    ///
    /// Every world this produces is a control world in the sense that matters — a diagnostic
    /// artefact, never sealed, never rendered, never archived. Nothing is written to disk at all
    /// unless <c>--out</c> is given.
    /// </summary>
    private static int CmdPanel(Args args)
    {
        ulong from = args.ULong("from", 9_000_001);
        int count = args.Int("count", 207);
        int years = args.Int("years", 50);
        double mde = args.Int("mde", 5);

        // The reference seeds are excluded by construction, and asserted rather than assumed —
        // keeping the two panels separate is the entire point of the exercise.
        HashSet<ulong> reference = [7, 42, 99, 1234, 2025];

        Console.WriteLine($"measurement panel: {count} seeds from {from}, {years} years, four arms, paired");
        Console.WriteLine($"  each seed on its own board (wb map make at that seed, {BoardMaker.DefaultWidth}x{BoardMaker.DefaultHeight})");
        Console.WriteLine();

        (string Name, ProximityControlKind Kind)[] arms =
        [
            ("flat", ProximityControlKind.Flat),
            ("geography", ProximityControlKind.None),
            ("shuffle", ProximityControlKind.Shuffle),
            ("redraw", ProximityControlKind.Redraw),
        ];

        Dictionary<string, List<int>> variety = [];
        Dictionary<string, List<int>> repeats = [];
        foreach ((string name, _) in arms) { variety[name] = []; repeats[name] = []; }

        // The geography arm again, on one shared board, so board variance can be separated from
        // seed variance. Var(own board) = Var(seed) + Var(board); Var(shared board) = Var(seed).
        List<int> sharedBoardShare = [];
        List<int> ownBoardShare = [];
        Dictionary<string, List<int>> shareByMechanic = new(StringComparer.Ordinal);
        Board shared = Boards.Stored();

        long started = Environment.TickCount64;

        for (int i = 0; i < count; i++)
        {
            ulong seed = from + (ulong)i;
            if (reference.Contains(seed))
                return Fail($"seed {seed} is a reference seed; the measurement panel must not contain one.");

            Board board = BoardMaker.Make(BoardMaker.DefaultWidth, BoardMaker.DefaultHeight, seed);

            foreach ((string name, ProximityControlKind kind) in arms)
            {
                GeographyProbe? probe = name == "geography" ? new GeographyProbe() : null;

                Simulation sim = new(seed, board: board, control: kind) { Probe = probe };
                sim.Run(years);

                Audit audit = Audit.Compute(WorldView.Build(sim.Log, seed, board));
                variety[name].Add(audit.DistinctChainShapes);
                repeats[name].Add(audit.RepeatRatePct);

                if (probe is null) continue;

                ownBoardShare.Add(probe.Overall().SharePct);
                foreach (DiscriminationSummary s in probe.Summarise())
                {
                    if (!shareByMechanic.TryGetValue(s.Mechanic, out List<int>? shares))
                        shareByMechanic[s.Mechanic] = shares = [];
                    shares.Add(s.SharePct);
                }
            }

            // The same seed on the shared board, geography arm only.
            GeographyProbe onShared = new();
            Simulation control = new(seed, board: shared) { Probe = onShared };
            control.Run(years);
            sharedBoardShare.Add(onShared.Overall().SharePct);

            if ((i + 1) % 25 == 0)
                Console.Error.WriteLine($"  {i + 1}/{count} seeds, {(Environment.TickCount64 - started) / 1000}s");
        }

        Console.WriteLine($"  {count} seeds in {(Environment.TickCount64 - started) / 1000}s");
        Console.WriteLine();

        Console.WriteLine("  causal variety (distinct deep-chain shapes), per arm");
        foreach ((string name, _) in arms)
        {
            List<int> values = variety[name];
            Console.WriteLine($"    {name,-12} mean={values.Average(),7:0.00}   " +
                              $"{Dispersion.Sd(values).Padded(11)}   {Dispersion.Range(values)}");
        }

        Console.WriteLine();
        Console.WriteLine("  three pre-registered contrasts, Holm-corrected across the family");
        Console.WriteLine("  (docs/panel-prereg.md §6 — this family is fixed at three and is not extended)");

        List<Contrast> contrasts =
        [
            PairedStats.Compare("shuffle - redraw", variety["shuffle"], variety["redraw"]),
            PairedStats.Compare("geography - shuffle", variety["geography"], variety["shuffle"]),
            PairedStats.Compare("geography - redraw", variety["geography"], variety["redraw"]),
        ];

        foreach ((Contrast contrast, bool survives, double threshold) in PairedStats.Holm(contrasts))
        {
            Console.WriteLine($"    {contrast.Line()}");
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"      Holm threshold {threshold:0.0000} — {(survives ? "survives" : "does not survive")}; " +
                $"clears the {mde:0} point MDE: {(contrast.ClearsMde(mde) ? "yes" : "no")}"));
        }

        Contrast headline = contrasts[^1];
        Console.WriteLine();
        Console.WriteLine($"  realised paired dispersion on the headline contrast: {headline.Sd}  " +
                          "(estimated sd=16.48 from the reference panel)");

        // ---- flat − geography, registered separately ------------------------
        //
        // Registered in docs/panel-prereg.md §7 after the controls phase, where the arm means came
        // out flat 64.4 against geography 63.1 — geography below the no-distance arm. That contrast
        // was not pre-registered and was correctly reported as description only. It is registered
        // now, before the next run, so the next run tests it rather than mines it.
        //
        // <b>Its own family, not a fourth member of the original three.</b> Adding it to that Holm
        // family would raise every threshold in it and change the verdicts on three contrasts that
        // have already been reported — re-analysing a settled result by enlarging its family after
        // the fact, which is the same move as re-analysing seen data with a newly chosen variance.
        // One contrast, α = 0.05, no correction to apply.
        Contrast flatMinusGeography =
            PairedStats.Compare("flat - geography", variety["flat"], variety["geography"]);

        Console.WriteLine();
        Console.WriteLine("  flat - geography — registered separately, its own family of one (α = 0.05)");
        Console.WriteLine($"    {flatMinusGeography.Line()}");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"      α 0.05, no Holm correction — {(flatMinusGeography.P <= 0.05 ? "significant" : "not significant")}; " +
            $"clears the {mde:0} point MDE: {(flatMinusGeography.ClearsMde(mde) ? "yes" : "no")}"));
        Console.WriteLine("      not in the three-contrast family above: enlarging that family after its");
        Console.WriteLine("      results were reported would move thresholds under a settled verdict.");

        // The repeat rate, on the same arms. Reported rather than tested: the MDE was set for
        // causal variety and inventing a second one here after the fact would be fitting.
        Console.WriteLine();
        Console.WriteLine("  verbatim repeat rate, per arm (reported, not tested — no MDE was set for it)");
        foreach ((string name, _) in arms)
            Console.WriteLine($"    {name,-12} mean={repeats[name].Average(),6:0.00}%   {Dispersion.Sd(repeats[name])}");

        // Board geometry, as a by-product of the panel rather than as a phase of its own.
        Console.WriteLine();
        Console.WriteLine("  board geometry — discriminating share, own board against shared board");
        Console.WriteLine($"    own board    mean={ownBoardShare.Average(),6:0.00}%   {Dispersion.Sd(ownBoardShare)}");
        Console.WriteLine($"    shared board mean={sharedBoardShare.Average(),6:0.00}%   {Dispersion.Sd(sharedBoardShare)}");

        Dispersion ownVar = Dispersion.Variance(ownBoardShare);
        Dispersion seedVar = Dispersion.Variance(sharedBoardShare);
        double boardVar = ownVar.Figure - seedVar.Figure;

        Console.WriteLine($"    from the seed alone {seedVar}; from seed and board together {ownVar}; " +
                          $"the difference {Dispersion.Variance(boardVar)}  " +
                          $"({(boardVar <= 0 ? "board adds nothing measurable" : $"board {Dispersion.Sd(Math.Sqrt(boardVar))}")})");

        Console.WriteLine();
        Console.WriteLine("  discriminating share per mechanic, across the panel's boards");
        List<string> mechanics = [.. shareByMechanic.Keys];
        mechanics.Sort(StringComparer.Ordinal);
        foreach (string mechanic in mechanics)
        {
            List<int> shares = shareByMechanic[mechanic];
            Console.WriteLine($"    {mechanic,-18} n={shares.Count,4}  mean={shares.Average(),6:0.00}%   " +
                              $"{Dispersion.Sd(shares)}");
        }

        if (args.Text("out", "") is { Length: > 0 } outDir)
        {
            Directory.CreateDirectory(outDir);
            StringBuilder tsv = new();
            tsv.Append("seed\tflat\tgeography\tshuffle\tredraw\n");
            for (int i = 0; i < count; i++)
            {
                tsv.Append((from + (ulong)i).ToString(CultureInfo.InvariantCulture));
                foreach ((string name, _) in arms)
                    tsv.Append('\t').Append(variety[name][i].ToString(CultureInfo.InvariantCulture));
                tsv.Append('\n');
            }

            string path = Path.Combine(outDir, "panel-variety.tsv");
            File.WriteAllText(path, tsv.ToString());
            Console.WriteLine();
            Console.WriteLine($"  per-seed figures: {path}");
        }

        return 0;
    }

    // ---- the reference set ------------------------------------------------

    /// <summary>
    /// Stages the materials a human needs to establish a new world's reference facts, and stops.
    ///
    /// <b>It prepares the reading. It does not perform it.</b> Nothing this writes is verified,
    /// every page says so, and nothing it writes may enter the test suite as a fixture. That is not
    /// caution — §4's rule is that a wrong engine figure is worse than a wrong model figure because
    /// nothing questions it, and a machine-derived facts sheet that lost its banner would be exactly
    /// that with a decade of shelf life.
    ///
    /// <b>Why a new set rather than a re-check.</b> Seed 42 at ruleset 4 is a different history, not
    /// a stale one: positions are assigned at worldgen and four mechanics consume distance, so the
    /// stream is consumed differently from the first year. §9's facts are about a world that no
    /// longer exists and there is nothing to diff. What is worth spending human attention on is the
    /// irreducible set the suite actually depends on, and nothing more.
    ///
    /// Zero inference: no client is constructed and no render is read.
    /// </summary>
    private static int CmdReference(Args args)
    {
        WorldView view = Load(args);

        string outDir = args.Text("to", Path.Combine(DefaultOutputDirectory, "carry-forward", "reference-set"));

        Console.WriteLine($"seed {view.Seed}, ruleset {Ruleset.Version} — " +
                          $"{view.Log.Count} records, years {view.FirstYear}–{view.LastYear}" +
                          (view.State.HasBoard ? ", on a board" : ", no board"));
        Console.WriteLine();

        foreach (string path in ReferenceSet.Write(view, outDir)) Console.WriteLine($"  {path}");

        Console.WriteLine();
        Console.WriteLine("  Machine-derived and unverified, all of it. Nothing above is ground truth and");
        Console.WriteLine("  nothing above may be used as a fixture. It exists to make the reading cheaper.");
        return 0;
    }

    // ---- the pre-verification instruments ---------------------------------

    /// <summary>
    /// What the seat-moving records say before a ruler list is made of them.
    ///
    /// The derivation's own output cannot answer the question it is asked here. "No duplicate in
    /// the list" holds both when a contested transfer is collapsed correctly and when a genuine
    /// second tenure is deleted, so the raw moves have to be read beside it.
    /// </summary>
    private static int CmdSeats(Args args)
    {
        WorldView view = Load(args);

        Console.WriteLine($"seed {view.Seed}, ruleset {Ruleset.Version} — " +
                          $"{view.Log.Count} records, years {view.FirstYear}–{view.LastYear}");
        Console.WriteLine();

        List<SeatRepeat> repeats = SeatTransfers.Repeats(view);
        int contested = 0, second = 0, unclassified = 0;

        foreach (SeatRepeat repeat in repeats)
        {
            Console.WriteLine($"  {repeat.Describe(view.State)}");

            switch (repeat.Shape)
            {
                case SeatRepeatShape.ContestedTransfer: contested++; break;
                case SeatRepeatShape.SecondTenure: second++; break;
                default: unclassified++; break;
            }
        }

        if (repeats.Count == 0) Console.WriteLine("  no person appears twice on any seat");

        Console.WriteLine();
        Console.WriteLine($"  {contested} contested transfer(s), {second} second tenure(s), " +
                          $"{unclassified} fitting neither shape");

        if (args.Flag("lists"))
        {
            Console.WriteLine();
            foreach (Faction f in view.State.Factions)
            {
                List<SeatSpell> history = ReferenceSet.SeatHistory(view, f.Id);
                if (history.Count == 0) continue;

                Console.WriteLine($"  {f.Name}: " + string.Join("; ", history.Select(h =>
                    $"{view.State.NameOf(h.Ruler)} {h.From}–" +
                    $"{(h.Ended == "still holding" ? "" : h.To.ToString(CultureInfo.InvariantCulture))} ({h.Ended})")));
            }
        }

        // A case fitting neither shape is escalated rather than given a rule of its own.
        return unclassified == 0 ? 0 : 1;
    }

    /// <summary>
    /// Every field name a consumer reads, against every field name the emitter writes.
    ///
    /// A verifier that reads a name the engine does not write cannot fail. The vocabulary comes
    /// off real records rather than off a declared table, because the rules decide what is written
    /// and a table beside them is a second thing to keep in step.
    /// </summary>
    private static int CmdSchema(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string set = args.Text("set", "ruleset-4");

        List<EventLog> logs = [];
        foreach (ulong seed in Seeds(args))
        {
            string n = seed.ToString(CultureInfo.InvariantCulture);
            string path = Path.Combine(root, set, $"seed-{n}", $"world-{n}.jsonl");
            if (!File.Exists(path)) return Fail($"no record at {path}");

            (EventLog log, ulong _) = JsonlIo.Read(path);
            logs.Add(log);
        }

        Dictionary<EventKind, SortedSet<string>> vocabulary = EventSchema.Emitted(logs);

        foreach (string line in EventSchema.Render(vocabulary)) Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine($"{EventSchema.Anywhere(vocabulary).Count} distinct field name(s) across " +
                          $"{vocabulary.Count} event kind(s), from {logs.Count} record(s) of {set}.");

        if (!args.Flag("reads"))
        {
            Console.WriteLine();
            Console.WriteLine("Add --reads to run the consumers and resolve what they asked for against this.");
            return 0;
        }

        // The sweep runs one world. Every kind's vocabulary is still the panel's, so a read on a
        // kind this seed happens not to contain is resolved against what the other four wrote.
        ulong sweepSeed = args.ULong("seed", 42);
        string directory = Path.Combine(root, set,
            $"seed-{sweepSeed.ToString(CultureInfo.InvariantCulture)}");

        (EventLog world, ulong swept) = JsonlIo.Read(Path.Combine(directory,
            $"world-{sweepSeed.ToString(CultureInfo.InvariantCulture)}.jsonl"));

        List<SchemaRead> rows = EventSchema.Resolve(
            SchemaSweep.Run(WorldView.Build(world, swept), directory), vocabulary);

        List<SchemaRead> dead = [.. rows.Where(static r => r.DeadRead)];
        List<SchemaRead> offKind = [.. rows.Where(static r => r.OffKind)];

        Console.WriteLine();
        Console.WriteLine($"## Reads, swept on {set} seed {swept}");
        Console.WriteLine();
        Console.WriteLine($"{rows.Count} (kind, field) read(s); {dead.Count} dead, {offKind.Count} off-kind.");
        Console.WriteLine();

        // Dead reads fail. An off-kind read is reported and not judged: walking every event and
        // asking each for `deaths` is a legitimate shape, and asserting on it manufactures false
        // positives, which is how a blanket coverage rule once cost seven true sections.
        foreach (SchemaRead row in dead)
            Console.WriteLine($"  DEAD  {EventKinds.Name(row.Kind)}.{row.Field}");

        foreach (SchemaRead row in offKind)
            Console.WriteLine($"  off   {EventKinds.Name(row.Kind)}.{row.Field}");

        if (args.Flag("verbose"))
            foreach (SchemaRead row in rows.Where(static r => r.EmittedOnThisKind))
                Console.WriteLine($"  ok    {EventKinds.Name(row.Kind)}.{row.Field}");

        Console.WriteLine();
        Console.WriteLine(dead.Count == 0
            ? "No consumer read a name the emitter writes nowhere."
            : $"{dead.Count} dead read(s). A verifier that reads a name the engine does not write cannot fail.");

        return dead.Count == 0 ? 0 : 1;
    }

    /// <summary>
    /// Where the checker's holdouts land, grouped by rule, across a whole baseline set.
    ///
    /// Reads the stored sidecars. Recomputing them would run today's rules over yesterday's prose,
    /// which gives the same figure on both sides of the comparison and lets a rule that has since
    /// gone quiet agree with the bug it exists to expose.
    /// </summary>
    /// <summary>
    /// Which rules FLOOR can protect, measured across a baseline set rather than reasoned about.
    ///
    /// <b>Why this is a command and not a paragraph.</b> The project document carried a hand-written
    /// list of the rules lacking floor protection. Measured, it was wrong in both directions —
    /// naming two rules that are protected in some scopes and missing one that is protected in
    /// none. A stale list drifts one way; a list wrong in both directions was never measured at
    /// all, it was reasoned out from which rules seemed as though they ought to be uninstrumented.
    ///
    /// So the list is emitted from the sidecars the checker wrote, and the document carries the
    /// output with the command that produced it. Same standing rule as the schema sweep, and the
    /// same reason: a declared artefact describing a measurable property of the code is a second
    /// copy of that property, kept in step by hand.
    /// </summary>
    private static int CmdFloors(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string set = args.Text("set", $"ruleset-{Ruleset.Version}");

        List<IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>>> panel = [];
        List<ulong> read = [];

        foreach (ulong seed in Seeds(args))
        {
            string sidecar = Path.Combine(root, set, $"seed-{seed.ToString(CultureInfo.InvariantCulture)}",
                $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}.findings.json");

            if (!File.Exists(sidecar)) continue;

            panel.Add(FindingsSidecar.ReadCoverage(sidecar));
            read.Add(seed);
        }

        // A sweep that silently read nothing reports the same empty table as a sweep that read
        // everything and found nothing to say. The whole brief this came from is about that.
        if (panel.Count == 0)
        {
            Console.Error.WriteLine($"wb: no sidecars under {Path.Combine(root, set)} — nothing measured");
            return 1;
        }

        List<FloorReason> rows = GoldenDiff.FloorClassification(panel);

        Console.WriteLine($"floor coverage — {set}, {panel.Count} seed(s): " +
                          string.Join(", ", read));
        Console.WriteLine();
        Console.WriteLine("| rule | scopes with a floor | of | reason |");
        Console.WriteLine("|---|---|---|---|");

        int unreachable = 0;
        foreach (FloorReason r in rows)
        {
            Console.WriteLine($"| `{r.Rule}` | {r.WithFloor} | {r.Scopes} | {r.Reason} |");
            if (r.Reason == "no floor on this panel") unreachable++;
        }

        Console.WriteLine();
        Console.WriteLine($"{unreachable} rule(s) have no floor anywhere on this panel: FLOOR cannot " +
                          "fail for them, on any seed, under the current counter scheme.");

        string? to = args.Text("to", "") is { Length: > 0 } path ? path : null;
        if (to is not null)
        {
            string? dir = Path.GetDirectoryName(to);
            if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);

            List<string> lines =
            [
                $"| rule | scopes with a floor | of | reason |",
                "|---|---|---|---|",
                .. rows.Select(r => $"| `{r.Rule}` | {r.WithFloor} | {r.Scopes} | {r.Reason} |"),
            ];
            File.WriteAllLines(to, lines);
            Console.Error.WriteLine($"wb: written to {to}");
        }

        return 0;
    }

    /// <summary>
    /// What the relation graph does over the life of a world: how many ties of each kind were
    /// made, how many ended, and where the live count peaked against where it finished.
    ///
    /// <b>Folded from the record, never from a re-run.</b> That is what lets it read a sealed
    /// baseline of an older ruleset and produce the "before" column for a change that has since
    /// happened — the engine that wrote those files no longer exists to be asked.
    ///
    /// Exits non-zero on the step-two brief's §4 degeneracy guard: a fully connected trade graph
    /// carries no information and neither does an empty one, so a final count near zero or near
    /// peak stops the loop on the exit code rather than on somebody reading the table.
    /// </summary>
    private static int CmdTies(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string set = args.Text("set", "");
        RelationKind guarded = Enum.Parse<RelationKind>(args.Text("kind", "Trade"), ignoreCase: true);

        List<(ulong Seed, RelationTrajectory.Report Report)> panel = [];

        // Either a whole baseline set, or one world file — the second is how a run that has not
        // been sealed yet gets measured, which is every run before the cut.
        if (set.Length > 0)
        {
            foreach (ulong seed in Seeds(args))
            {
                string n = seed.ToString(CultureInfo.InvariantCulture);
                string path = Path.Combine(root, set, $"seed-{n}", $"world-{n}.jsonl");
                if (!File.Exists(path)) return Fail($"no record at {path}");

                (EventLog log, ulong _) = JsonlIo.Read(path);
                panel.Add((seed, RelationTrajectory.Of(log, seed)));
            }
        }
        else
        {
            WorldView view = Load(args);
            panel.Add((view.Seed, RelationTrajectory.Of(view.Log, view.Seed)));
        }

        bool degenerate = false;
        int pooledPeak = 0, pooledFinal = 0, pooledEnded = 0;

        foreach ((ulong seed, RelationTrajectory.Report report) in panel)
        {
            foreach (string line in RelationTrajectory.Render(report, seed)) Console.WriteLine(line);
            Console.WriteLine();

            if (report.Of(guarded) is { } watched)
            {
                pooledPeak += watched.Peak;
                pooledFinal += watched.Final;
                pooledEnded += watched.Ended;

                // Reported per seed, asserted pooled. At a peak of three the non-degenerate range
                // is two values wide and one tie decides the verdict, which tests the granularity
                // rather than the rule.
                Console.WriteLine($"  {guarded}: {RelationTrajectory.Degeneracy(watched)}" +
                    (watched.Peak <= 20 ? "  (coarse — reported, not asserted)" : ""));

                // The same question against the pairs that actually existed. A world down to one
                // house cannot hold a trade tie, and calling that "the rule emptied the graph"
                // measures hegemony rather than the rule.
                Console.WriteLine($"  {guarded}: " +
                    RelationTrajectory.DensityOf(watched, report.AvailablePairs));
                Console.WriteLine($"  houses standing: " +
                    $"peak {report.PeakAvailablePairs} pair(s), final {report.FinalAvailablePairs} pair(s)");

                List<(string Cause, int Count)> causes = report.Causes(guarded);
                if (causes.Count > 0)
                {
                    Console.WriteLine($"  ended by: " +
                        string.Join(", ", causes.Select(c => $"{c.Cause} {c.Count}")));
                }

                if (args.Flag("trajectory"))
                {
                    Console.WriteLine("  live by year: " + string.Join(" ", watched.Live));
                }
            }

            Console.WriteLine($"  first termination: " +
                (report.FirstTerminationYear is { } y
                    ? $"year {y.ToString(CultureInfo.InvariantCulture)} at {report.FirstTerminationAt}"
                    : "never"));
            Console.WriteLine();
        }

        // Pooled, because a per-seed cause table is too small to read a distribution off and a
        // per-seed degeneracy band at a peak of three is two values wide.
        {
            string verdict = RelationTrajectory.Degeneracy(pooledPeak, pooledFinal, pooledEnded,
                $"`{guarded}` across the panel");
            degenerate = verdict.StartsWith("DEGENERATE", StringComparison.Ordinal);

            Console.WriteLine($"panel — {guarded}: {verdict}  " +
                              $"(peaks summed {pooledPeak}, finals summed {pooledFinal}, " +
                              $"{pooledEnded} ended)");
            Console.WriteLine();

            Dictionary<string, int> pooled = new(StringComparer.Ordinal);
            foreach ((ulong _, RelationTrajectory.Report report) in panel)
                foreach ((string cause, int n) in report.Causes(guarded))
                    pooled[cause] = pooled.GetValueOrDefault(cause) + n;

            Console.WriteLine($"panel — {guarded} terminations by cause:");
            foreach ((string cause, int n) in pooled.OrderByDescending(static kv => kv.Value)
                         .ThenBy(static kv => kv.Key, StringComparer.Ordinal))
            {
                Console.WriteLine($"  {cause,-28} {n}");
            }

            // A cause field with one reachable value across the panel is a field that renders as
            // a universal. Step one found `tie` in exactly that state and the standing rule came
            // out of it: it does not get rendered until it has two.
            Console.WriteLine();
            Console.WriteLine(pooled.Count switch
            {
                0 => "No termination of this kind anywhere on the panel.",
                1 => $"ONE reachable value ({pooled.Keys.First()}). Record it; do not render it.",
                _ => $"{pooled.Count} reachable values. The cause is a real distinction on this panel.",
            });
        }

        return degenerate ? 1 : 0;
    }

    /// <summary>
    /// Every piece of standing state, and whether anything on the panel ever made it smaller.
    ///
    /// <b>Half of the monotonic sweep, and it says which half.</b> Counts can separate "came down"
    /// from "never came down". They cannot separate "has no removal path" from "has one nothing
    /// reached", and this project has already paid for treating a whole quiet panel as proof of
    /// the former. The other half is read off the rules by a person and put beside this.
    /// </summary>
    private static int CmdStanding(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string set = args.Text("set", "");

        List<(EventLog Log, ulong Seed)> panel = [];

        if (set.Length > 0)
        {
            foreach (ulong seed in Seeds(args))
            {
                string n = seed.ToString(CultureInfo.InvariantCulture);
                string path = Path.Combine(root, set, $"seed-{n}", $"world-{n}.jsonl");
                if (!File.Exists(path)) return Fail($"no record at {path}");

                (EventLog log, ulong _) = JsonlIo.Read(path);
                panel.Add((log, seed));
            }
        }
        else
        {
            string directory = args.Text("from", DefaultOutputDirectory);
            foreach (ulong seed in Seeds(args))
            {
                string n = seed.ToString(CultureInfo.InvariantCulture);
                string path = Path.Combine(directory, $"world-{n}.jsonl");
                if (!File.Exists(path)) return Fail($"no record at {path}");

                (EventLog log, ulong _) = JsonlIo.Read(path);
                panel.Add((log, seed));
            }
        }

        List<StandingItem> rows = StandingState.Sweep(panel);
        IReadOnlyList<string> lines = StandingState.Render(rows, panel.Count);

        foreach (string line in lines) Console.WriteLine(line);

        if (args.Text("to", "") is { Length: > 0 } to)
        {
            string? dir = Path.GetDirectoryName(to);
            if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
            File.WriteAllLines(to, lines);
            Console.Error.WriteLine($"wb: written to {to}");
        }

        // Reported, never repaired here. Fixing an unbounded list is how a bounded phase stops
        // being bounded, so this exits 0 whatever it finds.
        return 0;
    }

    /// <summary>
    /// Where a fresh run stops matching a sealed baseline of the previous ruleset, against where
    /// the change that was meant to separate them first acted.
    ///
    /// Exits non-zero when a seed diverges *before* its first termination, which is the step-two
    /// brief's §5 halt: it means something other than the intended change moved the world.
    /// </summary>
    private static int CmdDivergence(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string against = args.Text("against", "ruleset-5");
        int years = args.Int("years", 50);

        bool halt = false;

        foreach (ulong seed in Seeds(args))
        {
            string n = seed.ToString(CultureInfo.InvariantCulture);
            string path = Path.Combine(root, against, $"seed-{n}", $"world-{n}.jsonl");
            if (!File.Exists(path)) return Fail($"no record at {path}");

            (EventLog older, ulong archived) = JsonlIo.Read(path);
            if (archived != seed) return Fail($"{path} carries seed {archived}, not {seed}");

            Simulation sim = new(seed);
            sim.Run(years);

            Divergence.Report report = Divergence.Between(older, sim.Log, seed);

            Console.WriteLine($"seed {n}: {report.OldCount} → {report.NewCount} events");
            Console.WriteLine($"  {report.Verdict}");
            Console.WriteLine($"  first difference in content rather than in ids: " +
                (report.FirstContentDifference < 0
                    ? "none"
                    : $"index {report.FirstContentDifference.ToString(CultureInfo.InvariantCulture)}"));
            Console.WriteLine();

            halt |= !report.Holds;
        }

        return halt ? 1 : 0;
    }

    /// <summary>
    /// What one inserted event costs the render cache, measured rather than reasoned about.
    ///
    /// <c>Event.Key</c> is documented as existing so a cached render survives a shift in log
    /// position. It survives a retcon and it does not survive an insertion, because the key takes
    /// the emission's sequence within its year — so inserting one event rekeys every later event
    /// in that year, and a pack keyed on those events loses its identity without its content
    /// having moved. Sized here while it is cheap; the fix belongs to Stage 7, which is where it
    /// starts to bite.
    /// </summary>
    private static int CmdKeyShift(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string set = args.Text("set", $"ruleset-{Ruleset.Version}");

        int rekeyed = 0, changed = 0, unexplained = 0;

        foreach (ulong seed in Seeds(args))
        {
            string n = seed.ToString(CultureInfo.InvariantCulture);
            string path = Path.Combine(root, set, $"seed-{n}", $"world-{n}.jsonl");
            if (!File.Exists(path)) return Fail($"no record at {path}");

            (EventLog log, ulong _) = JsonlIo.Read(path);
            KeyBlastRadius.Report report = KeyBlastRadius.Measure(log, seed);

            Console.WriteLine("  " + report.Line);
            rekeyed += report.Rekeyed;
            changed += report.Changed;
            unexplained += report.Unexplained;
        }

        Console.WriteLine();
        Console.WriteLine(unexplained > 0
            ? $"{unexplained} event(s) before the insertion point already fail to reproduce their " +
              "stored key. This measurement's model of the key is wrong; the figures above are void."
            : $"**{rekeyed} cached render(s) lose their key for every {changed} whose content " +
              "changes** — one inserted event, per seed, in the middle of the middle year.");

        return unexplained > 0 ? 1 : 0;
    }

    /// <summary>
    /// One arm per termination rule, so an effect can be attributed to a rule instead of to the
    /// three that shipped together.
    ///
    /// <b>The null arm is the check on the harness, not a result.</b> A rule that never fires on a
    /// seed must produce that seed's previous-ruleset log exactly; if it does not, the switch
    /// itself moved the world and every other arm's figure is void. Same reasoning as the identity
    /// proximity control, which exists to prove the substitution machinery consumes nothing from
    /// the streams the rules draw on.
    ///
    /// Exits non-zero when a null arm fails to reproduce, because that is a broken instrument
    /// rather than a finding.
    /// </summary>
    private static int CmdDiscriminate(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string against = args.Text("against", "ruleset-5");
        int years = args.Int("years", 50);

        (string Label, TerminationArm Arm)[] arms =
        [
            ("none (= ruleset 5)", TerminationArm.None),
            ("war only", TerminationArm.War),
            ("collapse only", TerminationArm.Collapse),
            ("disuse only", TerminationArm.Disuse),
            ("all (= ruleset 6)", TerminationArm.All),
        ];

        bool broken = false;

        foreach (ulong seed in Seeds(args))
        {
            string n = seed.ToString(CultureInfo.InvariantCulture);
            string path = Path.Combine(root, against, $"seed-{n}", $"world-{n}.jsonl");
            if (!File.Exists(path)) return Fail($"no record at {path}");

            (EventLog baseline, ulong archived) = JsonlIo.Read(path);
            if (archived != seed) return Fail($"{path} carries seed {archived}, not {seed}");

            Console.WriteLine($"## seed {n}");
            Console.WriteLine();
            // Pacts, alliances and wars are here because they are the two channels a `Trade` edge
            // actually feeds, plus the thing the war rule is attached to. `ProposeAlliance` scores
            // an approach partly as `Trade / 2`, and `TradePact` refuses outright above a value of
            // 40 — so ending a tie both lowers an appeal and *reopens* a gate. Counting them is
            // the difference between a measured channel and two figures that happen to move
            // together.
            Console.WriteLine("| arm | events | deep chains | distinct shapes | ties ended | " +
                              "pacts made | alliances made | wars | 70% held from | reproduces the baseline |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");

            foreach ((string label, TerminationArm arm) in arms)
            {
                Simulation sim = new(seed, arm: arm);
                sim.Run(years);

                Audit audit = Audit.Compute(WorldView.Build(sim.Log, seed));
                RelationTrajectory.Report ties = RelationTrajectory.Of(sim.Log, seed);

                // Compared against the log with the arm marker taken back off. The marker is on
                // the genesis event on purpose — an arm world carried away from its file would
                // otherwise look exactly like a history — but it is a fact about the run rather
                // than about the world, and left in it diverges every arm at index 0, which is
                // the instrument reporting its own label as a finding.
                Divergence.Report divergence = Divergence.Between(
                    baseline, Divergence.WithoutArmMarker(sim.Log), seed);

                bool identical = divergence.FirstDifference < 0
                                 && baseline.Count == sim.Log.Count;

                int ended = 0;
                foreach (KindTrajectory k in ties.Kinds)
                    if (k.Kind == RelationKind.Trade) ended = k.Ended;

                int allied = 0, pacts = 0, wars = 0;
                foreach (Event e in sim.Log.Events)
                {
                    if (e.Kind == EventKind.DiploAllianceFormed && e.Outcome == Outcome.Succeeded) allied++;
                    if (e.Kind == EventKind.EconomyTradePact) pacts++;
                    if (e.Kind == EventKind.DiploWarDeclared) wars++;
                }

                Console.WriteLine($"| {label} | {sim.Log.Count} | {audit.DeepChains} | " +
                                  $"{audit.DistinctChainShapes} | {ended} | {pacts} | {allied} | " +
                                  $"{wars} | {Runaway(sim.Log, seed)} | {(identical ? "**yes**" : "no")} |");

                // The arm that cannot fire has to reproduce, or nothing else here means anything.
                if (arm != TerminationArm.None && arm != TerminationArm.All && ended == 0 && !identical)
                {
                    Console.Error.WriteLine(
                        $"wb: the '{label}' arm ended no tie on seed {n} and still did not reproduce " +
                        "the baseline. The switch moved the world; every arm above is void.");
                    broken = true;
                }

                if (arm == TerminationArm.None && !identical)
                {
                    Console.Error.WriteLine(
                        $"wb: the null arm does not reproduce the baseline on seed {n} — " +
                        $"first difference at index {divergence.FirstDifference}. " +
                        "Ruleset 6 changed something outside the three termination rules.");
                    broken = true;
                }
            }

            Console.WriteLine();
        }

        return broken ? 1 : 0;
    }

    /// <summary>
    /// The first year one house held 70% of the settled population, or "never" — the runaway
    /// figure <c>wb stats</c> reports, recomputed here so an arm can be read without writing files.
    ///
    /// Evaluated after every event rather than at a year boundary, so it names the year the
    /// threshold was first crossed. Sampling once a year reported it one year late.
    /// </summary>
    private static string Runaway(EventLog log, ulong seed)
    {
        int hit = 0;

        Replay.Walk(log, seed, (state, e) =>
        {
            if (hit != 0) return;

            int total = 0, most = 0;
            foreach (Place p in state.Places)
                if (!p.Controller.IsNone) total += p.Population;

            foreach (Faction f in state.Factions)
            {
                int mine = 0;
                foreach (Place p in state.HoldingsOf(f.Id)) mine += p.Population;
                most = Math.Max(most, mine);
            }

            if (total > 0 && most * 100 / total >= 70) hit = e.Year;
        });

        return hit == 0 ? "never" : $"Y{hit.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// The war-rule experiment. Four arms, paired on the same seeds and boards, log-only.
    ///
    /// <c>--dry-run</c> runs the reference seeds and reports whether each arm of each decision rule
    /// is reachable, and what σ the paired contrast has. Both are pre-registration inputs and both
    /// are computed before the panel is touched.
    /// </summary>
    private static int CmdWarPanel(Args args)
    {
        ulong from = args.ULong("from", 9_100_001);
        int count = args.Int("count", 90);
        int years = args.Int("years", 50);
        double mde = args.Int("mde", 5);
        bool dryRun = args.Flag("dry-run");

        List<ulong> seeds = [];
        List<Board> boards = [];

        if (dryRun)
        {
            // The reference seeds, on the stored board, because the dry run's job is to show the
            // decision rules are reachable on data that already exists — not to spend the panel.
            foreach (ulong seed in Seeds(args)) { seeds.Add(seed); boards.Add(Boards.Stored()); }
        }
        else
        {
            HashSet<ulong> reference = [7, 42, 99, 1234, 2025];
            for (int i = 0; i < count; i++)
            {
                ulong seed = from + (ulong)i;
                if (reference.Contains(seed))
                    return Fail($"seed {seed} is a reference seed; the panel must not contain one.");

                seeds.Add(seed);
                boards.Add(BoardMaker.Make(BoardMaker.DefaultWidth, BoardMaker.DefaultHeight, seed));
            }
        }

        Console.WriteLine(dryRun
            ? $"DRY RUN — {seeds.Count} reference seed(s), four arms, on the stored board"
            : $"war-rule panel: {seeds.Count} seeds from {from}, {years} years, four arms, paired");
        Console.WriteLine($"  each seed on {(dryRun ? "the stored board" : "its own board")}; " +
                          "every figure folded from logs, no inference");
        Console.WriteLine();

        Dictionary<string, List<int>> runaway = [];
        Dictionary<string, List<int>> shapes = [];
        foreach (string arm in WarRulePanel.Arms) { runaway[arm] = []; shapes[arm] = []; }

        int censoredNull = 0, unmatched = 0;
        List<string> mismatches = [];
        long started = Environment.TickCount64;

        for (int i = 0; i < seeds.Count; i++)
        {
            foreach (ArmResult r in WarRulePanel.RunSeed(seeds[i], boards[i], years))
            {
                runaway[r.Arm].Add(WarRulePanel.Censor(r.RunawayYear, years + 1));
                shapes[r.Arm].Add(r.Shapes);

                if (r.Arm == WarRulePanel.Null && r.Censored) censoredNull++;
                if (r.ScheduleMatched) continue;

                unmatched++;
                mismatches.Add($"seed {r.Seed.ToString(CultureInfo.InvariantCulture)}");
            }

            if (!dryRun && (i + 1) % 10 == 0)
                Console.Error.WriteLine($"  {i + 1}/{seeds.Count} seeds, {(Environment.TickCount64 - started) / 1000}s");
        }

        Console.WriteLine($"  {seeds.Count} seed(s) in {(Environment.TickCount64 - started) / 1000}s");
        Console.WriteLine();

        // The random arm has to have delivered its treatment, or the contrast that names it is
        // measuring something else. A halt condition, not a footnote.
        if (unmatched > 0)
        {
            Console.Error.WriteLine(
                $"wb: the random arm could not match the war arm's removals on {unmatched} world(s): " +
                string.Join(", ", mismatches.Take(8)) + ". The arms are not matched and the " +
                "war − random contrast is void.");
            return 1;
        }

        Console.WriteLine("  runaway year (first year one house holds 70% of settled population)");
        foreach (string arm in WarRulePanel.Arms)
        {
            List<int> values = runaway[arm];
            Console.WriteLine($"    {arm,-10} mean={values.Average(),6:0.00}   " +
                              $"{Dispersion.Sd(values).Padded(11)}   {Dispersion.Range(values)}");
        }

        Console.WriteLine();
        Console.WriteLine($"  degeneracy guard: {WarRulePanel.Degeneracy(runaway[WarRulePanel.Null], censoredNull)}");
        Console.WriteLine();

        List<Contrast> contrasts =
        [
            PairedStats.Compare("war - null", runaway[WarRulePanel.War], runaway[WarRulePanel.Null]),
            PairedStats.Compare("war - random", runaway[WarRulePanel.War], runaway[WarRulePanel.Random]),
            PairedStats.Compare("collapse - null", runaway[WarRulePanel.Collapse], runaway[WarRulePanel.Null]),
        ];

        Console.WriteLine("  three pre-registered contrasts, Holm-corrected across the family");
        Console.WriteLine("  (the family is fixed at three and is not extended)");

        Dictionary<string, (bool Survives, bool Clears)> verdicts = new(StringComparer.Ordinal);

        foreach ((Contrast contrast, bool survives, double threshold) in PairedStats.Holm(contrasts))
        {
            Console.WriteLine($"    {contrast.Line()}");
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"      Holm threshold {threshold:0.0000} — {(survives ? "survives" : "does not survive")}; " +
                $"clears the {mde:0} year MDE: {(contrast.ClearsMde(mde) ? "yes" : "no")}"));

            verdicts[contrast.Name] = (survives, contrast.ClearsMde(mde));
        }

        Console.WriteLine();
        Console.WriteLine("  secondary, reported and not adjudicated");
        foreach (string arm in WarRulePanel.Arms)
        {
            Console.WriteLine($"    distinct shapes, {arm,-10} mean={shapes[arm].Average(),7:0.00}   " +
                              $"{Dispersion.Sd(shapes[arm]).Padded(11)}");
        }

        Console.WriteLine();
        Console.WriteLine("  pre-committed disposition");
        Console.WriteLine($"    {Disposition(verdicts)}");

        if (dryRun)
        {
            Console.WriteLine();
            Console.WriteLine("  decision-rule reachability — every arm shown producible");
            Console.WriteLine("  (a rule with an arm no input can reach has been shipped twice here:");
            Console.WriteLine("   a criterion the population never met, and a statistic that could not vary)");

            HashSet<string> cells = new(StringComparer.Ordinal);
            foreach (bool warNullReal in new[] { true, false })
                foreach (bool warRandomReal in new[] { true, false })
                {
                    string cell = Disposition(new Dictionary<string, (bool, bool)>(StringComparer.Ordinal)
                    {
                        ["war - null"] = (warNullReal, warNullReal),
                        ["war - random"] = (warRandomReal, warRandomReal),
                    });

                    cells.Add(cell);
                    Console.WriteLine($"    war-null {(warNullReal ? "real " : "null "),-5} " +
                                      $"war-random {(warRandomReal ? "real " : "null "),-5} → " +
                                      cell[..cell.IndexOf('.')]);
                }

            Console.WriteLine($"    {cells.Count} distinct cell(s) reachable of 3 in the brief's table");

            Console.WriteLine();
            Console.WriteLine("  degeneracy-guard reachability");
            // Each arm needs an input that reaches *it*, not merely an input that fails. A panel
            // of identical censored years trips the spread rule first, so it demonstrates the
            // spread arm twice and the censoring arm never — which is how an unreachable arm
            // hides inside a reachability check.
            Console.WriteLine($"    flat panel        → {WarRulePanel.Degeneracy([30, 30, 30, 30, 30], 0)}");
            Console.WriteLine($"    mostly censored   → {WarRulePanel.Degeneracy([52, 52, 52, 22, 30], 3)}");
            Console.WriteLine($"    this dry run      → {WarRulePanel.Degeneracy(runaway[WarRulePanel.Null], censoredNull)}");

            Console.WriteLine();
            Console.WriteLine("  statistic reachability, on these five seeds");
            Console.WriteLine($"    primary varies      {(Dispersion.Sd(runaway[WarRulePanel.Null]).Figure > 0 ? "yes" : "NO")}");
            Console.WriteLine($"    censoring exercised {(censoredNull > 0 ? $"yes, {censoredNull} world(s)" : "NO")}");

            // A contrast whose deltas are identically zero on the dry-run seeds is not thereby
            // structurally zero — but it is the shape that has to be watched, so it is named here
            // rather than discovered in the panel's output.
            foreach (Contrast c in contrasts)
            {
                if (c.Sd.Figure > 0) continue;
                Console.WriteLine($"    NOTE  '{c.Name}' has zero variance on these seeds. Not " +
                                  "structurally zero — the collapse arm can move a world — but the");
                Console.WriteLine("          panel must show it varying, or the contrast reports nothing.");
            }

            Console.WriteLine();
            Console.WriteLine("  sizing, from the paired variance of these seeds (sizing only — this");
            Console.WriteLine("  figure does not decide anything about the run it came from)");

            Contrast warNull = contrasts[0];
            double sigma = warNull.Sd.Figure;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"    paired sd = {sigma:0.00} years on war - null"));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"    N at that sd, MDE {mde:0}, 80% power, two-sided 0.05 = " +
                $"{PairedStats.RequiredN(sigma, mde)}"));

            // The point estimate of σ from five observations is itself very uncertain, so the
            // registered N takes the margin in advance rather than regretting it afterwards —
            // the same move docs/panel-prereg.md §4 made, and for the same reason.
            double upper = Math.Sqrt(4 * sigma * sigma / 1.6488);
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"    one-sided 80% upper bound on sd = {upper:0.00}; N there = " +
                $"{PairedStats.RequiredN(upper, mde)}"));
        }

        return 0;
    }

    /// <summary>
    /// The disposition table from the brief, evaluated rather than read off by a person.
    ///
    /// Every cell is reachable and the dry run demonstrates it: a decision rule with an arm no
    /// input can reach is the shape this project has now been bitten by twice.
    /// </summary>
    private static string Disposition(IReadOnlyDictionary<string, (bool Survives, bool Clears)> verdicts)
    {
        (bool warNullSurvives, bool warNullClears) = verdicts["war - null"];
        (bool warRandomSurvives, bool warRandomClears) = verdicts["war - random"];

        bool warNullReal = warNullSurvives && warNullClears;
        bool warRandomReal = warRandomSurvives && warRandomClears;

        if (!warNullReal)
        {
            return "war - null is not distinguishable: the five-seed panel caught a tail. " +
                   "SHIP ALL THREE.";
        }

        return warRandomReal
            ? "war - null real and war - random real: the war rule is the cause. " +
              "SHIP COLLAPSE + DISUSE, CARD THE WAR RULE."
            : "war - null real and war ≈ random: the world is knife-edge on losing trade ties. " +
              "SHIP COLLAPSE + DISUSE; the brake problem is world design, not a rule defect.";
    }

    private static int CmdHoldouts(Args args)
    {
        string root = args.Text("baselines", "baselines");
        string set = args.Text("set", "ruleset-4");
        string against = args.Text("against", "ruleset-3");

        Holdouts.Report report = Holdouts.Build(root, set, against, Seeds(args));

        foreach (string line in Holdouts.Render(report)) Console.WriteLine(line);

        string? to = args.Text("to", "") is { Length: > 0 } path ? path : null;
        if (to is not null)
        {
            string? dir = Path.GetDirectoryName(to);
            if (dir is { Length: > 0 }) Directory.CreateDirectory(dir);
            File.WriteAllLines(to, Holdouts.Render(report));
            Console.Error.WriteLine($"wb: written to {to}");
        }

        // Non-zero where the phase's halt conditions are met, so the loop stops on the exit code
        // rather than on somebody reading the table.
        return report.Halts ? 1 : 0;
    }

    /// <summary>The five-seed panel, or whatever <c>--seeds</c> names instead.</summary>
    private static List<ulong> Seeds(Args args)
    {
        List<ulong> seeds = [];

        foreach (string raw in args.Text("seeds", "7,42,99,1234,2025")
                     .Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (ulong.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed))
                seeds.Add(seed);
        }

        return seeds;
    }

    // ---- baselines --------------------------------------------------------

    /// <summary>
    /// Cutting and checking a sealed golden baseline.
    ///
    /// Create-only, and it never touches an existing one. The v1 baseline was assembled by hand
    /// across three rounds, two of which aborted correctly; what those aborts found is why this
    /// exists as a command rather than as a procedure somebody follows.
    /// </summary>
    private static int CmdBaseline(Args args)
    {
        string what = args.Positional(0) ?? "check";

        switch (what)
        {
            case "cut":
            {
                ulong seed = args.ULong("seed", 42);
                string from = args.Text("from", DefaultOutputDirectory);
                string to = args.Text("to", "")
                    is { Length: > 0 } given ? given : throw new ArgumentException(
                        "usage: wb baseline cut --seed 42 --from <dir> --to baselines/<set>/seed-42");

                BaselineCut cut = BaselineArchive.Cut(new BaselineRequest
                {
                    Seed = seed,
                    From = from,
                    To = to,
                    Id = args.Text("id", $"{Path.GetFileName(Path.GetDirectoryName(to.TrimEnd('/', '\\')) ?? "baseline")}-seed-{seed}"),
                    RepositoryRoot = RepositoryRoot(),
                    Verification = args.Text("verification", "stability-anchor-only"),
                    Model = args.Text("model", LlmOptions.Default.Model),
                    ModelDigest = args.Text("model-digest", ""),
                    Notes = args.Text("note", "") is { Length: > 0 } note ? [note] : [],
                });

                Console.WriteLine(cut.Directory);
                Console.WriteLine($"  engine {cut.Header.EngineVersion} @ {cut.Header.EngineCommit}");
                Console.WriteLine($"  ruleset {cut.Header.RulesetVersion}");
                Console.WriteLine($"  checker fingerprint {cut.CheckerFingerprint}");
                foreach (BaselineArtefact a in cut.Artefacts)
                    Console.WriteLine($"    {a.Filename,-34} {a.Sha256[..16]}  {a.Bytes,9} bytes  {a.Role}");
                Console.WriteLine($"  sealed {cut.Seal}");
                return 0;
            }

            case "check":
            {
                string directory = args.Positional(1) ?? args.Text("dir", "");
                if (directory.Length == 0) return Fail("usage: wb baseline check <dir>");

                List<string> failures = BaselineArchive.Check(directory);
                foreach (string failure in failures) Console.WriteLine($"  FAIL {failure}");

                Console.WriteLine(failures.Count == 0
                    ? $"{directory}: the seal verifies and every artefact matches its manifest hash"
                    : $"{directory}: {failures.Count} failure(s)");

                return failures.Count == 0 ? 0 : 1;
            }

            default:
                return Fail($"unknown baseline command '{what}' — try cut or check");
        }
    }

    /// <summary>The repository root, found by the directory git keeps its own records in.</summary>
    private static string RepositoryRoot()
    {
        for (DirectoryInfo? at = new(Directory.GetCurrentDirectory()); at is not null; at = at.Parent)
            if (Directory.Exists(Path.Combine(at.FullName, ".git"))) return at.FullName;

        throw new DirectoryNotFoundException(
            "no git repository above the working directory. The checker fingerprint hashes what " +
            "git stores, so a baseline cannot be cut outside one.");
    }

    // ---- queries ----------------------------------------------------------

    private static int CmdWhy(Args args)
    {
        WorldView view = Load(args);
        if (!EventId.TryParse(args.Positional(0), out EventId id))
            return Fail("usage: wb why e:1188 [--seed 42]");

        foreach (string line in CausalTrace.Why(view, id, args.Int("depth", 8)))
            Console.WriteLine(line);
        return 0;
    }

    private static int CmdWhat(Args args)
    {
        WorldView view = Load(args);
        if (!EventId.TryParse(args.Positional(0), out EventId id))
            return Fail("usage: wb what e:1188 [--seed 42]");

        foreach (string line in CausalTrace.What(view, id, args.Int("depth", 4)))
            Console.WriteLine(line);
        return 0;
    }

    private static int CmdWho(Args args)
    {
        WorldView view = Load(args);
        if (!EntityId.TryParse(args.Positional(0), out EntityId id) || id.Kind != EntityKind.Actor)
            return Fail("usage: wb who a:112 [--seed 42]");

        foreach (string line in Dossier.Actor(view, id)) Console.WriteLine(line);
        return 0;
    }

    private static int CmdFaction(Args args)
    {
        WorldView view = Load(args);
        if (!EntityId.TryParse(args.Positional(0), out EntityId id) || id.Kind != EntityKind.Faction)
            return Fail("usage: wb faction f:2 [--seed 42]");

        foreach (string line in Dossier.Faction(view, id)) Console.WriteLine(line);
        return 0;
    }

    private static int CmdAtlas(Args args)
    {
        foreach (string line in Dossier.Atlas(Load(args))) Console.WriteLine(line);
        return 0;
    }

    private static int CmdStats(Args args)
    {
        WorldView view = Load(args);
        foreach (string line in WorldStats.Compute(view).Report(view)) Console.WriteLine(line);
        return 0;
    }

    /// <summary>
    /// Every conspiracy accounted for: examined, or skipped with a named reason.
    ///
    /// Diagnostic and written beside the run rather than into it. The event log records what
    /// happened in the world; this records what happened in the engine, and a log carrying its
    /// own instrumentation would make the world's record depend on how it was being watched.
    /// </summary>
    private static int CmdPlots(Args args)
    {
        string[] seeds = args.Text("seeds", "7,42,99,1234,2025")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        int years = args.Int("years", 50);
        int unbalanced = 0;

        foreach (string raw in seeds)
        {
            if (!ulong.TryParse(raw.Trim(), out ulong seed)) continue;

            PlotLedger ledger = new();
            Simulation sim = new(seed) { Ledger = ledger };
            sim.Run(years);

            PlotAccounting account = ledger.Account(sim.Log);
            if (!account.Balances) unbalanced++;

            Console.WriteLine($"seed {seed}");
            foreach (string line in account.Report()) Console.WriteLine(line);

            // When the resolver first reached anything, and what it reached. The pre-Y35 question
            // is answered by these two numbers rather than by reading the log.
            List<PlotStanding> examined = [.. ledger.Plots.Where(p => p.Examined > 0)];
            Console.WriteLine(examined.Count == 0
                ? "      no plot was ever examined"
                : $"      first examined in year {examined.Min(p => p.LastYear)}; " +
                  $"plots opened {ledger.Plots.Min(p => p.Opened)}–{ledger.Plots.Max(p => p.Opened)}");

            if (args.Flag("verbose"))
            {
                foreach (PlotStanding p in ledger.Plots)
                {
                    Console.WriteLine(
                        $"      {p.Arc} opened {p.Opened,3}  examined {p.Examined,3}x  " +
                        $"last {p.LastYear,3}  {p.Reason}");
                }
            }

            Console.WriteLine();
        }

        return unbalanced;
    }

    /// <summary>
    /// Every outcome-bearing decision in the world, with its distribution, pooled across a panel.
    ///
    /// No bar is applied. There is no principled threshold for "too skewed" yet, and inventing
    /// one to sort the output would be a constant chosen by fitting the thing it is meant to
    /// judge. This ranks and reports; adjudication is a human act, performed once per finding
    /// and written down with its reasoning.
    /// </summary>
    private static int CmdOutcomes(Args args)
    {
        string[] seeds = args.Text("seeds", "7,42,99,1234,2025")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        int years = args.Int("years", 50);
        List<EventLog> logs = [];

        foreach (string raw in seeds)
        {
            if (!ulong.TryParse(raw.Trim(), out ulong seed)) continue;
            Simulation sim = new(seed);
            sim.Run(years);
            logs.Add(sim.Log);
        }

        List<OutcomeSkew> pooled = OutcomeAudit.Pool(logs);

        Console.WriteLine($"outcome distributions, pooled over {logs.Count} seeds, most skewed first");
        Console.WriteLine();
        foreach (string line in OutcomeAudit.Report(pooled)) Console.WriteLine(line);

        Console.WriteLine();
        Console.WriteLine("reachability");
        foreach (OutcomeSkew s in pooled)
        {
            if (!s.SingleBranch) continue;
            Console.WriteLine($"  {s.Decision}: only \"{s.Commonest}\" was ever emitted across the panel — " +
                              "unreachable, or too rare for five worlds to tell apart");
        }

        // Free-string branches cannot be enumerated from a type, so absence of a branch is only
        // ever "not observed" rather than "cannot happen". Said rather than left implicit.
        Console.WriteLine("  (branches are observed values; a reason or mode the engine never emitted");
        Console.WriteLine("   cannot be distinguished from one it cannot emit, except by reading the code)");

        Console.WriteLine();
        Console.WriteLine("raids, three ways — beaten off / through with a haul / through empty");

        int i = 0;
        foreach (string raw in seeds)
        {
            if (!ulong.TryParse(raw.Trim(), out ulong seed)) continue;
            EventLog log = logs[i++];

            (int beaten, int haul, int empty) = OutcomeAudit.Raids(log);
            (int repeats, int afterFailure) = OutcomeAudit.RaidRepeats(log);
            (int total, int dead) = OutcomeAudit.RaidConsequences(log);

            Console.WriteLine(
                $"  seed {seed,-5} n={total,-4} beaten {beaten,3} ({OutcomeAudit.Pct(beaten, total)})  " +
                $"haul {haul,3} ({OutcomeAudit.Pct(haul, total)})  " +
                $"empty {empty,3} ({OutcomeAudit.Pct(empty, total)})   " +
                $"repeat targets {repeats,3} of which after a failure {afterFailure,3}   " +
                $"cited by nothing {dead,3} ({OutcomeAudit.Pct(dead, total)})");
        }

        return 0;
    }

    private static int CmdAudit(Args args)
    {
        WorldView view = Load(args);
        // --skew lowers the bar for the lopsided-outcome list, which is how you find the event
        // kinds a renderer can be right about by guessing rather than by reading.
        foreach (string line in Audit.Compute(view, skewThreshold: args.Int("skew", 70)).Report())
            Console.WriteLine(line);
        return 0;
    }

    /// <summary>
    /// Runs the self-consistency checks over a finished document, section by section.
    ///
    /// Separate from rendering on purpose. These rules need no access to the world, so they can
    /// be run against any chronicle at any time — including one produced before the rules
    /// existed, which is how their catch rate gets measured honestly rather than by re-rendering
    /// and hoping the model repeats itself.
    /// </summary>
    private static int CmdVerify(Args args)
    {
        string path = args.Positional(0) ?? Path.Combine(DefaultOutputDirectory, "chronicle-42.md");
        if (!File.Exists(path)) return Fail($"no such document: {path}");

        List<FindingScope> results = [];
        string heading = "(front matter)";
        StringBuilder body = new();

        foreach (string line in File.ReadLines(path))
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                Flush();
                heading = line[4..].Trim();
                continue;
            }

            if (line.StartsWith('#')) continue;         // group headings and the title
            if (line.StartsWith('_') || line.StartsWith('>')) continue;   // notes, not prose
            body.AppendLine(line);
        }
        Flush();

        int total = 0;
        foreach (FindingScope scope in results)
        {
            if (scope.Findings.Count == 0) continue;
            total += scope.Findings.Count;

            Console.WriteLine(scope.Scope);
            foreach (Fabrication f in scope.Findings) Console.WriteLine($"    {f.Kind,-20} {f.Context}");
            Console.WriteLine();
        }

        // Coverage summarised to one line, and in full in the sidecar. A person wants to know
        // that the rules ran; the numbers that say how much are for the golden diff to read.
        Coverage overall = new();
        foreach (FindingScope scope in results) overall.Merge(scope.Coverage);

        Console.WriteLine("what the rules read:");
        foreach (string rule in overall.Names)
        {
            RuleCounts counts = overall.Rules[rule];

            Console.WriteLine($"    {rule,-20} extracted {counts.Extracted,4}   " +
                              $"checked {counts.Checked,4} ({counts.CheckedPct,3}%)   " +
                              $"unresolvable {counts.Unresolvable,4}   fired {counts.Fired,3}" +
                              (counts.Extracted == 0 ? "   ← inert across the whole document" : "") +
                              (counts.Accounted ? "" : "   ← UNACCOUNTED, a silent path remains"));

            // Every drop, and why. A rule discarding two thirds of what it reads is either
            // meeting prose it should leave alone or failing to resolve something it should
            // not — and only the reason distinguishes them.
            foreach ((string reason, int count) in overall.Reasons(rule))
                Console.WriteLine($"        {count,4}  {reason}");
        }

        Console.WriteLine();
        Console.WriteLine($"{results.Count} sections, {total} self-consistency failures");

        // A sidecar of its own, not the render's.
        //
        // Both wrote "<name>.findings.json", so running verify after book replaced the render's
        // findings — exclusions included — with this command's Tier 1 result. The chronicle then
        // said a period had no verified account while the sidecar beside it said nothing had
        // gone wrong. Two commands, two files.
        string sidecar = Path.ChangeExtension(path, ".tier1.json");
        FindingsSidecar.Write(sidecar, results);
        Console.Error.WriteLine(sidecar);
        return 0;

        void Flush()
        {
            string text = body.ToString().Trim();
            body.Clear();
            // Two lines of preamble is not a scope. Reporting every rule as inert on it put a
            // dozen notes at the head of the sidecar and taught the reader to skip the section
            // where the real ones appear.
            if (text.Length == 0 || text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length < 40)
                return;

            Coverage cover = new();
            results.Add(new FindingScope(heading, SelfConsistency.Check(text, cover), cover));
        }
    }


    // ---- the test suite ---------------------------------------------------

    /// <summary>
    /// The five layers of the test suite, each runnable on its own.
    ///
    /// Layers 1–3 need neither a model nor a render and are fast enough to run on every commit.
    /// Layer 4 needs a chronicle; layer 5 needs a stored one to compare against.
    /// </summary>
    /// <summary>
    /// What one layer did: whether it executed, and what it found if it did.
    ///
    /// The distinction is the whole point of this type existing rather than an <c>int</c>. A layer
    /// that could not run returns no failures, and so does a layer that ran and was satisfied —
    /// which made the two indistinguishable in the only line most readers see.
    /// </summary>
    private readonly record struct LayerRun(string Name, bool Ran, int Failures, string Why = "")
    {
        public static LayerRun Skipped(string name, string why) => new(name, false, 0, why);
        public static LayerRun Completed(string name, int failures) => new(name, true, failures);
    }

    private static int CmdTest(Args args)
    {
        string layer = args.Positional(0) ?? "all";

        if (layer is "checker")
        {
            Console.WriteLine("layer 2 runs under `dotnet test` — it is unit tests over synthetic prose:");
            Console.WriteLine("    dotnet test --filter CheckerRuleTests");
            return 0;
        }

        List<LayerRun> runs = [];

        if (layer is "dynamics" or "all") runs.Add(TestDynamics(args));
        if (layer is "corpus" or "all") runs.Add(TestCorpus(args));
        if (layer is "chronicle" or "all") runs.Add(TestChronicle(args));
        if (layer is "golden" or "all") runs.Add(TestGolden(args));

        int failures = 0, ran = 0;
        foreach (LayerRun r in runs)
        {
            failures += r.Failures;
            if (r.Ran) ran++;
        }

        // The top line reports what ran, not only what failed.
        //
        // "all layers passed" over a skipped layer is a clean bill of health issued without an
        // examination, and this command printed exactly that while layer 5 sat out a ruleset
        // mismatch and layer 4 sat out a missing chronicle. Same family as a detector that alarms
        // unconditionally and as an error message printing a limit that did not apply: accurate
        // about what it found, silent about whether it looked.
        Console.WriteLine();
        Console.WriteLine($"{ran} of {runs.Count} layers ran" +
                          (failures == 0 ? ", none failed" : $", {failures} failures"));

        foreach (LayerRun r in runs)
            if (!r.Ran)
                Console.WriteLine($"  {r.Name} SKIPPED — {r.Why}");

        Console.WriteLine("  layer 2 runs under `dotnet test`; it is not one of the above.");

        // A skip is not a failure and must not become one — that was the settled decision and it
        // stays. It is reported instead, which is the thing that was missing.
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Layer 3, the regression corpus.
    ///
    /// Reported as two numbers rather than one, because the two halves fail for opposite reasons.
    /// A passage that does not fire is a hole in the checker; a correction that does fire is a
    /// false positive, and false positives are what cost round 10 seven true sections of canon.
    ///
    /// <b>Every case reads the sealed v1 world, and this command was the last place that did not.</b>
    /// A corpus row is a fabrication found by hand in prose written about one particular world:
    /// four failed attempts on Paernmel Has, sixteen dead in the raid on Hadale in 19. Re-running
    /// the simulation to obtain that world silently converted each row into an assertion about
    /// whatever the current rules happen to produce, so the first real ruleset change moved the
    /// world out from under all of them.
    ///
    /// The test suite learned this at ruleset 2 and pinned its fixtures to the archived record.
    /// This command kept simulating and has been throwing on a missing scope ever since — it was
    /// broken at ruleset 3 before geography touched anything, which is the whole shape of the
    /// silent-path family: one idea implemented twice, fixed once, and the unfixed copy failing
    /// in a place nobody was looking.
    /// </summary>
    private static LayerRun TestCorpus(Args args)
    {
        Console.WriteLine("layer 3 — the regression corpus");

        List<CorpusResult> results = Corpus.RunAll(Corpus.FindDirectory(Directory.GetCurrentDirectory()));
        int missed = 0, noisy = 0;

        foreach (CorpusResult result in results)
        {
            if (result.Passed) continue;

            Console.WriteLine($"      {(result.Fired ? "NOISY " : "MISSED")} {result.Case.Id}");
            Console.WriteLine($"             {result.Detail}");
            if (result.Fired) noisy++; else missed++;
        }

        Console.WriteLine($"  {results.Count} rows: {results.Count - missed - noisy} pass, " +
                          $"{missed} not caught, {noisy} false positives on the correction");

        if (args.Flag("verbose"))
            foreach (CorpusResult result in results)
                Console.WriteLine($"      {result.Case.Id,-46} {result.Case.ExpectRule}");

        return LayerRun.Completed("layer 3", missed + noisy);
    }

    /// <summary>Layer 1, across the seed panel. A metric that holds on one seed is an anecdote.</summary>
    private static LayerRun TestDynamics(Args args)
    {
        string[] seeds = args.Text("seeds", "7,42,99,1234,2025")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        int failures = 0;
        List<Audit> panel = [];
        List<EventLog> panelLogs = [];
        List<WorldView> panelViews = [];
        Console.WriteLine("layer 1 — dynamics invariants");

        foreach (string raw in seeds)
        {
            if (!ulong.TryParse(raw.Trim(), out ulong seed)) continue;

            // Simulated rather than loaded, so the panel does not depend on files existing.
            Simulation sim = new(seed);
            sim.Run(args.Int("years", 50));
            WorldView view = WorldView.Build(sim.Log, seed);

            List<Invariant> results = Invariants.Check(view);
            int broken = results.Count(r => !r.Held);
            failures += broken;
            panel.Add(Audit.Compute(view));

            // Once, not twice.
            //
            // Every log was added to the panel twice, which doubled n on every pooled
            // distribution metric while leaving the percentages untouched — so the figures read
            // correctly and the sample size they were justified by was a fiction. That matters
            // precisely here, because "assert the rate only where n supports it" is the rule
            // these metrics exist under, and a doubled n is that rule being satisfied with an
            // invented denominator. Found while adding the geography metrics beside them.
            panelLogs.Add(view.Log);
            panelViews.Add(view);

            Console.WriteLine($"  seed {seed,-5} {results.Count - broken}/{results.Count} held");

            foreach (Invariant r in results)
            {
                if (!r.Held)
                {
                    Console.WriteLine($"      FAIL {r.Name}: {r.Measured}, expected {r.Expected}" + Sample(r));
                    continue;
                }

                // A rate whose sample is small enough to be coarse says so even when it holds:
                // the granularity is a property of the measurement, not of this run's luck.
                if (r.Coarse)
                    Console.WriteLine($"      note {r.Name}: {r.Measured}" + Sample(r));
            }
        }

        Console.WriteLine();
        Console.WriteLine("  pooled across the panel");

        foreach (Invariant r in Invariants.CheckPanel(panel, panelLogs, panelViews))
        {
            if (!r.Held) failures++;
            Console.WriteLine($"      {(r.Held ? "ok  " : "FAIL")} {r.Name}: {r.Measured}, " +
                              $"expected {r.Expected}" + Sample(r));
        }

        return LayerRun.Completed("layer 1", failures);

        static string Sample(Invariant r) =>
            r.Sample <= 0 ? "" : $"   [n={r.Sample}" + (r.Coarse ? $", {r.PointsPerObservation}]" : "]");
    }

    /// <summary>Layer 4 — the chronicle against the log that produced it.</summary>
    private static LayerRun TestChronicle(Args args)
    {
        WorldView view = Load(args);
        ulong seed = args.ULong("seed", 42);
        string path = Path.Combine(args.Text("out", DefaultOutputDirectory),
            $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}.md");

        Console.WriteLine();
        Console.WriteLine("layer 4 — chronicle against the record");

        if (!File.Exists(path))
        {
            Console.WriteLine($"  no chronicle at {path}; skipped");
            return LayerRun.Skipped("layer 4", $"no chronicle at {path}");
        }

        List<ChronicleAudit.Complaint> complaints = ChronicleAudit.Check(view, File.ReadAllText(path));
        foreach (ChronicleAudit.Complaint c in complaints)
            Console.WriteLine($"      FAIL {c.Section}: {c.Kind} — {c.Detail}");

        Console.WriteLine($"  {complaints.Count} complaints");
        return LayerRun.Completed("layer 4", complaints.Count);
    }

    /// <summary>Layer 5 — against the most recent stored render.</summary>
    /// <summary>
    /// Layer 5 against a sealed baseline: two sidecars, compared.
    ///
    /// Distinct from the <c>out/golden</c> mode below in one way that matters — <b>it can never
    /// write</b>. A sealed baseline is create-only, and a diff layer that could update its own
    /// reference is a diff layer that passes by moving the thing it is measured against. Making
    /// a new baseline stays an act of moving the directory aside by hand.
    ///
    /// Both sides are sidecars written by the checker that produced them, never counts recomputed
    /// from stored prose. Recomputing runs today's rules over yesterday's text, and a rule that
    /// has gone quiet then reports the same figure on both sides.
    /// </summary>
    private static LayerRun TestGoldenAgainstBaseline(Args args, string baselineDir)
    {
        ulong seed = args.ULong("seed", 42);
        string stem = $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}";
        string currentDir = args.Text("current", args.Text("out", DefaultOutputDirectory));

        Console.WriteLine();
        Console.WriteLine($"layer 5 — against the sealed baseline at {baselineDir}");

        string storedProse = Path.Combine(baselineDir, stem + ".md");
        string storedSidecar = Path.Combine(baselineDir, stem + ".findings.json");
        string currentProse = Path.Combine(currentDir, stem + ".md");
        string currentSidecar = Path.Combine(currentDir, stem + ".findings.json");

        foreach (string required in new[] { storedProse, storedSidecar, currentProse, currentSidecar })
        {
            if (File.Exists(required)) continue;
            Console.WriteLine($"  missing {required}");
            Console.WriteLine("  produce the current side with: wb book --out <dir> --check-only --factions all");
            return new LayerRun("layer 5", Ran: false, Failures: 1, Why: $"missing {required}");
        }

        // The anchor is ruleset-scoped, and comparing across rulesets compares two different
        // worlds. Skipped with the reason stated rather than failed or passed: a failure would
        // read as a regression that is not one, and a pass would be a comparison of incomparable
        // things reported as agreement. Neither the baseline nor the anchor is touched either way.
        string baselineRuleset = BaselineRuleset(baselineDir);

        if (!string.Equals(baselineRuleset, Ruleset.Version, StringComparison.Ordinal))
        {
            Console.WriteLine($"  SKIPPED — baseline is ruleset {baselineRuleset} and this build " +
                              $"runs ruleset {Ruleset.Version}.");
            Console.WriteLine("  The rules changed what the simulation produces, so the stored world and " +
                              "the current one are different worlds and a diff between them means nothing.");
            Console.WriteLine("  A new baseline under the current ruleset needs a render round, which is " +
                              "generation. The sealed baseline stays sealed.");
            return LayerRun.Skipped("layer 5",
                $"baseline is ruleset {baselineRuleset}, build runs ruleset {Ruleset.Version}");
        }

        List<Drift> drift = GoldenDiff.Compare(File.ReadAllText(storedProse), File.ReadAllText(currentProse));

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> stored =
            FindingsSidecar.ReadCoverage(storedSidecar);

        drift.AddRange(GoldenDiff.CoverageSound(stored, GoldenDiff.AsCoverage(
            FindingsSidecar.ReadCoverage(currentSidecar))));

        // How much of the coverage half actually had a floor to stand on. FLOOR skips any row the
        // baseline recorded at zero, and on seed 42 that is 129 rows of 208 — protection silently
        // absent for most of the document, which is the condition `rule-inert` exists to make loud
        // one layer down and which went untraced one layer up.
        drift.AddRange(GoldenDiff.FloorCoverage(stored));

        // The query half. The v1 baseline has no query sidecar — it records
        // query-coverage-unstructured as a deficiency — so there is nothing here to diff yet.
        // Reported as unavailable rather than skipped in silence: a layer that says nothing about
        // a path reads exactly like a layer that checked it and was satisfied.
        string storedAnswers = Path.Combine(baselineDir, $"answers-{seed.ToString(CultureInfo.InvariantCulture)}.findings.json");
        string currentAnswers = Path.Combine(currentDir, $"answers-{seed.ToString(CultureInfo.InvariantCulture)}.findings.json");

        if (!File.Exists(storedAnswers))
        {
            Console.WriteLine("      note query: no coverage baseline — this baseline records " +
                              "query-coverage-unstructured, so the query path cannot be diffed yet");
        }
        else if (!File.Exists(currentAnswers))
        {
            Console.WriteLine($"      note query: baseline has a sidecar and {currentAnswers} does not; " +
                              "run wb suite to produce one");
        }
        else
        {
            drift.AddRange(GoldenDiff.CoverageSound(
                FindingsSidecar.ReadCoverage(storedAnswers),
                GoldenDiff.AsCoverage(FindingsSidecar.ReadCoverage(currentAnswers))));
        }

        drift.Sort(static (a, b) => a.Fails != b.Fails
            ? b.Fails.CompareTo(a.Fails)
            : string.CompareOrdinal(a.Kind + a.Section, b.Kind + b.Section));

        int failures = drift.Count(d => d.Fails);

        foreach (Drift d in drift)
            Console.WriteLine($"      {(d.Fails ? "FAIL" : "note")} {d.Section}: {d.Kind} — {d.Detail}");

        Console.WriteLine($"  {failures} failed, {drift.Count - failures} noted");

        if (args.Flag("accept") || args.Flag("rebaseline"))
        {
            Console.WriteLine();
            Console.WriteLine("  a sealed baseline is create-only and this layer never writes to one.");
            Console.WriteLine("  to establish a new baseline, move the directory aside first.");
            return LayerRun.Completed("layer 5", Math.Max(failures, 1));
        }

        return LayerRun.Completed("layer 5", failures);
    }

    /// <summary>
    /// The ruleset a sealed baseline was cut under.
    ///
    /// Read from its manifest. A null or absent value means the baseline predates the counter,
    /// which is ruleset 1 by definition — that is the ruleset that was in force when it was cut,
    /// and treating "not recorded" as "matches whatever is running" is exactly the absent-versus-
    /// unknown conflation that would make this check pass by not running.
    /// </summary>
    private static string BaselineRuleset(string baselineDir)
    {
        string manifest = Path.Combine(baselineDir, "manifest.json");
        if (!File.Exists(manifest)) return "1";

        using System.Text.Json.JsonDocument doc =
            System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));

        return doc.RootElement.TryGetProperty("ruleset_version", out System.Text.Json.JsonElement v)
            && v.ValueKind == System.Text.Json.JsonValueKind.String
                ? v.GetString() ?? "1"
                : "1";
    }

    private static LayerRun TestGolden(Args args)
    {
        ulong seed = args.ULong("seed", 42);
        string outDir = args.Text("out", DefaultOutputDirectory);
        string current = Path.Combine(outDir, $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}.md");

        string baselineDir = args.Text("baseline", "");
        if (baselineDir.Length > 0) return TestGoldenAgainstBaseline(args, baselineDir);

        Console.WriteLine();
        Console.WriteLine("layer 5 — against the stored render");

        string goldenDir = Path.Combine(outDir, "golden");
        string? golden = Newest(goldenDir, $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}-r*.md");

        if (golden is null || !File.Exists(current))
        {
            Console.WriteLine("  nothing stored yet; run `wb test golden --accept` to store this render");
            if ((args.Flag("accept") || args.Flag("rebaseline")) && File.Exists(current))
                Accept(goldenDir, current, seed, "");
            return LayerRun.Skipped("layer 5",
                golden is null ? "no stored render to compare against" : $"no current render at {current}");
        }

        string currentText = File.ReadAllText(current);
        List<Drift> drift = GoldenDiff.Compare(File.ReadAllText(golden), currentText);

        Dictionary<string, Coverage> coverage = GoldenDiff.CoverageOf(currentText);
        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> baseline = Baseline(goldenDir, seed);

        // coverage-sound: the half of this layer that can see a rule go quiet. The stored counts
        // are the ones written when the baseline was accepted; recomputing them from the stored
        // text would run today's rules over yesterday's prose and report agreement between a
        // broken checker and itself.
        if (baseline.Count > 0) drift.AddRange(GoldenDiff.CoverageSound(baseline, coverage));
        else Console.WriteLine("      note no coverage baseline stored; accept to record one");

        // The ratio is reported and no longer gates. coverage-sound supersedes it, and gating on
        // both would fail a build twice for one fault — and reward the cure that caused round 13,
        // which was to raise the ratio by extracting less.
        drift.AddRange(GoldenDiff.CheckedRatio(coverage, args.Int("bar", 95)));

        drift.Sort(static (a, b) => a.Fails != b.Fails
            ? b.Fails.CompareTo(a.Fails)
            : string.CompareOrdinal(a.Kind + a.Section, b.Kind + b.Section));

        int failures = drift.Count(d => d.Fails);

        foreach (Drift d in drift)
            Console.WriteLine($"      {(d.Fails ? "FAIL" : "note")} {d.Section}: {d.Kind} — {d.Detail}");

        Console.WriteLine($"  against {Path.GetFileName(golden)}: {failures} failed, " +
                          $"{drift.Count - failures} noted");

        if (!args.Flag("accept") && !args.Flag("rebaseline")) return LayerRun.Completed("layer 5", failures);

        // Accepting must not quietly lower a floor.
        //
        // A baseline that follows the last run down is not a baseline, and re-running the accept
        // command is exactly how that happens without anyone deciding it. Where the prose has
        // genuinely changed and a section really does hold fewer claims, lowering the floor is a
        // legitimate act — and it is an act, named on the command line with a reason.
        List<Drift> lowered = GoldenDiff.WouldLowerAFloor(baseline, coverage);

        if (lowered.Count > 0 && !args.Flag("rebaseline"))
        {
            Console.WriteLine();
            Console.WriteLine("  refusing to accept: this run reads less than the baseline in " +
                              $"{lowered.Count} place(s).");
            foreach (Drift d in lowered) Console.WriteLine($"      {d.Section}: {d.Detail}");
            Console.WriteLine();
            Console.WriteLine("  fix the extraction, or re-baseline deliberately:");
            Console.WriteLine("      wb test golden --seed <n> --rebaseline --why \"<reason>\"");

            return LayerRun.Completed("layer 5", Math.Max(failures, 1));
        }

        Accept(goldenDir, current, seed, args.Flag("rebaseline") ? args.Text("why", "") : "");

        // The floor moves up on accept and only ever down on a named re-baseline.
        Dictionary<string, Dictionary<string, RuleCounts>> floor = new(StringComparer.Ordinal);
        if (!args.Flag("rebaseline")) Raise(floor, baseline);

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> now = new(StringComparer.Ordinal);
        foreach ((string scope, Coverage cover) in coverage) now[scope] = cover.Rules;
        Raise(floor, now);

        Dictionary<string, Coverage> asCoverage = [];
        foreach ((string scope, Dictionary<string, RuleCounts> rules) in floor)
        {
            Coverage cover = new();
            foreach ((string rule, RuleCounts counts) in rules)
            {
                cover.Extracted(rule, counts.Extracted);
                cover.Checked(rule, counts.Checked);
                cover.Fired(rule, counts.Fired);
            }
            asCoverage[scope] = cover;
        }

        File.WriteAllText(FloorPath(goldenDir, seed), Coverage.ToJson(asCoverage));
        return LayerRun.Completed("layer 5", failures);
    }

    /// <summary>
    /// The coverage floor: the most each rule has ever read in each scope.
    ///
    /// A high-water mark, not the last run's snapshot. If the floor were simply whatever the
    /// newest accepted render recorded, then accepting a run in which a rule had gone quiet
    /// would install the quiet figure as the thing to beat, and the next round would compare a
    /// broken checker against itself and pass. That is the drift the floor exists to stop, and
    /// it is the shape rounds 11 to 13 kept taking.
    ///
    /// Held in one file that <c>--accept</c> may raise and only <c>--rebaseline</c> may lower.
    /// The per-round snapshots stay beside it as history; where no floor file exists yet, they
    /// are max-merged into one, so the floor starts at the highest figure ever actually seen
    /// rather than at whatever the last command happened to write.
    /// </summary>
    private static Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> Baseline(
        string goldenDir, ulong seed)
    {
        string path = FloorPath(goldenDir, seed);
        if (File.Exists(path)) return Coverage.FromJson(File.ReadAllText(path));

        Dictionary<string, Dictionary<string, RuleCounts>> merged = new(StringComparer.Ordinal);
        if (!Directory.Exists(goldenDir)) return [];

        string[] history = Directory.GetFiles(
            goldenDir, $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}-r*.coverage.json");
        Array.Sort(history, StringComparer.Ordinal);

        foreach (string file in history) Raise(merged, Coverage.FromJson(File.ReadAllText(file)));

        Dictionary<string, IReadOnlyDictionary<string, RuleCounts>> floor = new(StringComparer.Ordinal);
        foreach ((string scope, Dictionary<string, RuleCounts> rules) in merged) floor[scope] = rules;
        return floor;
    }

    private static string FloorPath(string goldenDir, ulong seed) =>
        Path.Combine(goldenDir, $"coverage-{seed.ToString(CultureInfo.InvariantCulture)}.json");

    /// <summary>Raises a floor to the higher of itself and another reading, per scope per rule.</summary>
    private static void Raise(
        Dictionary<string, Dictionary<string, RuleCounts>> floor,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, RuleCounts>> reading)
    {
        foreach ((string scope, IReadOnlyDictionary<string, RuleCounts> rules) in reading)
        {
            if (!floor.TryGetValue(scope, out Dictionary<string, RuleCounts>? mine))
                floor[scope] = mine = new Dictionary<string, RuleCounts>(StringComparer.Ordinal);

            foreach ((string rule, RuleCounts counts) in rules)
            {
                RuleCounts was = mine.GetValueOrDefault(rule, new RuleCounts(0, 0, 0, 0));
                if (counts.Extracted <= was.Extracted) continue;
                mine[rule] = counts;
            }
        }
    }

    /// <summary>Stores a render as the new reference. Also the LoRA corpus, at no extra cost.</summary>
    private static void Accept(string goldenDir, string current, ulong seed, string why)
    {
        Directory.CreateDirectory(goldenDir);
        string stem = $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}-r";

        int next = 1;
        foreach (string existing in Directory.GetFiles(goldenDir, stem + "*.md"))
        {
            string tail = Path.GetFileNameWithoutExtension(existing)[stem.Length..];
            if (int.TryParse(tail, out int n)) next = Math.Max(next, n + 1);
        }

        string stored = Path.Combine(goldenDir, $"{stem}{next.ToString(CultureInfo.InvariantCulture)}.md");
        File.Copy(current, stored, overwrite: true);

        // The coverage as the rules read it today, stored beside the prose. Next round's diff
        // compares against this file rather than recomputing it, which is the only arrangement
        // in which a rule that has since gone quiet shows up as a difference.
        File.WriteAllText(Path.ChangeExtension(stored, ".coverage.json"),
            Coverage.ToJson(GoldenDiff.CoverageOf(File.ReadAllText(current))));

        Console.WriteLine($"  stored as {stored}");

        if (why.Length == 0) return;

        // Why a floor was lowered, beside the floor. A re-baseline with no reason recorded is
        // indistinguishable next round from one nobody thought about.
        File.AppendAllText(Path.Combine(goldenDir, "rebaselines.log"),
            $"{Path.GetFileName(stored)}\t{why}\n");

        Console.WriteLine($"  re-baselined: {why}");
    }

    private static string? Newest(string directory, string pattern)
    {
        if (!Directory.Exists(directory)) return null;

        string? newest = null;
        int best = -1;

        foreach (string path in Directory.GetFiles(directory, pattern))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int at = name.LastIndexOf("-r", StringComparison.Ordinal);
            if (at < 0 || !int.TryParse(name[(at + 2)..], out int n) || n <= best) continue;
            best = n;
            newest = path;
        }

        return newest;
    }

    /// <summary>The deepest causal chains, rendered in full so they can be judged by reading
    /// rather than only counted.</summary>
    private static int CmdChains(Args args)
    {
        WorldView view = Load(args);
        int wanted = args.Int("top", 3);

        foreach (IReadOnlyList<EventId> chain in CausalTrace.DeepestChains(view.Log, wanted))
        {
            Console.WriteLine($"--- chain of {chain.Count} ---");
            foreach (EventId id in chain) Console.WriteLine($"  {view.Summarise(id)}");
            Console.WriteLine();
        }
        return 0;
    }

    // ---- render -----------------------------------------------------------

    private static async Task<int> CmdRender(Args args)
    {
        WorldView view = Load(args);
        if (!EventId.TryParse(args.Positional(0), out EventId id))
            return Fail("usage: wb render e:415 [--seed 42] [--force]");

        ContextPack pack = ContextPackBuilder.Single(view, id);
        return await Emit(args, pack, view);
    }

    private static async Task<int> CmdChronicle(Args args)
    {
        WorldView view = Load(args);
        ContextPack pack;

        if (args.Int("year", int.MinValue) is var year and not int.MinValue)
        {
            pack = ContextPackBuilder.Year(view, year);
        }
        else if (EntityId.TryParse(args.Text("reign", ""), out EntityId ruler) && ruler.Kind == EntityKind.Actor)
        {
            // A person may have held more than one seat, and each is its own account. Without
            // --faction the last one is taken, and the others are listed so they can be asked for.
            List<ReignSpell> spells = ContextPackBuilder.Reigns(view, ruler);
            if (spells.Count == 0) return Fail($"{view.State.Label(ruler)} never held a seat.");

            ReignSpell chosen = spells[^1];
            if (EntityId.TryParse(args.Text("faction", ""), out EntityId want) && want.Kind == EntityKind.Faction)
            {
                ReignSpell? match = spells.Find(s => s.Faction == want);
                if (match is null) return Fail($"{view.State.Label(ruler)} never held the seat of {view.State.NameOf(want)}.");
                chosen = match;
            }
            else if (spells.Count > 1)
            {
                Console.Error.WriteLine($"{view.State.Label(ruler)} held {spells.Count} seats; rendering the last. Others:");
                foreach (ReignSpell s in spells)
                    Console.Error.WriteLine($"  --reign {ruler} --faction {s.Faction}  ({view.State.NameOf(s.Faction)}, {s.From}–{s.To})");
            }

            pack = ContextPackBuilder.Reign(view, chosen);
        }
        else if (EntityId.TryParse(args.Text("arc", ""), out EntityId arc) && arc.Kind == EntityKind.Arc)
        {
            pack = ContextPackBuilder.Arc(view, arc);
        }
        else if (EntityId.TryParse(args.Text("faction", ""), out EntityId faction) && faction.Kind == EntityKind.Faction)
        {
            pack = ContextPackBuilder.Faction(
                view, faction, args.Int("from", int.MinValue), args.Int("to", int.MaxValue));
        }
        else if (EventId.TryParse(args.Text("chain", ""), out EventId tip))
        {
            pack = ContextPackBuilder.Chain(view, tip);
        }
        else
        {
            return Fail("usage: wb chronicle --year 24 | --reign a:12 | --arc w:3 | --faction f:2 | --chain e:415");
        }

        return await Emit(args, pack, view);
    }

    /// <summary>
    /// Renders a whole world into one readable document. The cache means passages already
    /// written are reused, so this is cheap to re-run and only pays for what is new.
    /// </summary>
    private static async Task Warm(ILlmClient client)
    {
        Console.Write("  loading the model… ");
        long started = Environment.TickCount64;

        try
        {
            await client.CompleteAsync(new LlmRequest { System = "Reply with one word.", Prompt = "Ready?" });
            Console.WriteLine($"{(Environment.TickCount64 - started) / 1000}s");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Reported and not fatal. The renders that follow will fail with their own message
            // if the daemon really is down, and that message is the more useful one.
            Console.WriteLine($"no answer ({ex.GetType().Name})");
        }
    }

    private static async Task<int> CmdBook(Args args)
    {
        WorldView view = Load(args);
        ulong seed = args.ULong("seed", 42);
        string outDir = args.Text("out", DefaultOutputDirectory);

        LlmOptions options = LlmOptions.Default with
        {
            Model = args.Text("model", LlmOptions.Default.Model),
            Endpoint = args.Text("endpoint", LlmOptions.Default.Endpoint),
        };

        // --check-only re-runs the rules over a render cache and never generates: no client is
        // constructed, so a cache miss fails where it happens instead of being quietly filled by
        // a call. That is what re-checking an archived baseline needs — the alternative, aiming
        // --endpoint at a closed port, proves the same thing by misconfiguration, and a property
        // that holds only while a setting stays wrong is not a property to depend on.
        bool checkOnly = args.Flag("check-only");

        using OllamaClient? live = checkOnly ? null : new OllamaClient(options);
        ILlmClient client = live is not null ? live : new CacheOnlyLlmClient(options.Model);

        RenderStore store = new(Path.Combine(outDir, "renders.json"));
        Chronicler chronicler = new(
            client,
            store,
            new RenderJournal(Path.Combine(outDir, "renders.jsonl")));

        // A 22 GB model is evicted between runs, and loading it counts against the first
        // section's timeout rather than against nothing. One trivial call in the same process
        // pays that cost where it is visible instead of turning the first real render into a
        // timeout that reads as an inference failure.
        if (live is not null) await Warm(live);

        // No chain scope. Walking a chain produced one sentence per event joined by "two years
        // later" — longer than the log lines it replaced and harder to scan, which fails the
        // only test that matters. Aggregation earns its keep by summarising many events, not by
        // transliterating a sequence. The chain builder stays for v1.2, where "why did X happen"
        // is exactly the question it answers.
        // Grouped, and chronological inside each group. Emitted in discovery order the document
        // ran two wars, then a reign that began in 51, then the faction sections starting at 2 —
        // which reads as a mistake rather than as an arrangement. Either order works; what does
        // not work is an order the reader cannot name.
        List<(string Group, string Heading, ContextPack Pack)> sections = [];

        int wars = args.Int("wars", 2);
        foreach (Arc arc in view.State.Arcs)
        {
            if (wars <= 0) break;
            if (arc.Kind != ArcKind.War) continue;
            sections.Add(("Wars", WarHeading(view, arc), ContextPackBuilder.Arc(view, arc.Id)));
            wars--;
        }

        int reigns = args.Int("reigns", 2);
        foreach (Faction f in view.State.ActiveFactions())
        {
            if (reigns <= 0) break;
            if (f.Leader.IsNone) continue;

            // The spell in *this* seat, not everything its holder ever did. A man who has held
            // two seats has two reigns and they are different chapters.
            ReignSpell? spell = ContextPackBuilder.Reigns(view, f.Leader).FindLast(s => s.Faction == f.Id);
            if (spell is null) continue;

            sections.Add(("Reigns",
                $"The rule of {view.State.NameOf(f.Leader)} over {f.Name}, {spell.From}–{spell.To}",
                ContextPackBuilder.Reign(view, spell)));
            reigns--;
        }

        // Chosen by how much history they carry, not by whether they survived it. Picking only
        // living powers dropped the faction that rose, fought, starved and was conquered — the
        // one chapter a history most wants — because it happened to end before the log did.
        //
        // "--factions all" takes every power there was. Weight is a reasonable default for a
        // sample but a poor one for a history: the Wurn League held three places, fought two
        // wars, lost all three and was destroyed, and ranked below the cut on event count
        // alone. A world's history should be able to include every power in it.
        int powers = string.Equals(args.Text("factions", ""), "all", StringComparison.OrdinalIgnoreCase)
            ? view.State.Factions.Count
            : args.Int("factions", 2);
        foreach (Faction f in Weightiest(view, args.Text("power", "")))
        {
            if (powers <= 0) break;
            // Faction names already carry their own article ("the Kebarrow Compact").
            string title = f.Name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)
                ? char.ToUpperInvariant(f.Name[0]) + f.Name[1..]
                : f.Name;

            // A century of one power's history does not compress into a single passage — asked
            // to, the model returns one 340-word block. Split into eras it produces sections
            // with shape, and each has a scope it can hold.
            ContextPack whole = ContextPackBuilder.Faction(view, f.Id);
            const int eraSize = 20;

            if (whole.Events.Count <= 60)
            {
                sections.Add(("Powers", $"{title}, {whole.FromYear}–{whole.ToYear}", whole));
            }
            else
            {
                for (int start = whole.FromYear; start <= whole.ToYear; start += eraSize)
                {
                    int end = Math.Min(start + eraSize - 1, whole.ToYear);
                    ContextPack era = ContextPackBuilder.Faction(view, f.Id, start, end);
                    if (era.Events.Count == 0) continue;
                    sections.Add(("Powers", $"{title}, {start}–{end}", era));
                }
            }

            powers--;
        }

        // Wars first, then the powers, then individual reigns — widest scope to narrowest —
        // and each group ordered by the year its subject begins.
        string[] order = ["Wars", "Powers", "Reigns"];
        sections.Sort((a, b) =>
        {
            int ga = Array.IndexOf(order, a.Group);
            int gb = Array.IndexOf(order, b.Group);
            if (ga != gb) return ga.CompareTo(gb);
            if (a.Pack.FromYear != b.Pack.FromYear) return a.Pack.FromYear.CompareTo(b.Pack.FromYear);
            return string.CompareOrdinal(a.Heading, b.Heading);
        });

        StringBuilder book = new();
        book.Append("# A history of ").Append(view.State.NameOf(EntityId.Place(1)))
            .Append("\n\nSeed ").Append(seed.ToString(CultureInfo.InvariantCulture))
            .Append(", years ").Append(view.FirstYear.ToString(CultureInfo.InvariantCulture))
            .Append('–').Append(view.LastYear.ToString(CultureInfo.InvariantCulture))
            .Append(". Every name, date and figure below is drawn from the simulation itself.\n");

        string stem = Path.Combine(outDir, $"chronicle-{seed.ToString(CultureInfo.InvariantCulture)}");
        string diagnosticsPath = stem + ".unverified.md";

        StringBuilder diagnostics = new();
        diagnostics.Append("# Unverified passages, seed ")
            .Append(seed.ToString(CultureInfo.InvariantCulture))
            .Append("\n\nEach of these was written twice and failed the check both times, so it is ")
            .Append("kept out of the chronicle. Nothing here is canon.\n");

        int suspect = 0, unverified = 0;
        string openGroup = "";
        List<FindingScope> reported = [];
        HashSet<string> excludedScopes = [];

        foreach ((string group, string heading, ContextPack pack) in sections)
        {
            if (pack.Events.Count == 0) continue;

            Console.Error.WriteLine($"  rendering {pack.Kind} \"{heading}\" ({pack.Events.Count} events)…");

            RenderOutcome outcome;
            try
            {
                outcome = await chronicler.RenderAsync(pack);
            }
            catch (RenderTruncatedException ex)
            {
                // One scope running out of room is not a reason to lose the other nine. It is
                // reported, held out of canon like any other failure, and the book goes on.
                Console.Error.WriteLine($"      FAILED {ex.Message}");
                unverified++;
                excludedScopes.Add(heading);
                book.Append("\n### ").Append(heading).Append("\n\n")
                    .Append("_No verified account of this period: the passage ran out of room ")
                    .Append("before it finished._\n");

                diagnostics.Append("\n\n## ").Append(heading).Append("\n\n- `truncated` — ")
                    .Append(ex.Message).Append('\n');
                continue;
            }

            suspect += outcome.Fabrication.Findings.Count;
            // Every scope, not only the ones with findings. The coverage block is the record
            // of what was examined, and a section that reported nothing is exactly the one worth
            // knowing the examination of.
            reported.Add(new FindingScope(heading, outcome.Fabrication.Findings, outcome.Fabrication.Coverage));

            foreach (Fabrication f in outcome.Fabrication.Findings)
            {
                Console.Error.WriteLine(
                    $"      {(f.BlocksCanon ? "FALSE " : "STYLE ")} {f.Kind,-22} '{f.Token}'  …{f.Context}…");
            }

            if (group != openGroup)
            {
                book.Append("\n\n## ").Append(group).Append("\n\n_In the order they begin._\n");
                openGroup = group;
            }

            // A passage that failed the check twice does not go in. Keeping it behind a warning
            // block made the chronicle both canon and debug artefact at once — text known to be
            // false, sitting in the document that is supposed to be the world's own history,
            // and about to be inherited by a query layer that has nowhere to put a warning.
            // The scope reports as unavailable here and the text goes to the diagnostics file.
            if (outcome.Render.Status == RenderStatus.Suspect)
            {
                unverified++;
                excludedScopes.Add(heading);
                book.Append("\n### ").Append(heading).Append("\n\n")
                    .Append("_No verified account of this period. The passage written for it did not ")
                    .Append("check out against the records; it is in `")
                    .Append(Path.GetFileName(diagnosticsPath)).Append("`._\n");

                diagnostics.Append("\n\n## ").Append(heading).Append("\n\n");
                foreach (Fabrication f in outcome.Fabrication.Findings)
                    diagnostics.Append("- `").Append(f.Kind).Append("` — ").Append(f.Context).Append('\n');
                diagnostics.Append('\n').Append(outcome.Render.Text).Append('\n');
                continue;
            }

            book.Append("\n### ").Append(heading).Append("\n\n").Append(outcome.Render.Text).Append('\n');
        }

        string path = stem + ".md";
        File.WriteAllText(path, book.ToString());

        // Findings in a machine-readable form as well as prose, so a catch rate can be counted
        // across seeds and rounds rather than re-read out of a conversation each time.
        //
        // The exclusions are applied here rather than as each scope is added, because whether a
        // section kept its place is only known after its render has been judged. A finding that
        // cost a section its place is marked fatal; without that the chronicle said a period had
        // no verified account while the sidecar recorded nothing at all.
        List<FindingScope> judged = [];
        foreach (FindingScope scope in reported)
            judged.Add(scope with { Excluded = excludedScopes.Contains(scope.Scope) });

        FindingsSidecar.Write(stem + ".findings.json", judged);

        if (unverified > 0) File.WriteAllText(diagnosticsPath, diagnostics.ToString());
        else if (File.Exists(diagnosticsPath)) File.Delete(diagnosticsPath);

        // Item 1 of round 12, asserted rather than hoped for: every extracted assertion ends
        // checked or explained. A rule with a third path presents as a rule that found nothing.
        Coverage tally = new();
        foreach (FindingScope s in reported) tally.Merge(s.Coverage);

        Console.Error.WriteLine();
        Console.Error.WriteLine("what the rules read:");

        foreach (string rule in tally.Names)
        {
            RuleCounts c = tally.Rules[rule];
            if (c.Extracted == 0) continue;

            Console.Error.WriteLine(
                $"    {rule,-20} extracted {c.Extracted,4}   checked {c.Checked,4} ({c.CheckedPct,3}%)   " +
                $"unresolvable {c.Unresolvable,4}   fired {c.Fired,3}");

            foreach ((string reason, int count) in tally.Reasons(rule))
                Console.Error.WriteLine($"        {count,4}  {reason}");
        }

        foreach (string broken in tally.Unaccounted())
            Console.Error.WriteLine($"    UNACCOUNTED {broken}");

        Console.Error.WriteLine();
        Console.Error.WriteLine(
            $"{sections.Count} passages, {suspect} suspect tokens, {unverified} held out of canon");

        // Said out loud rather than left to be assumed. These passages were served from entries
        // written before the pack inputs were hashed, so the cache cannot show they are still the
        // passages those inputs produce — a weaker claim than a pinned hit, and one a reader
        // would otherwise have no way to distinguish from the stronger one.
        if (store.UnpinnedHits > 0)
        {
            Console.Error.WriteLine(
                $"  {store.UnpinnedHits} of them came from cache entries with no input hash, " +
                "so their inputs are unverified. Re-render to pin them.");
        }

        Console.WriteLine(path);
        if (unverified > 0) Console.WriteLine(diagnosticsPath);
        return 0;
    }

    /// <summary>
    /// A war's heading, carrying who fought it and when.
    ///
    /// Arc names are generated from the ground being contested, so two wars over the same place
    /// produce two chapters called "the War for Threi Cut", told apart only by a year appended
    /// to the second. The belligerents are what a reader is actually looking for, and the arc
    /// has recorded them since v0.
    /// </summary>
    /// <summary>
    /// Powers ranked by how many significant events name them, dead or alive, with any named
    /// in <paramref name="wanted"/> promoted to the front.
    ///
    /// The promotion exists so a particular power can be asked for by name — a section that is
    /// being used as a verification benchmark has to stay in the document even when the weight
    /// ordering would drop it.
    /// </summary>
    private static List<Faction> Weightiest(WorldView view, string wanted = "")
    {
        List<Faction> first = [];
        foreach (string token in wanted.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!EntityId.TryParse(token.Trim(), out EntityId id) || id.Kind != EntityKind.Faction) continue;
            if (id.Index <= view.State.Factions.Count) first.Add(view.State.FactionOf(id));
        }

        List<(Faction Faction, int Weight)> ranked = [];

        foreach (Faction f in view.State.Factions)
        {
            if (first.Exists(p => p.Id == f.Id)) continue;

            int weight = 0;
            foreach (EventId id in view.Log.ForEntity(f.Id))
                if (view.Log.Get(id).Significance >= Significance.Major) weight++;
            ranked.Add((f, weight));
        }

        ranked.Sort(static (a, b) => a.Weight != b.Weight
            ? b.Weight.CompareTo(a.Weight)
            : a.Faction.Id.CompareTo(b.Faction.Id));

        List<Faction> ordered = [.. first];
        foreach ((Faction f, int _) in ranked) ordered.Add(f);
        return ordered;
    }

    private static string WarHeading(WorldView view, Arc arc)
    {
        string name = char.ToUpperInvariant(arc.Name[0]) + arc.Name[1..];
        string years = $"{arc.StartYear.ToString(CultureInfo.InvariantCulture)}–" +
                       $"{(arc.EndYear ?? view.LastYear).ToString(CultureInfo.InvariantCulture)}";

        if (arc.Sides.Count < 2) return $"{name}, {years}";

        List<string> sides = [];
        foreach (EntityId side in arc.Sides) sides.Add(view.State.NameOf(side));
        return $"{name}: {string.Join(" against ", sides)}, {years}";
    }

    /// <summary>
    /// The query regression suite. With <c>--retrieval</c> it stops after retrieval and prints
    /// the events found, which is where a wrong answer is easiest to see — fluent prose hides
    /// a bad retrieval, a list of event lines does not.
    /// </summary>
    private static async Task<int> CmdSuite(Args args)
    {
        WorldView view = Load(args);
        bool retrievalOnly = args.Flag("retrieval");

        LlmOptions options = LlmOptions.Default with
        {
            Model = args.Text("model", LlmOptions.Default.Model),
            Endpoint = args.Text("endpoint", LlmOptions.Default.Endpoint),
        };

        using OllamaClient client = new(options);
        QueryEngine engine = new(client, view);

        if (retrievalOnly)
        {
            foreach (SuiteQuestion q in QuerySuite.ForSeed42)
            {
                QueryResult result = await engine.AskAsync(q.Text);
                Console.WriteLine($"Q: {q.Text}");
                Console.WriteLine($"   expect {q.Expect} — {q.Note}");
                // The subject as the planner wrote it, not only what it resolved to. A question that
                // retrieves nothing because the subject did not resolve looks identical to one
                // that retrieves nothing because the world holds nothing — and the first is a
                // bug in the resolver while the second is the right answer.
                Console.WriteLine($"   plan: {result.Plan.Shape} on " +
                    $"{(result.Plan.Entity.IsNone ? "nothing" : view.State.Label(result.Plan.Entity))}" +
                    $"  <- \"{result.Plan.Subject}\"  topics: {string.Join(", ", result.Plan.Topics)}" +
                    (result.FalsePremise is null ? "" : "  [premise rejected]"));

                foreach (EventId id in result.Retrieved) Console.WriteLine($"     {view.Summarise(id)}");
                if (result.Retrieved.Count == 0) Console.WriteLine("     (nothing retrieved)");
                Console.WriteLine();
            }
            return 0;
        }

        List<QuerySuite.Scored> scored = await QuerySuite.RunAsync(engine, view);

        int passed = 0;
        Coverage overall = new();

        foreach (QuerySuite.Scored s in scored)
        {
            if (s.Passed) passed++;
            Console.WriteLine($"[{(s.Passed ? "PASS" : "FAIL")}] {s.Question.Text}");
            Console.WriteLine($"        {s.Verdict}");
            Console.WriteLine($"        > {s.Result.Answer.ReplaceLineEndings("\n          ")}");

            foreach (Fabrication f in s.Result.Fatal)
                Console.WriteLine($"        FATAL    {f.Kind,-22} '{f.Token}'  {f.Context}");

            foreach (Fabrication f in s.Result.Fabrication.Findings)
                if (!f.BlocksCanon)
                    Console.WriteLine($"        note     {f.Kind,-22} '{f.Token}'  {f.Context}");

            if (s.Result.Withheld is { } held)
                Console.WriteLine($"        withheld: {held.ReplaceLineEndings(" ")}");

            foreach (string broken in s.Result.Fabrication.Coverage.Unaccounted())
                Console.WriteLine($"        UNSOUND  {broken}");

            overall.Merge(s.Result.Fabrication.Coverage);
            Console.WriteLine();
        }

        // The coverage table for the answer path as a whole. Per answer it says almost nothing —
        // a rule with no business in three sentences reads the same as a rule that never ran —
        // and across sixteen it is the only thing that distinguishes the two.
        Console.WriteLine("what the rules read, across every answer:");
        foreach (string rule in overall.Names)
        {
            RuleCounts c = overall.Rules[rule];
            Console.WriteLine($"    {rule,-20} extracted {c.Extracted,4}   checked {c.Checked,4} " +
                              $"({c.CheckedPct,3}%)   unresolvable {c.Unresolvable,3}   fired {c.Fired,3}" +
                              (c.Extracted == 0 ? "   [inert]" : ""));

            foreach ((string reason, int count) in overall.Reasons(rule))
                Console.WriteLine($"        {count,3}  {reason}");

            foreach (string span in overall.Spans(rule))
                Console.WriteLine($"             on: {span}");
        }

        // The same sidecar the chronicle path has always written, for the path that never had
        // one. Stdout carries this already, but prose is not diffable: `departure` extraction
        // went 4 → 0 between two v1.2 rounds with nothing to compare, on exactly this path.
        // Layer 5's value is diffing the coverage block rather than the answers, and until now
        // there was no block here to diff.
        string sidecarPath = Path.Combine(
            args.Text("out", DefaultOutputDirectory),
            $"answers-{args.ULong("seed", 42).ToString(CultureInfo.InvariantCulture)}.findings.json");

        FindingsSidecar.Write(sidecarPath, FindingsSidecar.ForAnswers(scored));
        Console.Error.WriteLine(sidecarPath);

        Console.WriteLine();
        Console.WriteLine($"{passed} of {scored.Count} passed");
        return passed == scored.Count ? 0 : 1;
    }

    private static async Task<int> CmdAsk(Args args)
    {
        WorldView view = Load(args);
        string question = args.Positional(0) ?? "";
        if (question.Length == 0) return Fail("usage: wb ask \"why did the Wurn League collapse?\"");

        LlmOptions options = LlmOptions.Default with
        {
            Model = args.Text("model", LlmOptions.Default.Model),
            Endpoint = args.Text("endpoint", LlmOptions.Default.Endpoint),
        };

        using OllamaClient client = new(options);
        QueryEngine engine = new(client, view);

        // Everything the model will be shown, and nothing generated. The pack is where a wrong
        // answer is usually already visible, and reading it costs a planning call rather than a
        // two-minute generation.
        if (args.Flag("pack"))
        {
            QueryPreparation prepared = await engine.PrepareAsync(question);

            if (prepared.Unresolvable is { } cannot) { Console.WriteLine(cannot); return 0; }
            if (prepared.FalsePremise is { } wrong) { Console.WriteLine(wrong); return 0; }
            if (prepared.Pack is not { } built) { Console.WriteLine("(nothing retrieved)"); return 0; }

            Console.WriteLine(built.Body);
            Console.WriteLine($"[{built.Events.Count} events, {built.Causes.Count} causes, " +
                              $"~{built.TokenEstimate} prompt tokens]");
            return 0;
        }

        QueryResult result = await engine.AskAsync(question);

        Console.Error.WriteLine(
            $"[{result.Plan.Shape.ToString().ToLowerInvariant()} query on " +
            $"{(result.Plan.Entity.IsNone ? "nothing" : view.State.Label(result.Plan.Entity))} " +
            $"<- \"{result.Plan.Subject}\"; {result.Retrieved.Count} records retrieved]");

        Console.WriteLine();
        Console.WriteLine(result.Answer);
        Console.WriteLine();

        if (args.Flag("show"))
        {
            Console.Error.WriteLine("records used:");
            foreach (EventId id in result.Retrieved)
                Console.Error.WriteLine($"  {view.Summarise(id)}");
        }

        if (result.BadCitations.Count > 0)
            Console.Error.WriteLine($"[citations not in the retrieved set: {string.Join(", ", result.BadCitations)}]");

        if (!result.Fabrication.Clean)
        {
            Console.Error.WriteLine($"[fabrication check: {result.Fabrication.Findings.Count} suspect]");
            foreach (Fabrication f in result.Fabrication.Findings.Take(6))
                Console.Error.WriteLine($"    {f.Kind,-18} '{f.Token}'  …{f.Context}…");
        }

        return 0;
    }

    private static async Task<int> Emit(Args args, ContextPack pack, WorldView view)
    {
        if (pack.Events.Count == 0) return Fail("nothing to render — that selection matched no events.");

        LlmOptions options = LlmOptions.Default with
        {
            Model = args.Text("model", LlmOptions.Default.Model),
            Endpoint = args.Text("endpoint", LlmOptions.Default.Endpoint),
        };

        string outDir = args.Text("out", DefaultOutputDirectory);
        using OllamaClient client = new(options);
        Chronicler chronicler = new(
            client,
            new RenderStore(Path.Combine(outDir, "renders.json")),
            new RenderJournal(Path.Combine(outDir, "renders.jsonl")));

        if (args.Flag("pack"))
        {
            Console.WriteLine(pack.Body);
            Console.WriteLine($"[{pack.Events.Count} events, ~{pack.TokenEstimate} prompt tokens, key {pack.Key}]");
            return 0;
        }

        Console.Error.WriteLine(
            $"rendering {pack.Kind} \"{pack.Title}\" — {pack.Events.Count} events, " +
            $"~{pack.TokenEstimate} prompt tokens, model {options.Model}…");

        RenderOutcome outcome = await chronicler.RenderAsync(pack, args.Flag("force"));

        Console.WriteLine();
        Console.WriteLine(outcome.Render.Text);
        Console.WriteLine();

        Console.Error.WriteLine(outcome.FromCache
            ? $"[cached — {pack.Key}]"
            : $"[{outcome.Render.PromptTokens} prompt + {outcome.Render.OutputTokens} output tokens " +
              $"in {outcome.Render.ElapsedMs / 1000}s]");

        FabricationReport check = outcome.Fabrication;
        if (check.Clean)
        {
            Console.Error.WriteLine($"[fabrication check: clean over {check.CheckedTokens} checked tokens]");
        }
        else
        {
            Console.Error.WriteLine($"[fabrication check: {check.Findings.Count} suspect of {check.CheckedTokens}]");
            foreach (Fabrication f in check.Findings.Take(10))
                Console.Error.WriteLine($"    {f.Kind,-6} '{f.Token}'  …{f.Context}…");
        }

        _ = view;
        return 0;
    }

    private static int CmdLog(Args args)
    {
        WorldView view = Load(args);
        Significance minimum = args.Flag("verbose") ? Significance.Bookkeeping : Significance.Minor;

        int from = args.Int("from", int.MinValue);
        int to = args.Int("to", int.MaxValue);

        bool filtering = from != int.MinValue || to != int.MaxValue;

        foreach (string line in LogFormatter.Render(view.Log, view.Seed, minimum))
        {
            // Blank lines separate years, so they must be dropped along with the years they
            // separate — otherwise a filtered range prints as a screen of nothing.
            if (line.Length == 0) { if (!filtering) Console.WriteLine(); continue; }

            if (filtering)
            {
                int year = int.Parse(line.AsSpan(2, 4), CultureInfo.InvariantCulture);
                if (year < from || year > to) continue;
            }
            Console.WriteLine(line);
        }
        return 0;
    }

    // ---- plumbing ---------------------------------------------------------

    private static WorldView Load(Args args)
    {
        string path = args.Text("file", "");
        if (path.Length == 0)
        {
            ulong seed = args.ULong("seed", 42);
            path = Path.Combine(
                args.Text("out", DefaultOutputDirectory),
                $"world-{seed.ToString(CultureInfo.InvariantCulture)}.jsonl");
        }

        // Provenance and integrity are checked on the way in by the one entry point every reader
        // goes through, so a world this build did not write says so before anything is derived
        // from it. Silent on the ordinary case: a file written by this engine under this ruleset
        // produces no notes at all.
        bool acceptNewer = args.Flag("accept-newer");
        BundleOpen opened = WorldBundle.Open(path, acceptNewer);

        foreach (string note in opened.Notes) Console.Error.WriteLine($"wb: {note}");

        // Said every time rather than once, because the override is the whole safeguard and a run
        // that used it should not read like a run that did not need it.
        if (acceptNewer && opened.Header is not null && WorldCompatibility.Check(opened.Header).Blocks)
            Console.Error.WriteLine("wb: opening it anyway, on --accept-newer.");

        return WorldView.Build(opened.Log, opened.Seed);
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"wb: {message}");
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            wb — simulated world builder (v0: no AI, no UI)

              wb run       [--seed 42] [--years 50] [--out out] [--print] [--verbose]
                           [--control flat|shuffle|redraw] [--arm war|collapse|disuse|none]
                           simulate a world and write world-<seed>.jsonl and .log

              wb log       [--seed 42] [--from Y] [--to Y] [--verbose]
                           replay the readable log

            geography — the board is imported once and stored, never generated from a seed

              wb map show  [<file>]                what a board is made of, and the separation
                                                   at which proximity reads 100
              wb map import <azgaar-export.json> [--to maps/board-1.wbmap.json]
                                                   convert an Azgaar cell export. Its output is
                                                   consumed, never embedded — no generator code
                                                   is linked or vendored
              wb map make  [--width 20] [--height 14] [--seed 1] [--to <file>]
                           a board of last resort, for a machine with no export to hand. Says so
                           in its own provenance. Create-only, like the baselines

              wb bundle write  [--seed 42] [--out out]
                           record a hash for every artefact beside the world, and a fingerprint
                           for its render cache, in the world's header
              wb bundle verify [--seed 42] [--out out]
                           check them. Opening a world does this anyway and refuses on a
                           mismatch: a map that is not the one a history happened on gives every
                           distance a different answer and nothing downstream looks wrong

            baselines — create-only, and never written to by a test layer

              wb baseline cut   --seed 42 --from <dir> --to baselines/<set>/seed-42
                                [--verification stability-anchor-only|hand-verified]
                           archive a run, hash every artefact, fingerprint the checker from what
                           git stores at the commit the artefacts name, and seal it
              wb baseline check <dir>
                           the seal against the manifest, and the manifest against the files

              wb reference [--file <world>] [--to out/carry-forward/reference-set]
                           stage what a human needs to establish a new world's reference facts: a
                           candidate sheet in the shape of §9, sixteen candidate query questions
                           with the records each answer came from, and ranked candidates for the
                           canonical withheld-not-absent case. Zero inference — no client is
                           constructed. Every page is marked machine-derived and unverified, and
                           none of it may be used as a fixture. It prepares the reading; it does
                           not perform it

            pre-verification — three instruments, upstream of any hand verification

              wb holdouts  [--set ruleset-4] [--against ruleset-3] [--seeds 7,42,…] [--to <file>]
                           where the checker's holdouts land, grouped by rule, across a baseline
                           set. Read from the stored sidecars and never recomputed: today's rules
                           over yesterday's prose give the same figure on both sides, so a rule
                           that has gone quiet agrees with the bug it exists to expose. Exits 1
                           when the pre-committed halt conditions are met

              wb seats     [--file <world>] [--lists]
                           every case where one person appears twice on one seat, classified: one
                           contested transfer (two records, one year, one hold) or a genuine second
                           tenure (different years, two holds). The ruler list cannot answer this
                           about itself — "no duplicate" holds both when a transfer collapses
                           correctly and when a tenure is deleted. Exits 1 on a repeat fitting
                           neither shape

              wb floors    [--set ruleset-5] [--seeds 7,42,…] [--to <file>]
                           how far FLOOR reaches, per rule, across a baseline set's sidecars.
                           FLOOR skips any row the baseline recorded at zero, so a rule that
                           extracted nothing has no floor to breach and can go quiet forever
                           without the golden layer noticing. Emitted rather than written down:
                           the hand-kept list of which rules those were named two that are
                           protected and missed one that is not

              wb schema    [--set ruleset-4] [--reads] [--verbose]
                           the emitter's field vocabulary, from the records rather than from a
                           declared table. --reads runs every consumer with a recorder attached
                           and resolves what they asked for against it. A verifier that reads a
                           name the engine does not write cannot fail: layer 4 read `took`,
                           `haul` and `plunder` and the engine writes `loot`. Exits 1 on a dead
                           read

            standing state — what only ever goes up, and what stops going up

              wb ties      [--set <baselines>|--file <world>] [--kind Trade] [--trajectory]
                           live ties per year per relation kind, ties made against ties ended, and
                           peak against final. Peak-against-final is not enough on its own and the
                           second figure says why: live ties over the pairs of houses that both
                           still hold ground. A world down to one house cannot hold a trade tie, so
                           "final near zero" measures hegemony there rather than any rule — and the
                           same denominator shows ruleset 5's trade graph over 100% full, holding
                           ties to houses that had ceased to exist. Exits 1 on the degeneracy guard

              wb standing  [--set <baselines>|--from <dir>] [--seeds 7,42,…] [--to <file>]
                           every piece of standing state and whether anything on the panel ever
                           made it smaller. Half the monotonic sweep, and it says which half: a
                           count cannot tell "no removal path" from "a path this panel never
                           reached", so the column is named for what was observed and the
                           classification is read off the rules beside it. Always exits 0 —
                           reporting is the deliverable, repairing is a separate decision

              wb divergence [--against ruleset-5] [--seeds 7,42,…] [--years 50]
                           where a fresh run stops matching a sealed baseline of the previous
                           ruleset, against where the new mechanic first acted. What replaces the
                           additive-only assertion once a ruleset changes worlds: divergence before
                           the first termination means something else moved the world. Exits 1 there

              wb discriminate [--against ruleset-5] [--seeds 7,42,…] [--years 50]
                           one arm per termination rule, so an effect can be attributed to a rule
                           rather than to the three that shipped together. The null arm is the
                           check on the instrument and not a result: switching all three off must
                           give back the previous ruleset's log event for event, or the switch
                           moved the world and every attribution made with it is void. Exits 1
                           there. `wb run --arm war|collapse|disuse|none` writes one to disk; it is
                           marked in the header and `wb baseline cut` refuses it

              wb keyshift  [--set ruleset-6] [--seeds 7,42,…]
                           what one inserted event costs the render cache. Event.Key is documented
                           as surviving a shift in log position; it survives a retcon and not an
                           insertion, because it takes the emission's sequence within its year.
                           Exits 1 if the measurement cannot reproduce the stored keys it depends on

              wb why       e:1188 [--depth 8]      what caused this
              wb what      e:1188 [--depth 4]      what this went on to cause
              wb who       a:112                   an actor's ties and timeline
              wb faction   f:2                     a faction's ledger and history
              wb atlas                             the map as it stands
              wb stats                             density, concentration, causality, gate check
              wb audit     [--skew 60]             repetition, dead-end mechanics, chain quality
              wb chains    [--top 3]               the deepest causal chains, in full

            v1 — prose (needs Ollama at localhost:11434)
              wb render    e:415                   one event as one or two sentences
              wb chronicle --year 24               a year as a chronicle entry
              wb chronicle --reign a:12            one ruler's time in power
                           [--faction f:5]         which seat, where they held more than one
              wb chronicle --arc w:3               a war, famine or conspiracy
              wb chronicle --faction f:2           a power's rise and fall
              wb chronicle --chain e:415           a causal chain as continuous history
              wb book      [--wars 2] [--factions 2|all] [--reigns 2] [--power f:4,f:5]
                           render a whole world into out/chronicle-<seed>.md
                           [--check-only]          re-check an existing render cache and write
                                                   the chronicle and its findings sidecar from
                                                   it; constructs no client, so a cache miss
                                                   fails rather than generating
              wb ask       "why did the Wurn League collapse?" [--show]
                           natural language over the log, answered from retrieved records

              add --pack to see the exact input without calling the model,
              --force to re-render over the cache, --model <tag> to swap models.

            checks — five layers, each runnable alone (none of them call the model)

              wb verify    out/chronicle-42.md     tier 1 over a finished document
                           reports what each rule read, not only what it found;
                           the .tier1.json sidecar carries the counts per section
              wb test      dynamics                the world's own metrics, across five seeds
              wb test      checker                 the rules, on synthetic prose (under dotnet test)
              wb test      corpus                  every fabrication found by hand, as a test
              wb test      chronicle [--seed 42]   a chronicle re-derived from the log
              wb test      golden [--accept]       this render against the baseline: figures, and
                                                   coverage-sound — extracted == checked +
                                                   unresolvable, and extracted >= the floor
                           [--rebaseline --why "…"]
                                                   lower the floor deliberately; --accept alone
                                                   refuses to and says where
                           [--baseline <dir>] [--current <dir>]
                                                   diff two sidecars against a sealed baseline.
                                                   Never writes to one: a sealed baseline is
                                                   create-only, and re-baselining means moving
                                                   the directory aside by hand
              wb test      all                     everything that needs no test host

            layer 4 lives in tests/WorldBuilder.Chronicle.Tests and runs under dotnet test. It
            verifies a chronicle against the record from an assembly that does not reference
            WorldBuilder.Inference, so it cannot come to share an implementation with the
            checker it exists to check. `wb test chronicle` is the in-process audit and is not
            that independent verifier.

            Query commands read out/world-<seed>.jsonl; pass --file to point elsewhere.

            provenance — a world file records the engine and ruleset that wrote it

              a world from an older engine, or one with no provenance at all, opens and
              says so. A changed ruleset opens too: the log is complete, and what it has
              lost is only the ability to be rebuilt from its seed. A world written by a
              newer engine than this build refuses to open, because an older reader
              cannot know what it is failing to understand.

              add --accept-newer to open one anyway.
            """);
    }
}

/// <summary>Minimal argument parsing: <c>--key value</c>, <c>--flag</c>, and positionals.</summary>
internal sealed class Args
{
    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly List<string> _positional = [];

    public static Args Parse(string[] args)
    {
        Args parsed = new();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal)) { parsed._positional.Add(arg); continue; }

            string key = arg[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                parsed._options[key] = args[++i];
            else
                parsed._flags.Add(key);
        }

        return parsed;
    }

    public string? Positional(int index) => index < _positional.Count ? _positional[index] : null;

    public bool Flag(string name) => _flags.Contains(name);

    public string Text(string name, string fallback) =>
        _options.TryGetValue(name, out string? value) ? value : fallback;

    public int Int(string name, int fallback) =>
        _options.TryGetValue(name, out string? value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed : fallback;

    public ulong ULong(string name, ulong fallback) =>
        _options.TryGetValue(name, out string? value)
        && ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong parsed)
            ? parsed : fallback;
}
