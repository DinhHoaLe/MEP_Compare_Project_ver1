using System.Globalization;
using System.IO;
using System.Text;
using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.Reporting;

public static class CsvReport
{
    public static void Write(string path,IEnumerable<ResultRow> rows,RunOptions options,string document,string link)
    {
        using var writer=new StreamWriter(path,false,new UTF8Encoding(true));
        writer.WriteLine("HostId,LinkedId,Category,SourceGuid,IfcTag,Status,DeltaX_mm,DeltaY_mm,DeltaZ_mm,MaxEndpoint_mm,LengthError_mm,AxisAngle_deg,SectionError_mm,Roll_deg,Notes,Document,Link,SourceParameter,Scope,PositionTolerance_mm,SizeTolerance_mm,LengthTolerance_mm,AngleTolerance_deg,ExportedAt,MatchMethod,CandidateLinkedIds,GeometryFallback,SearchDistance_mm,SearchSize_mm,SearchLength_mm,SearchAngle_deg");
        var timestamp=DateTimeOffset.Now.ToString("O",CultureInfo.InvariantCulture);
        foreach(var r in rows)
        {
            object?[] values=[r.HostId,r.LinkedId,r.Category,r.Guid,r.IfcTag,r.Status,r.DeltaXmm,r.DeltaYmm,r.DeltaZmm,r.EndpointMm,r.LengthMm,r.AngleDeg,r.SizeMm,r.RollDeg,r.Notes,document,link,options.SourceParameter,options.Scope,options.PositionMm,options.SizeMm,options.LengthMm,options.AngleDegrees,timestamp,r.MatchMethod,r.CandidateLinkedIds,options.GeometryFallback,options.SearchDistanceMm,options.SearchSizeMm,options.SearchLengthMm,options.SearchAngleDegrees];
            writer.WriteLine(string.Join(",",values.Select(Cell)));
        }
    }
    private static string Cell(object? value)
    {
        string text=value is IFormattable f?f.ToString(null,CultureInfo.InvariantCulture):value?.ToString() ?? "";
        // Quoting alone does not prevent spreadsheet formula execution.
        if(value is string && text.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' or '\t' or '\r') text="'"+text;
        return "\""+text.Replace("\"","\"\"")+"\"";
    }
}
