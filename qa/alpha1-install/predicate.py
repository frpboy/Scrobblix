"""Associate one untouched tracking toggle to the real app label row, never class alone."""
import xml.etree.ElementTree as E,re,sys
PACKAGE='com.frpboy.scrobblix'
def bounds(n):
    m=re.fullmatch(r'\[(\d+),(\d+)\]\[(\d+),(\d+)\]',n.get('bounds',''))
    assert m,'bad_bounds'
    b=tuple(map(int,m.groups()));assert b[0]<b[2] and b[1]<b[3],'empty_bounds'
    return b
def identify(root):
    nodes=[n for n in root.iter('node') if n.get('package')==PACKAGE]
    labels=[n for n in nodes if n.get('text')=='Track listening']
    if not labels:raise LookupError('tracking_label_not_visible')
    assert len(labels)==1,'ambiguous_tracking_label'
    label=labels[0];lb=bounds(label)
    # Actual API36 XML: toggle[40,1184][144,1280], label[168,1208][392,1256].
    # App source puts Switch then Text in a Row. Require their spatial row binding.
    candidates=[]
    for n in nodes:
        if n.get('checkable')!='true' or n.get('clickable')!='true':continue
        b=bounds(n)
        if b[2]<=lb[0] and b[1]<=lb[1] and b[3]>=lb[3] and lb[0]-b[2]<=lb[3]-lb[1]:
            candidates.append(n)
    assert len(candidates)==1,'ambiguous_or_missing_tracking_toggle'
    n=candidates[0];assert n.get('class')=='android.view.View','unexpected_tracking_class'
    assert n.get('checked')=='false','tracking_not_off'
    assert n.get('enabled')=='true','tracking_unavailable'
    return {'toggle_bounds':bounds(n),'label_bounds':lb,'checked':False,'association':'exact label row spatial binding; one clickable checkable directly left'}
if __name__=='__main__':
    print(identify(E.parse(sys.argv[1]).getroot()))
