using System.IO;
using MEP_Compare_Project_ver1.Models;
using MEP_Compare_Project_ver1.Matching;
using MEP_Compare_Project_ver1.Comparison;

internal static class MappingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var axis=new AxisGeometry(new(0,0,0),new(2000,0,0),Shape.Round,100,100,new(0,1,0),"test");
        var h=new ElementData(1,"host","Pipes","A","",axis,"",false) { UniqueId="host-1" };
        var h2=h with { Id=2,UniqueId="host-2" };
        var l=new ElementData(11,"ifc","Pipes","A","",axis with { Start=new(500,0,0),End=new(2500,0,0) },"",true) { UniqueId="link-1" };
        var l2=l with { Id=12,UniqueId="link-2",Guid="B" };
        var context=new MappingContext("host-doc","instance","linked-doc");
        var options=new RunOptions(1,true,true,Scope.WholeModel,"test",1,1,1,0.1);
        MappingSession Session() => new(context,[h,h2],[l,l2]);
        void Reject(Action action,string name) { bool rejected=false;try { action(); } catch(InvalidOperationException) { rejected=true; }check(rejected,name); }
        var session=Session();session.Confirm(1,12);
        var rows=session.Match(true,options);
        check(rows.Single(r=>r.Host?.Id==1).Link?.Id==12 && rows.Single(r=>r.Host?.Id==1).Method=="Manual","manual mapping overrides automatic GUID and reserves IFC");
        check(rows.Count(r=>r.Link?.Id==12)==1,"manual linked element appears only once");
        var compared=ComparisonService.Compare(rows.Single(r=>r.Host?.Id==1),options);
        check(compared.Status.Contains("Position") && compared.MatchMethod=="Manual","manual confirmation still measures geometry differences");
        Reject(()=>session.Confirm(2,12),"manual mapping rejects competing host without replacing existing pair");
        check(session.Count==1,"failed confirmation leaves mapping intact");
        var folder=Path.Combine(Path.GetTempPath(),"mep-mapping-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            var path=Path.Combine(folder,"mapping.json");session.Save(path);
            check(!session.Dirty,"saving clears unsaved state");
            var loaded=Session();loaded.Load(path);
            check(loaded.Count==1 && loaded.Match(false,options).Any(p=>p.Method=="Manual" && p.Link?.Id==12),"mapping JSON round trip");
            var renumbered=new MappingSession(context,[h with { Id=101 },h2],[l,l2 with { Id=112 }]);renumbered.Load(path);
            check(renumbered.Match(false,options).Any(p=>p.Method=="Manual" && p.Host?.Id==101 && p.Link?.Id==112),"mapping uses UniqueId across numeric ID changes");
            Reject(()=>new MappingSession(context with { LinkInstance="other" },[h,h2],[l,l2]).Load(path),"mapping rejects another link instance");
            Reject(()=>new MappingSession(context with { HostDocument="other" },[h,h2],[l,l2]).Load(path),"mapping rejects another host model");
            Reject(()=>new MappingSession(context,[h,h2],[l]).Load(path),"mapping rejects removed linked element");
            File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new MappingFile(1,context,[new("host-1","link-1"),new("host-2","link-1")])));
            Reject(()=>loaded.Load(path),"mapping load rejects non-bijective pairs");
            check(loaded.Count==1 && loaded.Match(false,options).Any(p=>p.Method=="Manual" && p.Link?.Id==12),"rejected file does not change existing session");
            session.Remove(1);
            check(session.Count==0 && session.Dirty && session.Match(false,options).All(p=>p.Method!="Manual"),"removing manual pair restores automatic matching");
            var wrongCategory=new MappingSession(context,[h],[l with { Category="Ducts" }]);
            Reject(()=>wrongCategory.Confirm(1,11),"manual mapping rejects duct to pipe");
            var unsupported=new MappingSession(context,[h],[l with { Geometry=null }]);unsupported.Confirm(1,11);
            check(ComparisonService.Compare(unsupported.Match(false,options).Single(),options).Status=="GeometryUnsupported","manual pairing cannot invent unsupported geometry measurements");
        }
        finally { Directory.Delete(folder,true); }
    }
}
