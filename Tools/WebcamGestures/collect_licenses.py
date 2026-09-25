"""Copy installed-wheel license/notice text, never code, models or font binaries."""
import importlib.metadata
import json
from pathlib import Path
import shutil
import sys

destination=Path(sys.argv[1])
destination.mkdir(parents=True,exist_ok=True)
index=[]
for distribution in importlib.metadata.distributions():
    name=distribution.metadata.get('Name','unknown')
    version=distribution.version
    copied=[]
    for entry in distribution.files or []:
        leaf=Path(str(entry)).name.lower()
        if not leaf.startswith(('license','copying','notice','authors')):
            continue
        source=Path(distribution.locate_file(entry))
        if not source.is_file() or source.stat().st_size>2000000:
            continue
        target=destination/(name+'-'+version)/str(entry)
        if '..' in Path(str(entry)).parts:
            continue
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(source,target)
        copied.append(str(target.relative_to(destination)))
    index.append({'name':name,'version':version,'license':distribution.metadata.get('License-Expression') or distribution.metadata.get('License'), 'files':copied})
(destination/'index.json').write_text(json.dumps(index,indent=2)+'\n')
print('Collected license notices for',len(index),'installed distributions.')
