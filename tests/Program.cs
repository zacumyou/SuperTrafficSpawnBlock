using SuperTrafficSpawnBlock;
AdjacentRoadGeometryTests.Run();
SpawnFootprintTests.Run();

var plan = new DispatchPlan<int>();
var ready = new HashSet<int> { 11, 22 };
var output = new List<int>();
foreach (int factor in new[] { 1, 2, 5, 10, 20, 50 })
{
    // Two valid paths, a pending request, and a duplicate entry from an interrupted tick.
    plan.Build(new[] { 11, 99, 22, 11 }, ready, factor, output);
    Check(output.Count(x => x == 11) == factor && output.Count(x => x == 22) == factor, "exact per-path multiplier");
    Check(output.Count(x => x == 99) == 1, "pending/unrelated dispatch preserved");
    var first = output.ToArray();
    plan.Build(first, ready, factor, output);
    Check(output.SequenceEqual(first), "idempotent repeated processing");
}
plan.Build(output.ToArray(), ready, 2, output);
Check(output.Count == 5 && output.Count(x => x == 11) == 2, "lowering multiplier shrinks pending copies");
plan.Build(new[] { 11, 99, 99 }, new HashSet<int>(), 50, output);
Check(output.SequenceEqual(new[] { 11, 99, 99 }), "failed paths and unrelated dispatches stay untouched");
foreach (int invalid in new[] { -1, 0, 3, int.MaxValue })
{
    plan.Build(new[] { 11 }, ready, invalid, output);
    Check(output.Count == 20, "invalid saved settings have bounded default");
}
Console.WriteLine("PASS: all six multipliers, repeated ticks, lowering multiplier, unrelated/failed requests, invalid saved settings.");
foreach (bool allow in new[] { false, true })
{
    Check(TrafficPolicy.Reject(false, true, false, allow), "ordinary spawner cannot send a car to Super");
    Check(TrafficPolicy.Reject(true, true, true, allow), "Super-to-Super route is rejected with either toggle");
    Check(!TrafficPolicy.Reject(false, false, true, allow), "ordinary truck routes remain unchanged");
    Check(!TrafficPolicy.Reject(true, false, false, allow), "outgoing passenger requests remain enabled");
    Check(TrafficPolicy.Reject(true, false, true, allow) == !allow, "outgoing heavy vehicle toggle");
}
Console.WriteLine("PASS: outbound-only route policy, heavy toggle, ordinary traffic isolation.");
var selector = new ConnectedLaneSelector<string, int, int>();
var lanes = new List<LaneExit<int, int>> { new(101, 1), new(102, 2), new(103, 3), new(104, 4) };
var exits = new HashSet<int> { 1, 2, 3 };
var counts = new Dictionary<int, int>();
for (int i = 0; i < 20; i++)
{
    Check(selector.TryChoose("block-a", lanes, exits, out int selected), "multiple connected lanes accepted");
    Check(selected != 104, "disconnected lane excluded");
    counts[selected] = counts.GetValueOrDefault(selected) + 1;
}
Check(counts.Values.Max() - counts.Values.Min() <= 1 && counts.Count == 3, "20 vehicles balanced over three lanes");
Check(selector.TryChoose("block-b", lanes, exits, out int independent) && independent == 101, "independent block rotation");
Check(!selector.TryChoose("block-a", lanes, new HashSet<int> { 4 }, out _), "single valid lane preserves original path");
Check(!selector.TryChoose("block-a", lanes, new HashSet<int>(), out _), "no connection preserves original path");
for (int i = 0; i < 4; i++)
    Check(selector.TryChoose("block-a", lanes, new HashSet<int> { 2, 3 }, out int selected) && (selected == 102 || selected == 103), "changed continuation excludes former lanes");
