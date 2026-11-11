using MEP_Compare_Project_ver1.Models;
using MEP_Compare_Project_ver1.Matching;

namespace MEP_Compare_Project_ver1.Comparison;

public static class ComparisonService
{
    public static ResultRow Compare(MatchItem pair, RunOptions options)
    {
        var h=pair.Host; var l=pair.Link;
        var row=new ResultRow { HostId=h?.Id,LinkedId=l?.Id,Category=h?.Category ?? l?.Category ?? "",Guid=h?.Guid ?? l?.Guid ?? "",
            IfcTag=l?.Tag ?? "",Status=pair.Status,Notes=pair.Notes,MatchMethod=pair.Method,CandidateLinkedIds=pair.CandidateLinkedIds,HostGeometry=h?.Geometry,LinkGeometry=l?.Geometry };
        if(pair.Status!="Paired") return row;
        if(h!.Category!=l!.Category) { row.Status="CategoryMismatch"; row.Notes=$"Host: {h.Category}; IFC: {l.Category}"; return row; }
        var a=h.Geometry; var b=l.Geometry;
        if(a is null || b is null) { row.Status="GeometryUnsupported";row.Notes=pair.Notes+$" Host: {h.GeometryIssue}; IFC: {l.GeometryIssue}";return row; }
        var delta=b.Center-a.Center;
        row.DeltaXmm=delta.X;row.DeltaYmm=delta.Y;row.DeltaZmm=delta.Z;
        var direct=Math.Max((b.Start-a.Start).Length,(b.End-a.End).Length);
        var reverse=Math.Max((b.End-a.Start).Length,(b.Start-a.End).Length);
        row.EndpointMm=Math.Min(direct,reverse);
        row.LengthMm=Math.Abs(b.Length-a.Length);
        row.AngleDeg=Vec.Angle(a.Direction,b.Direction);
        row.Notes=pair.Notes+$" Host: {a.Method}; IFC: {b.Method}. Δ = IFC − Host, tọa độ internal host, mm.";
        if(a.Shape!=b.Shape) { row.Status="SectionMismatch";return row; }
        if(a.Shape==Shape.Round) { row.SizeMm=Math.Abs(a.WidthMm-b.WidthMm); row.RollDeg=0; }
        else
        {
            var roll=Vec.Angle(a.SectionX,b.SectionX);
            var same=Math.Max(Math.Abs(a.WidthMm-b.WidthMm),Math.Abs(a.HeightMm-b.HeightMm));
            var swap=Math.Max(Math.Abs(a.WidthMm-b.HeightMm),Math.Abs(a.HeightMm-b.WidthMm));
            bool swapped = Math.Abs(90-roll)<roll;
            row.SizeMm=swapped?swap:same;row.RollDeg=swapped?Math.Abs(90-roll):roll;
        }
        var failures=new List<string>();
        if(row.EndpointMm>options.PositionMm) failures.Add("Position");
        if(row.LengthMm>options.LengthMm) failures.Add("Length");
        if(row.AngleDeg>options.AngleDegrees) failures.Add("Direction");
        if(row.SizeMm>options.SizeMm) failures.Add("Size");
        if(row.RollDeg>options.AngleDegrees) failures.Add("Roll");
        row.Status=failures.Count==0?"Match":"Different: "+string.Join(" + ",failures);
        return row;
    }
}
