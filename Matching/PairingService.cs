using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.Matching;

public static class PairingService
{
    public const string ConfirmationStatus = "Cần xác nhận cặp";
    private sealed record Candidate(ElementData Host, ElementData Link, double Distance, double Size, double Length, double Angle)
    {
        public string Description => FormattableString.Invariant($"Linked {Link.Id}: tâm {Distance:F2} mm, tiết diện {Size:F2} mm, chiều dài {Length:F2} mm, góc {Angle:F3}°");
    }

    // Supply the full host context even for a scoped report: hidden/unselected hosts
    // still reserve GUIDs and compete for geometric candidates.
    public static IReadOnlyList<MatchItem> Match(IReadOnlyList<ElementData> hosts, IReadOnlyList<ElementData> links, bool includeLinkOnly, RunOptions options)
    {
        var guidRows=GuidMatcher.Match(hosts,links,false);
        if(!options.GeometryFallback)
            return GuidMatcher.Match(hosts,links,includeLinkOnly).Select(r=>r.Status=="DuplicateGuid"
                ? r with { Status=ConfirmationStatus, Notes="DuplicateGuid. "+r.Notes } : r).ToList();
        Validate(options);
        var hostGroups=hosts.Where(h=>GuidMatcher.Key(h.Guid)!="").GroupBy(h=>GuidMatcher.Key(h.Guid),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.Ordinal);
        var linkGroups=links.Where(l=>GuidMatcher.Key(l.Guid)!="").GroupBy(l=>GuidMatcher.Key(l.Guid),StringComparer.Ordinal).ToDictionary(g=>g.Key,g=>g.ToList(),StringComparer.Ordinal);
        var used=guidRows.Where(r=>r.Status=="Paired").Select(r=>r.Link!.Id).ToHashSet();
        bool Blocked(ElementData l) => GuidMatcher.Key(l.Guid) is var key && key!="" &&
            (linkGroups[key].Count>1 || hostGroups.TryGetValue(key,out var sameHosts) && sameHosts.Count>1);
        var blocked=links.Where(Blocked).Select(l=>l.Id).ToHashSet();
        var available=links.Where(l=>!used.Contains(l.Id) && !blocked.Contains(l.Id) && Usable(l.Geometry)).ToList();
        var eligible=guidRows.Where(r=>r.Status is "MissingSourceGuid" or "MissingInIfc").Select(r=>r.Host!).ToList();
        // Sort candidates by X for bounded spatial lookup; do not rank-and-pick nearest.
        var buckets=available.GroupBy(l=>(l.Category,l.Geometry!.Shape)).ToDictionary(g=>g.Key,g=>g.OrderBy(l=>l.Geometry!.Center.X).ToList());
        var byHost=eligible.ToDictionary(h=>h.Id,_=>new List<Candidate>());
        var byLink=new Dictionary<long,List<Candidate>>();
        foreach(var host in eligible.Where(h=>Usable(h.Geometry)))
        {
            var a=host.Geometry!;
            if(!buckets.TryGetValue((host.Category,a.Shape),out var bucket)) continue;
            double min=a.Center.X-options.SearchDistanceMm, max=a.Center.X+options.SearchDistanceMm;
            int lo=0,hi=bucket.Count;
            while(lo<hi) { int mid=lo+(hi-lo)/2;if(bucket[mid].Geometry!.Center.X<min) lo=mid+1;else hi=mid; }
            for(int index=lo;index<bucket.Count && bucket[index].Geometry!.Center.X<=max;index++)
            {
                var target=bucket[index];var b=target.Geometry!;
                double distance=(a.Center-b.Center).Length;
                if(distance>options.SearchDistanceMm) continue;
                double length=Math.Abs(a.Length-b.Length);
                if(length>options.SearchLengthMm) continue;
                double angle=Vec.Angle(a.Direction,b.Direction);
                if(angle>options.SearchAngleDegrees) continue;
                double size=a.Shape==Shape.Round ? Math.Abs(a.WidthMm-b.WidthMm) : Math.Min(
                    Math.Max(Math.Abs(a.WidthMm-b.WidthMm),Math.Abs(a.HeightMm-b.HeightMm)),
                    Math.Max(Math.Abs(a.WidthMm-b.HeightMm),Math.Abs(a.HeightMm-b.WidthMm)));
                if(size>options.SearchSizeMm) continue;
                var candidate=new Candidate(host,target,distance,size,length,angle);
                byHost[host.Id].Add(candidate);
                if(!byLink.TryGetValue(target.Id,out var reverse)) byLink[target.Id]=reverse=[];
                reverse.Add(candidate);
            }
        }
        var result=new List<MatchItem>();
        foreach(var row in guidRows)
        {
            if(row.Status=="Paired") { result.Add(row with { Method="GUID" });continue; }
            if(row.Status=="DuplicateGuid")
            {
                string key=GuidMatcher.Key(row.Host!.Guid);
                string ids=linkGroups.TryGetValue(key,out var targets)?Ids(targets.Select(t=>t.Id)):"";
                result.Add(row with { Status=ConfirmationStatus,Notes="DuplicateGuid. "+row.Notes,Method="GUID",CandidateLinkedIds=ids });continue;
            }
            var host=row.Host!;
            if(!Usable(host.Geometry))
            { result.Add(row with { Status="GeometryUnsupported",Method="Geometry",Notes=row.Notes+" Không thể tìm cặp hình học: "+host.GeometryIssue });continue; }
            var candidates=byHost[host.Id].OrderBy(c=>c.Link.Id).ToList();
            // Conservative bidirectional uniqueness. Never resolve a competing component
            // by enumeration order, nearest distance, score tie-breaking or greedy removal.
            if(candidates.Count==1 && byLink[candidates[0].Link.Id].Count==1)
            {
                var chosen=candidates[0];used.Add(chosen.Link.Id);
                result.Add(new(host,chosen.Link,"Paired",row.Notes+" Ghép hình học duy nhất hai chiều; "+chosen.Description)
                    { Method="Geometry",CandidateLinkedIds=Ids([chosen.Link.Id]) });
            }
            else if(candidates.Count>0)
            {
                string competitors=Ids(candidates.SelectMany(c=>byLink[c.Link.Id]).Select(c=>c.Host.Id));
                result.Add(new(host,null,ConfirmationStatus,$"{row.Notes} Host cạnh tranh: {competitors}. "+string.Join("; ",candidates.Select(c=>c.Description)))
                    { Method="Geometry",CandidateLinkedIds=Ids(candidates.Select(c=>c.Link.Id)) });
            }
            else result.Add(row with { Status="NoGeometryCandidate",Method="Geometry",Notes=row.Notes+" Không có ứng viên trong giới hạn tìm cặp; không kết luận là khớp hoặc lệch." });
        }
        if(includeLinkOnly)
            foreach(var link in links.Where(l=>!used.Contains(l.Id)))
            {
                if(blocked.Contains(link.Id))
                {
                    string key=GuidMatcher.Key(link.Guid);
                    string competitors=hostGroups.TryGetValue(key,out var hh)?Ids(hh.Select(h=>h.Id)):"";
                    result.Add(new(null,link,ConfirmationStatus,$"DuplicateGuid. Host IDs: {competitors}; Linked IDs: {Ids(linkGroups[key].Select(l=>l.Id))}") { Method="GUID",CandidateLinkedIds=Ids(linkGroups[key].Select(l=>l.Id)) });
                }
                else if(byLink.TryGetValue(link.Id,out var candidates))
                    result.Add(new(null,link,ConfirmationStatus,$"Ứng viên hình học chưa ghép. Host IDs: {Ids(candidates.Select(c=>c.Host.Id))}") { Method="Geometry",CandidateLinkedIds=Ids([link.Id]) });
                else if(!Usable(link.Geometry)) result.Add(new(null,link,"GeometryUnsupported","Không thể tìm cặp hình học: "+link.GeometryIssue) { Method="Geometry" });
                else result.Add(new(null,link,"OnlyInIfc","Không tìm thấy host tương ứng.") { Method="",CandidateLinkedIds="" });
            }
        return result;
    }
    private static string Ids(IEnumerable<long> ids) => string.Join(",",ids.Distinct().OrderBy(id=>id));
    private static bool Usable(AxisGeometry? g) => g is not null &&
        new[]{g.Start.X,g.Start.Y,g.Start.Z,g.End.X,g.End.Y,g.End.Z,g.WidthMm,g.HeightMm,g.Length}.All(double.IsFinite)
        && g.Length>1e-8 && g.WidthMm>0 && g.HeightMm>0;
    private static void Validate(RunOptions o)
    {
        if(new[]{o.SearchDistanceMm,o.SearchLengthMm,o.SearchSizeMm,o.SearchAngleDegrees}.Any(v=>!double.IsFinite(v)||v<0) || o.SearchAngleDegrees>90)
            throw new ArgumentException("Giới hạn tìm cặp phải hữu hạn, ≥ 0; góc ≤ 90°.");
    }
}
