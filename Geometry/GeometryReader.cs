using Autodesk.Revit.DB;
using MEP_Compare_Project_ver1.Models;

namespace MEP_Compare_Project_ver1.Geometry;

public static class GeometryReader
{
    public const double MmPerFoot=304.8;
    public static Vec Point(XYZ p) => new(p.X*MmPerFoot,p.Y*MmPerFoot,p.Z*MmPerFoot);
    public static Vec Vector(XYZ p) => new(p.X,p.Y,p.Z);
    public static AxisGeometry Read(Element e, Transform linkTransform)
    {
        if(e is MEPCurve mep)
        {
            if(e.Location is not LocationCurve { Curve: Line line }) throw new NotSupportedException("Chỉ hỗ trợ đường tâm thẳng; không xấp xỉ đường cong bằng dây cung.");
            var connectors=mep.ConnectorManager.Connectors.Cast<Connector>().Where(c=>c.ConnectorType==ConnectorType.End).ToList();
            if(connectors.Count!=2) throw new NotSupportedException("Cần đúng 2 end connectors.");
            var c=connectors[0];
            var shape=c.Shape switch { ConnectorProfileType.Round=>Shape.Round,ConnectorProfileType.Rectangular=>Shape.Rectangular,_=>throw new NotSupportedException("Tiết diện oval/khác chưa hỗ trợ.") };
            var w=shape==Shape.Round?2*c.Radius:c.Width;
            var h=shape==Shape.Round?w:c.Height;
            var end=connectors[1];
            if(c.Shape!=end.Shape || (shape==Shape.Round ? Math.Abs(end.Radius-c.Radius)>1e-6 : Math.Abs(end.Width-w)>1e-6 || Math.Abs(end.Height-h)>1e-6))
                throw new NotSupportedException("Tiết diện thay đổi dọc phần tử.");
            return Make(line.GetEndPoint(0),line.GetEndPoint(1),shape,w,h,c.CoordinateSystem.BasisX,linkTransform,"LocationCurve + end connector");
        }
        var geometry=e.get_Geometry(new Options { DetailLevel=ViewDetailLevel.Fine,IncludeNonVisibleObjects=false,ComputeReferences=false });
        if(geometry is null) throw new NotSupportedException("Không có geometry.");
        var solids=new List<Solid>();
        Collect(geometry,Transform.Identity,solids);
        if(solids.Count!=1) throw new NotSupportedException($"Cần một solid liên tục; nhận {solids.Count}. Mesh/compound không suy đoán bằng bounding box.");
        var s=solids[0];
        var faces=s.Faces.Cast<Face>().ToList();
        var cylinders=faces.OfType<CylindricalFace>().ToList();
        if(cylinders.Count>0) return Cylinder(s,faces,cylinders,linkTransform);
        return Prism(s,faces,linkTransform);
    }
    private static void Collect(GeometryElement geometry,Transform transform,List<Solid> solids)
    {
        foreach(var obj in geometry)
        {
            if(obj is Solid s && s.Volume>1e-9) solids.Add(SolidUtils.CreateTransformed(s,transform));
            else if(obj is GeometryInstance instance) Collect(instance.GetSymbolGeometry(),transform.Multiply(instance.Transform),solids);
            else if(obj is Mesh) throw new NotSupportedException("Mesh IFC chưa hỗ trợ phân tích tiết diện đáng tin cậy.");
        }
    }
    private static List<XYZ> Vertices(Solid s) => s.Edges.Cast<Edge>().SelectMany(e=>e.Tessellate()).ToList();
    private static AxisGeometry Cylinder(Solid solid,List<Face> faces,List<CylindricalFace> cylinders,Transform t)
    {
        double Radius(CylindricalFace c) => c.get_Radius(0).GetLength();
        if(cylinders.Any(c=>Math.Abs(Radius(c)-c.get_Radius(1).GetLength())>1e-6)) throw new NotSupportedException("Trụ elliptic chưa hỗ trợ.");
        var outer=cylinders.MaxBy(Radius)!;
        var outerRadius=Radius(outer);
        var axis=outer.Axis.Normalize();var origin=outer.Origin;
        if(faces.Any(f=>f is not PlanarFace && f is not CylindricalFace) ||
            faces.OfType<PlanarFace>().Any(f=>Math.Abs(f.FaceNormal.DotProduct(axis))<1-1e-6) ||
            cylinders.Any(c=>Math.Abs(c.Axis.Normalize().DotProduct(axis))<1-1e-6 || (c.Origin-origin).CrossProduct(axis).GetLength()>1e-5))
            throw new NotSupportedException("Solid không phải ống trụ thẳng đồng trục.");
        var radii=cylinders.Select(Radius).OrderBy(x=>x).ToList();
        double inner=radii[0]<outerRadius-1e-6?radii[0]:0;
        if(radii.Any(r=>Math.Abs(r-outerRadius)>1e-6 && Math.Abs(r-inner)>1e-6)) throw new NotSupportedException("Nhiều lớp hình trụ chưa hỗ trợ.");
        var values=Vertices(solid).Select(p=>(p-origin).DotProduct(axis)).ToList();
        if(values.Count==0) throw new NotSupportedException("Không có cạnh solid.");
        double lo=values.Min(),hi=values.Max();
        var expected=Math.PI*(outerRadius*outerRadius-inner*inner)*(hi-lo);
        if(expected<=0 || Math.Abs(solid.Volume-expected)>Math.Max(1e-8,expected*1e-4)) throw new NotSupportedException("Solid trụ bị cắt/biến dạng; không dùng hộp bao.");
        return Make(origin+axis*lo,origin+axis*hi,Shape.Round,2*outerRadius,2*outerRadius,outer.get_Radius(0),t,"Coaxial cylindrical solid (outer diameter)");
    }
    private static AxisGeometry Prism(Solid solid,List<Face> faces,Transform t)
    {
        if(faces.Count!=6 || faces.Any(f=>f is not PlanarFace)) throw new NotSupportedException("Chỉ hỗ trợ IFC solid trụ hoặc lăng trụ chữ nhật 6 mặt.");
        var vertices=Vertices(solid);
        var axes=new List<XYZ>();
        foreach(var face in faces.Cast<PlanarFace>())
        {
            if(face.EdgeLoops.Size!=1 || face.EdgeLoops.get_Item(0).Size!=4 || face.EdgeLoops.get_Item(0).Cast<Edge>().Any(e=>e.AsCurve() is not Line))
                throw new NotSupportedException("Mặt không phải chữ nhật kín.");
            var n=face.FaceNormal.Normalize();
            if(axes.All(a=>Math.Abs(a.DotProduct(n))<1-1e-6)) axes.Add(n);
        }
        if(axes.Count!=3 || Math.Abs(axes[0].DotProduct(axes[1]))>1e-6 || Math.Abs(axes[0].DotProduct(axes[2]))>1e-6 || Math.Abs(axes[1].DotProduct(axes[2]))>1e-6)
            throw new NotSupportedException("Lăng trụ không trực giao.");
        var dims=axes.Select(a=> {var values=vertices.Select(p=>p.DotProduct(a)).ToList();return (Axis:a,Min:values.Min(),Max:values.Max(),Size:values.Max()-values.Min());}).OrderByDescending(x=>x.Size).ToList();
        var volume=dims.Aggregate(1.0,(v,d)=>v*d.Size);
        if(volume<=0 || Math.Abs(solid.Volume-volume)>Math.Max(1e-8,volume*1e-5)) throw new NotSupportedException("Solid không phải lăng trụ đặc chữ nhật.");
        // A solid has no semantic centerline. The unique longest axis is an explicit
        // Ver1 inference, reported in every result; a cube is rejected as ambiguous.
        if(dims[0].Size<dims[1].Size*1.001) throw new NotSupportedException("Trục dọc lăng trụ mơ hồ (các cạnh dài bằng nhau).");
        var center=dims.Aggregate(XYZ.Zero,(p,d)=>p+d.Axis*((d.Min+d.Max)*0.5));
        return Make(center-dims[0].Axis*dims[0].Size*0.5,center+dims[0].Axis*dims[0].Size*0.5,
            Shape.Rectangular,dims[1].Size,dims[2].Size,dims[1].Axis,t,"Rectangular prism: unique longest axis inferred");
    }
    private static AxisGeometry Make(XYZ start,XYZ end,Shape shape,double width,double height,XYZ sectionX,Transform t,string method)
    {
        if(!t.IsConformal || Math.Abs(t.Scale-1)>1e-8) throw new NotSupportedException("Link transform có scale không hỗ trợ.");
        if(start.DistanceTo(end)<1e-8 || width<=0 || height<=0) throw new NotSupportedException("Kích thước/chiều dài không hợp lệ.");
        return new(Point(t.OfPoint(start)),Point(t.OfPoint(end)),shape,width*MmPerFoot,height*MmPerFoot,Vector(t.OfVector(sectionX)).Unit,method);
    }
}
