from pyrevit.api import DB
import System
from System import Array, Object, Int64, Double, Enum
from System.Reflection import Assembly
from System.Collections.Generic import List

assembly = Assembly.LoadFile(inputs['dll'])
prefix = 'MEP_Compare_Project_ver1.'
def type_of(name):
    return assembly.GetType(prefix + name, True)
def call(name, method, args):
    return type_of(name).GetMethod(method).Invoke(None, Array[Object](args))
links = list(DB.FilteredElementCollector(doc).OfClass(DB.RevitLinkInstance))
link = next(x for x in links if x.GetLinkDocument() is not None)
scope_type = type_of('Models.Scope')
options = type_of('Models.RunOptions').GetConstructors()[0].Invoke(Array[Object]([
    Int64(link.Id.Value),True,True,Enum.Parse(scope_type,'WholeModel'),'Revit.test',Double(1),Double(1),Double(1),Double(0.1)]))
data = call('Readers.ModelReader','Read',[uidoc,options,List[DB.ElementId]()])
matches = call('Matching.GuidMatcher','Match',[data.Item1,data.Item2,True])
rows = []
for pair in matches:
    r = call('Comparison.ComparisonService','Compare',[pair,options])
    rows.append({'host_id':r.HostId,'linked_id':r.LinkedId,'tag':r.IfcTag,'guid':r.Guid,'status':unicode(r.Status).encode('ascii','backslashreplace'),
        'dx_mm':r.DeltaXmm,'dy_mm':r.DeltaYmm,'dz_mm':r.DeltaZmm,'endpoint_mm':r.EndpointMm,
        'angle_deg':r.AngleDeg,'size_mm':r.SizeMm,'length_mm':r.LengthMm,'roll_deg':r.RollDeg,'notes':unicode(r.Notes).encode('ascii','backslashreplace')})
diagnostic = []
for pair_ids in inputs.get('diagnostic_pairs',[]):
    h = doc.GetElement(DB.ElementId(pair_ids[0]))
    l = link.GetLinkDocument().GetElement(DB.ElementId(pair_ids[1]))
    host = next(x for x in data.Item1 if x.Id == pair_ids[0])
    target = next(x for x in data.Item2 if x.Id == pair_ids[1])
    # Explicit geometry-only diagnostic, independent of matching acceptance.
    pair = type_of('Matching.MatchItem').GetConstructors()[0].Invoke(Array[Object]([host,target,'Paired','diagnostic only']))
    r = call('Comparison.ComparisonService','Compare',[pair,options])
    diagnostic.append({'host_id':r.HostId,'linked_id':r.LinkedId,'tag':r.IfcTag,'status_geometry_only':unicode(r.Status).encode('ascii','backslashreplace'),
        'dx_mm':r.DeltaXmm,'dy_mm':r.DeltaYmm,'dz_mm':r.DeltaZmm,'notes':unicode(r.Notes).encode('ascii','backslashreplace')})
result = {'assembly':assembly.Location,'rows':rows,'diagnostic_not_matching_override':diagnostic,'host_modified':doc.IsModified}


