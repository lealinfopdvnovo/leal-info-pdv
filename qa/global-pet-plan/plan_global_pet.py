"""Draft-only GLOBAL/PET planner. No network, package write, feed write or installation."""
from copy import deepcopy
import re


def version(text, prefix):
    if not isinstance(text,str) or not re.fullmatch(prefix+r'\.\d+(?:\.\d+)*',text):
        raise ValueError('Invalid version')
    return tuple(map(int,text.split('.')))


def plan_global(installed, common_version):
    target=version(common_version,'10')
    result=[]
    seen=set()
    for state in installed:
        identity=state['Identity']
        if not isinstance(identity,str) or not identity.strip() or identity in seen:
            raise ValueError('Missing or duplicate installation identity')
        seen.add(identity)
        if state['Edition'] not in ('STANDARD','PLUS','PRO'):
            raise ValueError('Unknown edition')
        # Existing one-number channels require a separate migration; never fabricate PET versions.
        old=version(state['CommonVersion'],'10')
        version(state['PetVersion'],'1')
        width=max(len(old),len(target))
        if old+(0,)*(width-len(old)) >= target+(0,)*(width-len(target)):
            raise ValueError('GLOBAL must advance the common version')
        preserved=deepcopy(state)
        preserved['CommonVersion']=common_version
        result.append({'Before':deepcopy(state),'After':preserved,'Changed':['CommonVersion']})
    if not result:
        raise ValueError('No installation state supplied')
    return {'Mode':'PREPARE_ONLY','CanPublish':False,'GlobalVersion':common_version,'Installations':result}
