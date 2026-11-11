from pyrevit.api import DB
from pyrevit.revit.db.query import get_param

def value(p):
    if p.StorageType == DB.StorageType.String:
        return p.AsString()
    return p.AsValueString()

def describe(e):
    if e is None:
        return None
    ps = []
    for p in e.Parameters:
        name = p.Definition.Name
        if any(s in name.lower() for s in ['guid', 'test', 'tag', 'ifc']):
            ps.append({'name': name, 'storage': str(p.StorageType), 'value': value(p)})
    return {'id': e.Id.Value, 'class': e.GetType().FullName, 'parameters': ps}

hosts = [describe(doc.GetElement(DB.ElementId(i))) for i in inputs['host_ids']]
links = []
for link in DB.FilteredElementCollector(doc).OfClass(DB.RevitLinkInstance):
    linked_doc = link.GetLinkDocument()
    if linked_doc is None:
        continue
    candidates = []
    for e in DB.FilteredElementCollector(linked_doc).WhereElementIsNotElementType():
        if e.Category and e.Category.Id.Value in [-2008000, -2008044]:
            candidates.append(describe(e))
    t = link.GetTotalTransform()
    links.append({'id': link.Id.Value, 'name': link.Name, 'transform_origin_ft': [t.Origin.X,t.Origin.Y,t.Origin.Z], 'elements': candidates})
result = {'hosts': hosts, 'links': links, 'host_modified': doc.IsModified}
