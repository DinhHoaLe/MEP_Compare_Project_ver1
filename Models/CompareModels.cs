namespace MEP_Compare_Project_ver1.Models;

public readonly record struct Vec(double X, double Y, double Z)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static Vec operator -(Vec a, Vec b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static Vec operator *(Vec a, double b) => new(a.X*b,a.Y*b,a.Z*b);
    public double Dot(Vec b) => X*b.X+Y*b.Y+Z*b.Z;
    public double Length => Math.Sqrt(Dot(this));
    public Vec Unit => Length > 1e-12 ? this * (1/Length) : throw new InvalidOperationException("Zero direction");
    public static double Angle(Vec a, Vec b) => Math.Acos(Math.Clamp(Math.Abs(a.Unit.Dot(b.Unit)),0,1))*180/Math.PI;
}
public enum Scope { WholeModel, ActiveView, Selection }
public enum Shape { Round, Rectangular }
public record RunOptions(long LinkId, bool Ducts, bool Pipes, Scope Scope, string SourceParameter,
    double PositionMm, double SizeMm, double LengthMm, double AngleDegrees)
{
    public bool GeometryFallback { get; init; } = true;
    public double SearchDistanceMm { get; init; } = 1000;
    public double SearchSizeMm { get; init; } = 10;
    public double SearchLengthMm { get; init; } = 100;
    public double SearchAngleDegrees { get; init; } = 5;
}
public record AxisGeometry(Vec Start, Vec End, Shape Shape, double WidthMm, double HeightMm, Vec SectionX, string Method)
{
    public double Length => (End-Start).Length;
    public Vec Center => (Start+End)*0.5;
    public Vec Direction => (End-Start).Unit;
}
public record ElementData(long Id, string Name, string Category, string Guid, string Tag,
    AxisGeometry? Geometry, string GeometryIssue, bool Linked);
public record LinkChoice(long Id, string Name, bool Loaded, bool IsIfc)
{
    public string Display => $"{Id} | {Name}" + (!Loaded ? " [Unloaded]" : !IsIfc ? " [IFC chưa xác định]" : "");
}
public sealed class ResultRow
{
    public long? HostId { get; init; }
    public long? LinkedId { get; init; }
    public string Category { get; init; } = "";
    public string Guid { get; init; } = "";
    public string IfcTag { get; init; } = "";
    public string MatchMethod { get; init; } = "";
    public string CandidateLinkedIds { get; init; } = "";
    public string Status { get; set; } = "";
    public double? DeltaXmm { get; set; }
    public double? DeltaYmm { get; set; }
    public double? DeltaZmm { get; set; }
    public double? EndpointMm { get; set; }
    public double? LengthMm { get; set; }
    public double? AngleDeg { get; set; }
    public double? SizeMm { get; set; }
    public double? RollDeg { get; set; }
    public string Notes { get; set; } = "";
    public AxisGeometry? HostGeometry { get; init; }
    public AxisGeometry? LinkGeometry { get; init; }
}
