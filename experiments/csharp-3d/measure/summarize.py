import json, sys, statistics as st, os
S = os.path.dirname(os.path.abspath(__file__))

def load(name):
    p = os.path.join(S, name + '.json')
    return json.load(open(p, encoding='utf-8')) if os.path.exists(p) else None

def size_sum(reqs, pred):
    rs = [r for r in reqs if 'url' in r and 'raw' in r and pred(r['url'])]
    return dict(n=len(rs), raw=sum(r['raw'] for r in rs), gz=sum(r['gz'] for r in rs), br=sum(r['br'] for r in rs))

def runs_summary(runs, key, cold):
    vals = [r[key] for r in runs if r.get('cold') == cold and r.get(key) is not None]
    return (round(st.median(vals), 1) if vals else None, vals)

out = {}
for name in ['kni', 'webgl', 'three']:
    d = load(name)
    if not d:
        print(name, 'missing'); continue
    reqs = d['requests']
    print('=====', name, d['url'])
    print('runs:')
    for r in d['runs']:
        print('  ', json.dumps(r, ensure_ascii=False))
    for key in ['firstDrawMs', 'fps', 'cpuMsPerFrame', 'csDrawMsPerFrame']:
        c = runs_summary(d['runs'], key, True)
        w = runs_summary(d['runs'], key, False)
        print(f'  {key}: cold median={c[0]} {c[1]} | warm median={w[0]} {w[1]}')
    if name == 'three':
        fw = size_sum(reqs, lambda u: u.endswith('.js') or u.endswith('.css') or '.json' in u.split('/')[-1])
        need = size_sum(reqs, lambda u: any(x in u for x in ['three.module', 'viewport.js', 'editmesh.js', 'practice.js']))
        allsite = size_sum(reqs, lambda u: True)
        print('  JS/CSS downloaded on page:', fw)
        print('  3D に必要な分 (three.module + viewport.js + editmesh.js + practice.js):', need)
        print('  all requests (decoded sizes):', allsite)
        for r in reqs:
            if 'raw' in r and ('three' in r['url'] or 'viewport' in r['url'] or 'editmesh' in r['url'] or 'practice.js' in r['url']):
                print('   ', r['url'].split('/')[-1][:60], r['raw'], r['gz'], r['br'])
    else:
        fw = size_sum(reqs, lambda u: '/_framework/' in u)
        cont = size_sum(reqs, lambda u: '/_content/' in u)
        own = size_sum(reqs, lambda u: u.endswith('/js/gl.js') or u.endswith('app.css') or u.endswith('index.html') or u.endswith('/practice-cs'))
        print('  _framework:', fw)
        print('  _content (nkast JS):', cont)
        print('  own js/css/html:', own)
        print('  total:', size_sum(reqs, lambda u: True))