selector.Clear();
Check(selector.TryChoose("block-a", lanes, exits, out int reset) && reset == 101, "new city resets rotation");
Check(DepartureRange.IsFullForwardExit(.25f, 1, 0, 1), "forward partial start with full exit");
Check(!DepartureRange.IsFullForwardExit(1, 0, 0, 1), "reverse traversal rejected");
Check(!DepartureRange.IsFullForwardExit(0, .5f, 0, 1), "mid-segment exit rejected");
Check(!DepartureRange.IsFullForwardExit(0, 1, .5f, 1), "mid-segment continuation rejected");
Check(!DepartureRange.IsFullForwardExit(float.NaN, 1, 0, 1), "invalid curve input rejected");
Console.WriteLine("PASS: lane distribution, disconnected exits, independent blocks, route changes and traversal guards.");
static void Check(bool result, string label) { if (!result) throw new Exception(label); }
foreach (int factor in new[] { 1, 2, 5, 10, 20, 50 })
{
    Check(SpawnPacing.Budget(factor, 0) == factor, "steady window budget");
    int limit = Math.Max(8, factor * 2);
    Check(SpawnPacing.Budget(factor, limit) == 0, "congestion stops growth");
    Check(SpawnPacing.Budget(factor, limit - 1) == 1, "bounded remaining capacity");
    Check(SpawnPacing.Budget(factor, 0) == factor, "no catch-up debt after congestion");
    var delays = Enumerable.Range(0, factor).Select(i => SpawnPacing.Delay(i, factor)).ToArray();
    Check(delays[0] == 0 && delays.Last() < 256, "departure window bounds");
    Check(delays.SequenceEqual(delays.OrderBy(x => x)), "ordered departures");
    Check(delays.Distinct().Count() == factor, "no randomized duplicate delays");
}
Console.WriteLine("PASS: continuous window budgets, bounded queues, no catch-up burst and evenly spaced departures.");
var gate = new CongestionGate<string>();
Check(!gate.ShouldPause("a", 20, 20, 0), "scheduled wait is not congestion");
Check(!gate.ShouldPause("a", 20, 10, 4), "below blocked threshold");
Check(gate.ShouldPause("a", 20, 10, 5), "five overdue departures pause default block");
Check(!gate.ShouldPause("b", 20, 0, 0), "other block remains active");
Check(gate.ShouldPause("a", 20, 6, 0), "hysteresis waits for queue drainage");
Check(gate.ShouldPause("a", 20, 2, 1), "still overdue remains paused");
Check(!gate.ShouldPause("a", 20, 5, 0), "clear overdue and low queue resumes");
Check(gate.ShouldPause("a", 1, 2, 2), "low multiplier can pause");
gate.Retain(new HashSet<string> { "b" });
Check(!gate.ShouldPause("a", 20, 10, 1), "removed block clears pause");
gate.ShouldPause("a", 20, 20, 5);
gate.Clear();
Check(!gate.ShouldPause("a", 20, 10, 1), "preload clears pause");
Console.WriteLine("PASS: congestion pause, hysteresis, independent sources and lifecycle cleanup.");
Check(BlockVariants.Factors.SequenceEqual(new[] { 1, 2, 5, 10, 20, 50 }), "six requested variants");
Check(BlockVariants.Factors.Select(BlockVariants.PrefabName).Distinct().Count() == 6, "unique prefab identities");
Check(BlockVariants.PrefabName(20) == "Super Traffic Spawn Block", "existing save identity preserved as 20x");
Check(BlockVariants.Factors.All(f => BlockVariants.DisplayName(f).EndsWith(f + "x")), "all variant labels include rate");
Console.WriteLine("PASS: six variant identities, visible rates and legacy 20x identity.");
Check(DestinationRules.Matches(0, 30, 0, true, true), "unassigned uses outside connection");
Check(!DestinationRules.Matches(0, 20, 0, false, true), "unassigned rejects internal random destinations");
Check(DestinationRules.Matches(20, 20, 0, false, true), "assigned sink accepts exact destination");
Check(!DestinationRules.Matches(20, 30, 0, true, true), "assigned sink does not silently fallback outside");
Check(!DestinationRules.Matches(21, 20, 0, false, true), "late result for former sink rejected");
Check(!DestinationRules.Matches(20, 20, 0, false, false), "demolished destination rejected");
Console.WriteLine("PASS: external-only default, exact sink routing, changed selection and demolished destination rejection.");
var distributionRandom = new Random(1977);
foreach (int destinations in new[] { 1, 2, 3, 7 })
{
    var allocations = DestinationAllocation.Allocate(100000, destinations, distributionRandom.Next);
    Check(allocations.Sum() == 100000, "total spawn budget preserved across destinations");
    Check(allocations.All(n => Math.Abs(n - 100000.0 / destinations) < 900), "uniform per-vehicle probability");
}
int drawIndex = 0;
Check(DestinationAllocation.Allocate(4, 2, n => (drawIndex++ < 3 ? 0 : 1)).SequenceEqual(new[] { 3, 1 }), "random assignment is per vehicle, not per batch or forced 50/50 count");
Check(DestinationAllocation.Allocate(0, 3, n => throw new Exception("unexpected draw")).Sum() == 0, "zero budget does not draw");
Console.WriteLine("PASS: equal destination probability, per-vehicle draws, total budget and zero-budget behavior.");
Check(RouteRequestBudget.Available(0) == 4, "at most four new path requests per window");
Check(RouteRequestBudget.Available(15) == 1, "pending limit leaves one slot");
Check(RouteRequestBudget.Available(16) == 0 && RouteRequestBudget.Available(20) == 0, "pending cap stops new work");
Check(RouteRequestBudget.Available(12) == 4, "completed work releases capacity");
Console.WriteLine("PASS: global path request window and in-flight limits.");

var rates = new RateSchedule<int>();
Check(Enumerable.Range(0,100).Sum(_ => rates.Next(1, RateSchedule<int>.Half)) == 50, "half rate is one attempt per two windows");
Check(rates.Next(2, RateSchedule<int>.Half) == 0 && rates.Next(3, RateSchedule<int>.Half) == 0 && rates.Next(2, RateSchedule<int>.Half) == 1, "independent half rate blocks");
rates.Reset(3); Check(rates.Next(3, RateSchedule<int>.Half) == 0, "disable clears fractional budget without catch-up");
Check(rates.Next(3,3)==3 && rates.Next(3,15)==15 && rates.Next(3,RateSchedule<int>.Half)==0, "switching rates resets fractional credit");
Check(RateSchedule<int>.Allowed(-2) && RateSchedule<int>.Allowed(3) && RateSchedule<int>.Allowed(15) && !RateSchedule<int>.Allowed(0), "new rates validated");
rates.Clear(); Check(rates.Next(2, RateSchedule<int>.Half)==0, "load resets fractional credit");
Console.WriteLine("PASS: half rate pacing, 3x/15x, independent blocks, disable/rate-change/load resets.");


