from pyrevit.api import DB
from System import Array, Object, Int64, Double, Enum
from System.Reflection import Assembly
from System.Collections.Generic import List

assembly = Assembly.LoadFile(inputs['dll'])
prefix = 'MEP_Compare_Project_ver1.'
def t(name):
    return assembly.GetType(prefix + name, True)
def call(name, method, args):
    return t(name).GetMethod(method).Invoke(None, Array[Object](args))
def ascii_text(value):
    return unicode(value).encode('ascii', 'backslashreplace')
links = list(DB.FilteredElementCollector(doc).OfClass(DB.RevitLinkInstance))
link = next(x for x in links if x.GetLinkDocument() is not None)
options = t('Models.RunOptions').GetConstructors()[0].Invoke(Array[Object]([
    Int64(link.Id.Value), True, True, Enum.Parse(t('Models.Scope'),'WholeModel'),
    'Revit.test', Double(1), Double(1), Double(1), Double(0.1)]))
data = call('Readers.ModelReader', 'Read', [uidoc, options, List[DB.ElementId]()])

def rows(hosts, targets):
    pairs = call('Matching.PairingService','Match',[hosts,targets,True,options])
    result_rows=[]
    for pair in pairs:
        r=call('Comparison.ComparisonService','Compare',[pair,options])
        result_rows.append({'host_id':r.HostId,'linked_id':r.LinkedId,'method':ascii_text(r.MatchMethod),
            'candidates':ascii_text(r.CandidateLinkedIds),'status':ascii_text(r.Status),
            'dx_mm':r.DeltaXmm,'notes':ascii_text(r.Notes)})
    return result_rows

def without_guids(elements):
    # Clone only detached DTOs. Never clear or write a Revit parameter.
    copied=Array.CreateInstance(t('Models.ElementData'),elements.Count)
    for i,e in enumerate(elements):
        copied[i]=t('Models.ElementData').GetConstructors()[0].Invoke(Array[Object]([
            Int64(e.Id),e.Name,e.Category,'',e.Tag,e.Geometry,e.GeometryIssue,e.Linked]))
    return copied

original=rows(data.Item1,data.Item2)
no_guids=rows(without_guids(data.Item1),without_guids(data.Item2))
assert all(r['method']=='GUID' for r in original if r['linked_id'] is not None and r['host_id'] is not None)
assert any(r['method']=='Geometry' and r['linked_id'] is not None for r in no_guids)
assert all(r['linked_id'] is None for r in no_guids if r['host_id'] is not None and r['candidates'].find(',')>=0)
result={'assembly':assembly.Location,'module_id':str(assembly.ManifestModule.ModuleVersionId),
    'model_guid_priority':original,'guidless_detached_dto_simulation':no_guids,
    'host_modified':doc.IsModified,'linked_modified':link.GetLinkDocument().IsModified,
    'assertions':'passed; no document changes, GUID-less case uses detached copies only'}
