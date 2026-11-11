using MEP_Compare_Project_ver1.Models;
using MEP_Compare_Project_ver1.Matching;
using MEP_Compare_Project_ver1.Comparison;

internal static class PairingChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var g=new AxisGeometry(new(0,0,0),new(0,0,2000),Shape.Round,100,100,new(1,0,0),"Synthetic vertical pipe");
        var o=new RunOptions(1,true,true,Scope.WholeModel,"test",1,1,1,0.1);
        ElementData E(long id,string guid,AxisGeometry? geometry,bool link=false,string category="Pipes")=>new(id,"Synthetic",category,guid,"tag",geometry,"synthetic unsupported",link);
        AxisGeometry Shift(Vec offset)=>g with { Start=g.Start+offset,End=g.End+offset };
        IReadOnlyList<MatchItem> Match(ElementData[] h,ElementData[] l,RunOptions? options=null)=>PairingService.Match(h,l,true,options??o);
        var unique=Match([E(1,"",g)],[E(2,"",Shift(new(618.5,0,0)),true)]);
        check(unique.Count==1 && unique[0] is { Status:"Paired",Method:"Geometry" } && unique[0].Link?.Id==2,"geometry pairs GUID-less vertical pipe at 618.5 mm");
        var row=ComparisonService.Compare(unique[0],o);
        check(row.Status.Contains("Position") && row.MatchMethod=="Geometry" && row.DeltaXmm==618.5,"geometry search radius does not become comparison tolerance");
        check(Match([E(1,"A",g)],[E(2,"B",g,true)]).Single().Method=="Geometry","unrelated exporter GUIDs can use geometry when no shared identifier exists");
        var priority=Match([E(1,"A",g),E(3,"",g)],[E(2,"A",Shift(new(5000,0,0)),true),E(4,"",g,true)]);
        check(priority.Single(p=>p.Host?.Id==1) is { Method:"GUID",Link.Id:2 } && priority.Single(p=>p.Host?.Id==3).Link?.Id==4,"GUID pair reserved before geometry even beyond search radius");
        var locked=Match([E(1,"A",g),E(3,"",g)],[E(2,"A",g,true)]);
        check(locked.Single(p=>p.Host?.Id==3).Link is null,"geometry cannot steal a GUID-reserved linked element");
        var duplicate=Match([E(1,"A",g),E(3,"A",Shift(new(10000,0,0)))],[E(2,"A",g,true),E(4,"",g,true)]);
        check(duplicate.Where(p=>p.Host is not null).All(p=>p.Status==PairingService.ConfirmationStatus && p.Link is null),"duplicate host GUID never bypassed by geometry");
        var dupLinks=Match([E(1,"",g)],[E(2,"Z",g,true),E(4,"Z",Shift(new(10000,0,0)),true)]);
        check(dupLinks.All(p=>p.Status!="Paired"),"duplicate linked GUID excluded from geometry fallback");
        var multiple=Match([E(1,"",g)],[E(2,"",g,true),E(4,"",Shift(new(500,0,0)),true)]);
        var ambiguous=multiple.Single(p=>p.Host is not null);
        check(ambiguous.Status==PairingService.ConfirmationStatus && ambiguous.Link is null && ambiguous.CandidateLinkedIds=="2,4","two candidates stay ambiguous even when one is exact and nearer");
        var ambiguousRow=ComparisonService.Compare(ambiguous,o);
        check(ambiguousRow.LinkedId is null && ambiguousRow.DeltaXmm is null && ambiguousRow.CandidateLinkedIds=="2,4","unconfirmed candidate has no fabricated comparison errors");
        var competition=Match([E(1,"",g),E(3,"",Shift(new(900,0,0)))],[E(2,"",g,true)]);
        check(competition.Where(p=>p.Host is not null).All(p=>p.Status==PairingService.ConfirmationStatus && p.Link is null),"two hosts competing for one IFC both require confirmation");
        var chain=Match([E(1,"",g),E(3,"",Shift(new(1500,0,0)))],[E(2,"",Shift(new(700,0,0)),true),E(4,"",Shift(new(1800,0,0)),true)]);
        check(chain.Where(p=>p.Host is not null).All(p=>p.Status==PairingService.ConfirmationStatus),"candidate chain never greedily resolves by processing order");
        string Fingerprint(IEnumerable<MatchItem> rows)=>string.Join(";",rows.OrderBy(r=>r.Host?.Id).ThenBy(r=>r.Link?.Id).Select(r=>$"{r.Host?.Id}|{r.Link?.Id}|{r.Status}|{r.CandidateLinkedIds}"));
        var reversed=Match([E(3,"",Shift(new(1500,0,0))),E(1,"",g)],[E(4,"",Shift(new(1800,0,0)),true),E(2,"",Shift(new(700,0,0)),true)]);
        check(Fingerprint(chain)==Fingerprint(reversed),"pairing decisions independent of host/link enumeration order");
        check(Match([E(1,"",g)],[E(2,"",Shift(new(1000,0,0)),true)]).Single().Status=="Paired","search distance boundary included");
        check(Match([E(1,"",g)],[E(2,"",Shift(new(1000.01,0,0)),true)]).All(p=>p.Status!="Paired"),"outside search distance never auto paired");
        check(Match([E(1,"",g)],[E(2,"",g with { WidthMm=111 },true)]).All(p=>p.Status!="Paired"),"section search limit enforced");
        check(Match([E(1,"",g)],[E(2,"",g with { End=new(0,0,2200) },true)]).All(p=>p.Status!="Paired"),"length search limit enforced");
        check(Match([E(1,"",g)],[E(2,"",g with { End=new(400,0,2000) },true)]).All(p=>p.Status!="Paired"),"axis angle search limit enforced");
        check(Match([E(1,"",g)],[E(2,"",g,true,"Ducts")]).All(p=>p.Status!="Paired"),"category mismatch excluded from candidates");
        check(Match([E(1,"",g)],[E(2,"",g with { Shape=Shape.Rectangular },true)]).All(p=>p.Status!="Paired"),"shape mismatch excluded from candidates");
        check(Match([E(1,"",g)],[E(2,"",g with { Start=g.End,End=g.Start },true)]).Single().Status=="Paired","geometry handles reversed vertical endpoints");
        var rect=g with { Shape=Shape.Rectangular,WidthMm=400,HeightMm=200 };
        check(Match([E(1,"",rect)],[E(2,"",rect with { WidthMm=200,HeightMm=400,SectionX=new(0,1,0) },true)]).Single().Status=="Paired","rectangular swapped dimensions accepted for candidate search");
        check(Match([E(1,"",null)],[E(2,"",g,true)]).Single(p=>p.Host is not null).Status=="GeometryUnsupported","unsupported host geometry cannot be guessed");
        check(Match([E(1,"",g)],[E(2,"",null,true)]).All(p=>p.Status!="Paired"),"unsupported IFC geometry cannot be guessed");
        check(Match([E(1,"",g)],[E(2,"",g with { Start=new(double.NaN,0,0) },true)]).All(p=>p.Status!="Paired"),"invalid numeric geometry excluded");
        check(Match([E(1,"",g)],[E(2,"",g,true)],o with { GeometryFallback=false }).All(p=>p.Status!="Paired"),"fallback can be disabled");
        check(Match([E(1,"A",g)],[E(2,"A",g,true)],o with { GeometryFallback=false }).Single().Method=="GUID","disabling fallback preserves GUID matching");
        check(PairingService.Match([E(1,"",g)],[E(2,"",g,true)],false,o).Single().Status=="Paired","scoped report can geometry pair without linked-only output");
        check(PairingService.Match([E(1,"",g),E(3,"",g)],[E(2,"",g,true)],false,o).Where(p=>p.Host?.Id==1).Single().Status==PairingService.ConfirmationStatus,"full host context preserves off-scope competition before report filtering");
        var bad=o with { SearchDistanceMm=double.NaN };
        bool rejected=false;try { Match([E(1,"",g)],[E(2,"",g,true)],bad); } catch(ArgumentException) { rejected=true; }
        check(rejected,"invalid search configuration rejected");
    }
}
