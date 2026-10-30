using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using HoldDraft;
using RimWorld;
using Verse;

static class Validate
{
    static int checks;
    static int nextId;
    static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static void Check(bool b,string s){if(!b)throw new Exception(s);checks++;}
    static readonly System.Reflection.FieldInfo drafted=AccessTools.Field(typeof(Pawn_DraftController),"draftedInt");
    static readonly System.Reflection.FieldInfo tick=AccessTools.Field(typeof(TickManager),"ticksGameInt");
    static Pawn Pawn(bool player=true)
    {
        var p=Blank<Pawn>(); p.thingIDNumber=++nextId; p.def=new ThingDef{race=new RaceProperties{intelligence=Intelligence.Humanlike}};
        p.health=Blank<Pawn_HealthTracker>();p.drafter=Blank<Pawn_DraftController>();p.drafter.pawn=p;
        AccessTools.Field(typeof(Pawn_HealthTracker),"healthState").SetValue(p.health,PawnHealthState.Mobile);
        AccessTools.Field(typeof(Thing),"factionInt").SetValue(p,new Faction{def=new FactionDef{isPlayer=player}});
        AccessTools.Field(typeof(Thing),"mapIndexOrState").SetValue(p,(sbyte)0);
        drafted.SetValue(p.drafter,true);return p;
    }
    static object Bench(string name,int count,Action action)
    {
        for(int i=0;i<1000;i++)action();
        long start=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();
        long watch=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<count;i++)action();sw.Stop();
        long allocated=GC.GetAllocatedBytesForCurrentThread()-watch;
        Check(allocated==0,name+" steady-state allocation must be zero");
        return new{name,iterations=count,milliseconds=sw.Elapsed.TotalMilliseconds,allocatedBytes=allocated};
    }
    static int Main(string[] args)
    {
        try
        {
            var game=Blank<Game>(); game.tickManager=Blank<TickManager>();
            AccessTools.Field(typeof(Game),"maps").SetValue(game,new List<Map>{Blank<Map>()});
            var monitor=new DraftMonitor(game);AccessTools.Field(typeof(Game),"components").SetValue(game,new List<GameComponent>{monitor});
            Current.Game=game;DraftMod.Settings=new DraftSettings();
            Check(!DraftMod.Settings.showAutoUndraftButton&&DraftMod.Settings.warningHours==24,"Default button hidden and warning 24 hours");
            var p=Pawn();bool result=true;
            Check(!DraftHoldPatch.Prefix(p,ref result)&&!result,"Normal player draft stays without extra toggle");
            DraftState.Raw(p).allowAutoUndraft=true;result=true;
            Check(DraftHoldPatch.Prefix(p,ref result)&&result,"Explicit opt-out delegates to vanilla");
            result=true;Check(DraftHoldPatch.Prefix(Pawn(false),ref result),"NPC keeps vanilla behavior");
            DraftState.Raw(p).allowAutoUndraft=false;tick.SetValue(game.tickManager,100);monitor.Observe(p);
            Check(DraftState.Raw(p).draftedSince==100&&monitor.TrackedCount==1,$"Draft event starts timer: since={DraftState.Raw(p).draftedSince}, tracked={monitor.TrackedCount}, drafted={p.Drafted}, dead={p.Dead}, eligible={DraftMonitor.Eligible(p)}, human={p.RaceProps.Humanlike}");
            tick.SetValue(game.tickManager,60099);monitor.Refresh();Check(!monitor.Report.active,"No warning before 24 game hours");
            tick.SetValue(game.tickManager,60100);monitor.Refresh();Check(monitor.Report.active&&monitor.Overdue.Count==1,"Warning at exactly 24 game hours");
            monitor.Observe(p);Check(DraftState.Raw(p).draftedSince==100,"Repeated observe/spawn preserves start time");
            drafted.SetValue(p.drafter,false);monitor.Observe(p);Check(!monitor.Report.active&&DraftState.Raw(p).draftedSince==-1,"Manual/forced undraft clears alert immediately");
            drafted.SetValue(p.drafter,true);monitor.Observe(p);Check(DraftState.Raw(p).draftedSince==60100,"Redraft starts a fresh timer");
            var h=new Harmony("staydrafted.validation");
            foreach(var type in typeof(DraftMod).Assembly.GetTypes().Where(t=>t.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0))h.CreateClassProcessor(type).Patch();
            Check(Harmony.GetPatchInfo(AccessTools.PropertySetter(typeof(Pawn_DraftController),nameof(Pawn_DraftController.Drafted))).Postfixes.Any(x=>x.owner==h.Id),"Draft setter observer emits");h.UnpatchAll(h.Id);
            Check(!DraftState.Overdue(60000,-1,24)&&DraftState.Overdue(60000,0,24),"Timestamp boundaries");
            // The monitor only sees registered drafted pawns, regardless of total colony size.
            var hugeColony=Enumerable.Range(0,10000).Select(_=>{var pawn=Pawn();drafted.SetValue(pawn.drafter,false);return pawn;}).ToArray();
            monitor.Refresh();Check(monitor.LastVisitCount==1,"10,000 other pawns do not enter the tracked scan");
            foreach(var pawn in hugeColony.Take(9)){drafted.SetValue(pawn.drafter,true);monitor.Observe(pawn);}
            monitor.Refresh();Check(monitor.LastVisitCount==10,"Only the 10 registered drafted pawns are visited");
            tick.SetValue(game.tickManager,60101);
            var samples=new List<object>{Bench("idle tick: 10 drafted, 10,000 other pawns",1000000,monitor.GameComponentTick),Bench("cached alert report",1000000,()=>{var r=monitor.Report;if(r.active)throw new Exception("Unexpected overdue");})};
            samples.Add(Bench("600-tick scan: 10 tracked",10000,monitor.Refresh));
            foreach(var pawn in hugeColony){drafted.SetValue(pawn.drafter,true);monitor.Observe(pawn);}monitor.Refresh();
            Check(monitor.LastVisitCount==10001,"Stress scan visits each tracked pawn once");
            samples.Add(Bench("stress scan: 10,001 tracked",100,monitor.Refresh));
            var path=Path.Combine(args[0],"Hold-Draft/Tests/performance-result.json");
            File.WriteAllText(path,JsonSerializer.Serialize(new{checks,intervalTicks=DraftMonitor.CheckInterval,samples},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("PASS: "+checks+" draft policy, alert lifecycle, actual Harmony and allocation/performance checks\n"+File.ReadAllText(path));return 0;
        }
        catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
