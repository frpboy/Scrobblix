"""Synthetic API36-only bar diagnostics. Raw service output never leaves this parser.
Missing/ambiguous values are unknown, never false. No device calls at import time.
"""
import re
MAX_BYTES=262144
MAX_LINE=4096
BOOLS=('mNavigationLight','mHasLightNavigationBar','mForceDarkForScrim','mForceLightForScrim','mQsCustomizing','mQsExpanded','mBouncerVisible','mGlobalActionsVisible','mDirectReplying','mNavbarColorManagedByIme')
TRANS=('mTransitionDeferring','mTransitionPending','mTintChangePending')
INTENSITY=('mPendingDarkIntensity','mDarkIntensity','mNextDarkIntensity')
UNKNOWN='unknown'

def lines(raw):
    if len(raw)>MAX_BYTES: raise ValueError('input_limit')
    text=raw.decode('utf-8',errors='strict')
    ls=text.splitlines()
    if any(len(x)>MAX_LINE for x in ls):raise ValueError('line_limit')
    return ls

def one(values):return values[0] if len(values)==1 else UNKNOWN

def token(ls,key,pattern):
    hits=[m.group(1) for line in ls for m in re.finditer(r'(?<!\S)'+re.escape(key)+r'=(\S*)',line)]
    if len(hits)!=1 or not re.fullmatch(pattern,hits[0]):return UNKNOWN
    return hits[0]

APPEARANCE_NAMES=set("OPAQUE_STATUS_BARS OPAQUE_NAVIGATION_BARS LOW_PROFILE_BARS LIGHT_STATUS_BARS LIGHT_NAVIGATION_BARS SEMI_TRANSPARENT_STATUS_BARS SEMI_TRANSPARENT_NAVIGATION_BARS FORCE_LIGHT_NAVIGATION_BARS APPEARANCE_TRANSPARENT_CAPTION_BAR_BACKGROUND APPEARANCE_LIGHT_CAPTION_BARS".split())
def appearance(ls,key):
    hits=[l.strip().split('=',1)[1] for l in ls if l.strip().startswith(key+'=')]
    if len(hits)!=1:return UNKNOWN
    names=hits[0].split()
    if not names or len(names)!=len(set(names)) or not set(names)<=APPEARANCE_NAMES:return UNKNOWN
    return 'true' if 'LIGHT_NAVIGATION_BARS' in names else 'false'

def systemui(raw):
    ls=lines(raw)
    # Exact default-display controller section only; reject two matches.
    starts=[i for i,l in enumerate(ls) if l.strip()=='LightBarController:']
    if len(starts)!=1:raise ValueError('controller_context')
    ls=ls[starts[0]+1:]
    end=next((i for i,l in enumerate(ls) if l.strip()=='Recent Calculation Logs:'),len(ls))
    fields=ls[:end]
    result={k:token(fields,k,'true|false') for k in BOOLS}
    result['effectiveLightNav']=appearance(fields,'mAppearance')
    result['mNavigationBarMode']=token(fields,'mNavigationBarMode',r'[0-9]{1,2}')
    ns=[i for i,l in enumerate(ls)if l.strip()=='NavigationBarTransitionsController:']
    nav=ls[ns[0]+1:] if len(ns)==1 else []
    for k in TRANS:result[k]=token(nav,k,'true|false')
    for k in INTENSITY:result[k]=token(nav,k,r'(?:0(?:\.\d{1,8})?|1(?:\.0{1,8})?)')
    return result

def policy(raw):
    ls=lines(raw)
    displays=[i for i,l in enumerate(ls)if re.fullmatch(r'\s*Display: mDisplayId=0(?: \(organized\))?',l)]
    if len(displays)!=1:raise ValueError('display_context')
    start=displays[0]+1
    stop=next((i for i in range(start,len(ls))if re.fullmatch(r'\s*Display: mDisplayId=\d+(?: \(organized\))?',ls[i])),len(ls))
    ls=ls[start:stop]
    ps=[i for i,l in enumerate(ls)if l.strip()=='DisplayPolicy']
    if len(ps)!=1:raise ValueError('policy_context')
    indent=len(ls[ps[0]])-len(ls[ps[0]].lstrip())
    sub=[]
    for l in ls[ps[0]+1:]:
        if l.strip() and len(l)-len(l.lstrip())<=indent:break
        sub.append(l)
    values=[]
    for l in sub:
        if re.match(r'\s*mNavBarColorWindowCandidate=',l):
            # Names never escape; classify only exact known component suffixes.
            if re.fullmatch(r'\s*mNavBarColorWindowCandidate=Window\{[a-f0-9]+ u0 com\.frpboy\.scrobblix/androidx\.activity\.ComponentActivity\}',l):v='fixture'
            elif re.fullmatch(r'\s*mNavBarColorWindowCandidate=Window\{[a-f0-9]+ u0 com\.frpboy\.scrobblix/dev\.scrobblix\.app\.MainActivity\}',l):v='main'
            elif l.strip()=='mNavBarColorWindowCandidate=null':v='absent'
            else:v='other'
            values.append(v)
    return {'navColorCandidate':one(values),'lastEffectiveLightNav':appearance(sub,'mLastAppearance'),'hasBottomNav':token(sub,'mHasBottomNavigationBar','true|false')}
