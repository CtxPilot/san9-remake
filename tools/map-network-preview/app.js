import { createGraph } from './graph.js';

const $ = id => document.getElementById(id);
const ns = 'http://www.w3.org/2000/svg';
const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const project = (x,y) => [800+(x-y)*4,45+(x+y)*2];
const kindName = {city:'城市',port:'港口',fort:'关隘',facility:'设施'};
const nameOf = n => n.name;
const formatDays = days => `${Number(days.toFixed(1))}日`;
const map = $('map');
let data, graph, regionIndex, cutoff = 60, selected = 69, mode = 'city', view = [0,0,1600,920], renderKey = '', request = 0;
const canvas = document.createElement('canvas'); canvas.width=1600;canvas.height=920;
const ctx = canvas.getContext('2d');
const cityNode = city => graph.nodes.get(city);
const cityColor = id => `hsl(${(id*47+72)%360} 25% 77%)`;
function svg(type,attrs={},text='') {const e=document.createElementNS(ns,type);for(const [k,v] of Object.entries(attrs))e.setAttribute(k,v);if(text)e.textContent=text;return e;}
function setView(v) {view=[Math.max(-300,Math.min(1500,v[0])),Math.max(-200,Math.min(850,v[1])),Math.max(160,Math.min(1900,v[2])),Math.max(120,Math.min(1300,v[3]))];map.setAttribute('viewBox',view.join(' '));scaleNodes();}
function scaleNodes(){const box=$('map-stage').getBoundingClientRect(),scale=Math.max(view[2]/box.width,view[3]/box.height);for(const n of document.querySelectorAll('#nodes .node'))n.setAttribute('transform',`translate(${n.dataset.x},${n.dataset.y}) scale(${scale})`);}
function focus(nodes) {
    if(!nodes?.length)return;
    const points=nodes.map(n=>project(n.x,n.y)), xs=points.map(p=>p[0]),ys=points.map(p=>p[1]);
    const minx=Math.min(...xs),maxx=Math.max(...xs),miny=Math.min(...ys),maxy=Math.max(...ys);
    const box=$('map-stage').getBoundingClientRect(),ratio=box.width/box.height;
    let w=Math.max(300,maxx-minx+170),h=Math.max(250,maxy-miny+150);
    if(w/h<ratio)w=h*ratio;else h=w/ratio;
    setView([(minx+maxx-w)/2,(miny+maxy-h)/2,w,h]);
}
function terrain() {
    const chosen=graph.nodes.get(selected),key=`${data.sourceHash}:${mode}:${chosen?.city}`;
    if(renderKey===key)return;renderKey=key;
    const {width,height,grid}=data,blocked=new Set(grid.blocked),types=new Map(data.terrains.map(t=>[t.id,t]));
    ctx.clearRect(0,0,1600,920);
    for(let y=0;y<height;y++)for(let x=0;x<width;x++){
        const i=y*width+x,t=grid.terrain[i],e=grid.elevation[i],node=regionIndex.get(grid.region[i]);
        const [px,py]=project(x,y);let color;
        if(types.get(t)?.isWater)color=blocked.has(i)?'#d8e2d7':'#b8cec1';
        else if(blocked.has(i))color=`hsl(79 12% ${78-Math.min(8,e*.4)}%)`;
        else if(mode==='city')color=node?.city===chosen?.city?'#cbd4ac':`hsl(${((node?.city??0)*47+72)%360} 19% ${86-Math.min(4,e*.3)}%)`;
        else color=`hsl(${t===6||t===7?87:75} 20% ${85-Math.min(9,e*.5)}%)`;
        ctx.fillStyle=color;ctx.beginPath();ctx.moveTo(px,py);ctx.lineTo(px+4,py+2);ctx.lineTo(px,py+4);ctx.lineTo(px-4,py+2);ctx.closePath();ctx.fill();
        if(blocked.has(i)&&!types.get(t)?.isWater&&(x*13+y*7)%19===0){ctx.strokeStyle='#8e9b7c55';ctx.lineWidth=.5;ctx.beginPath();ctx.moveTo(px-3,py+1);ctx.lineTo(px,py-3-e*.07);ctx.lineTo(px+4,py+1);ctx.stroke();}
    }
    $('terrain-image').setAttribute('href',canvas.toDataURL());
}
function borders() {
    const {width,height,grid}=data,paths=[];
    for(let y=0;y<height;y++)for(let x=0;x<width;x++){
        const i=y*width+x,[px,py]=project(x,y);
        if(x+1<width&&grid.region[i]!==grid.region[i+1])paths.push(`M${px+4},${py+2}l-4,2`);
        if(y+1<height&&grid.region[i]!==grid.region[i+width])paths.push(`M${px-4},${py+2}l4,2`);
    }
    $('borders').replaceChildren(svg('path',{d:paths.join(''),class:'border-line'}));
}
function drawGraph() {
    const chosen=graph.nodes.get(selected),related=new Set(graph.neighbors(selected).map(n=>n.id));
    const edges=[];
    for(const edge of graph.lines){
        const a=graph.nodes.get(edge.a),b=graph.nodes.get(edge.b);
        const highlight=edge.a===selected||edge.b===selected;
        const p=project(a.x+.5,a.y+.5),q=project(b.x+.5,b.y+.5);
        const attrs={d:`M${p}L${q}`,class:`geo-edge ${highlight?'highlight':'dim'}`};
        if(edge.forward&&!edge.backward)attrs['marker-end']='url(#oneway-arrow)';
        if(edge.backward&&!edge.forward)attrs['marker-start']='url(#oneway-arrow)';
        const line=svg('path',attrs);const forward=graph.days(edge.a,edge.b),backward=graph.days(edge.b,edge.a);line.append(svg('title',{},`${a.name} → ${b.name}: ${forward==null?'无当前限时关系':formatDays(forward)}；反向: ${backward==null?'无当前限时关系':formatDays(backward)}`));edges.push(line);
    }
    $('edges').replaceChildren(...edges);$('edges').style.display=$('edges-toggle').checked?'':'none';
    const admin=[],root=chosen?cityNode(chosen.city):null;
    if(root)for(const n of graph.children(chosen.city)){if(n.id!==root.id)admin.push(svg('path',{d:`M${project(root.x+.5,root.y+.5)}L${project(n.x+.5,n.y+.5)}`,class:'admin-edge'}));}
    $('admin-lines').replaceChildren(...admin);$('admin-lines').style.display=$('admin-toggle').checked?'':'none';
    $('borders').style.display=$('borders-toggle').checked?'':'none';
    const groups=[];
    for(const n of data.nodes){
        const active=n.id===selected,near=related.has(n.id),sameCity=n.city===chosen?.city;
        const [x,y]=project(n.x+.5,n.y+.5),g=svg('g',{transform:`translate(${x},${y})`,class:`node ${n.kind}${active?' selected':''}${!active&&!near&&!sameCity?' dim':''}`,'data-node':n.id,'data-x':x,'data-y':y,role:'button',tabindex:0,'aria-label':`${nameOf(n)}，${kindName[n.kind]}`});
        if(active)g.append(svg('circle',{r:12,fill:'#c6a85525',stroke:'#aa8c51','stroke-width':1}));
        const fill=near?'#789365':sameCity?'#6a865e':'#8d9a7b';
        if(n.kind==='city')g.append(svg('rect',{x:-4.5,y:-4.5,width:9,height:9,rx:.5,fill,class:'symbol'}));
        else if(n.kind==='port')g.append(svg('path',{d:'M0 -4L4 0L0 4L-4 0Z',fill,class:'symbol'}));
        else g.append(svg('path',{d:'M-4 3L0 -4L4 3Z',fill,class:'symbol'}));
        g.append(svg('rect',{x:-10,y:-13,width:24+nameOf(n).length*(n.kind==='city'?14:12),height:26,fill:'transparent'}));
        g.append(svg('text',{x:9,y:3},nameOf(n)));
        groups.push(g);
    }
    groups.sort((a,b)=>Number(a.dataset.node==selected)-Number(b.dataset.node==selected));$('nodes').replaceChildren(...groups);scaleNodes();
}
function listCities() {
    const search=$('search').value.trim(),selectedCity=graph.nodes.get(selected)?.city;
    const nodes=search?data.nodes.filter(n=>n.name.includes(search)||n.regionName.includes(search)):data.nodes.filter(n=>n.kind==='city');
    $('city-count').textContent=search?`${nodes.length}个结果`:`${nodes.length}座`;
    $('city-list').innerHTML=nodes.length?nodes.map(n=>`<button class="city-item${n.id===selected||(n.kind==='city'&&n.city===selectedCity)?' active':''}" data-select="${n.id}"><span><i style="--city-color:${cityColor(n.city)}"></i>${esc(nameOf(n))}</span><small>${search?kindName[n.kind]:`${graph.children(n.city).length}设施`}</small></button>`).join(''):'<p class="empty-message">没有匹配的节点</p>';
}
function relatedButton(n,suffix) {return `<button class="related-item" data-select="${n.id}"><span><i></i>${esc(nameOf(n))}</span><small>${suffix??kindName[n.kind]} ↗</small></button>`;}
function inspector() {
    const n=graph.nodes.get(selected);if(!n)return;
    const city=cityNode(n.city),neighbors=graph.neighbors(n.id);
    const outgoing=graph.outgoing.get(n.id)??new Set(),incoming=graph.incoming.get(n.id)??new Set();
    $('inspector').innerHTML=`<h2>${esc(nameOf(n))}</h2><span class="kind-badge">${kindName[n.kind]}</span><div class="metadata"><div><span>所属城市</span>${city?`<button data-select="${city.id}">${esc(nameOf(city))} ↗</button>`:'<strong>未指定</strong>'}</div><div><span>所在地域</span><strong>${esc(n.regionName)}</strong></div><div><span>地图坐标</span><strong>${Math.round(n.x)}, ${Math.round(n.y)}</strong></div><div><span>限时可达方向</span><strong>${outgoing.size}出向 / ${incoming.size}入向</strong></div></div><div class="selected-note">${n.kind==='city'?`高亮${esc(nameOf(n))}辖区。切换「城市归属线」，查看它与所属设施的关系。`:`${esc(nameOf(n))}是地图上的独立节点。所属${esc(city?nameOf(city):'未指定城市')}，连接按实际设施间的参考行程判断。`}</div>`;
    $('neighbor-count').textContent=`${neighbors.length}个节点`;
    $('neighbors').innerHTML=`<div class="node-list">${neighbors.length?neighbors.map(other=>{const to=graph.days(n.id,other.id),back=graph.days(other.id,n.id);return relatedButton(other,to!=null&&back!=null?`→${formatDays(to)} / ←${formatDays(back)}`:to!=null?`→${formatDays(to)}`:`←${formatDays(back)}`);}).join(''):'<p class="empty-message">当前上限内没有可达设施。</p>'}</div>`;
    const children=graph.children(n.city).filter(other=>other.id!==n.id);
    $('affiliated').innerHTML=children.length?`<div class="section-heading">同城设施 <small>${children.length}个</small></div><div class="node-list">${children.map(other=>relatedButton(other)).join('')}</div>`:'';
    $('map-title').textContent=`${n.name} · ${city?nameOf(city):'独立设施'}`;
    $('map-subtitle').textContent=`${cutoff}日内 · ${neighbors.length}个可达设施 · 参考行程`;
}
function render(){if(!data)return;terrain();drawGraph();listCities();inspector();$('relation-stats').textContent=`${cutoff}日内 · ${graph.lines.length}组连接 · ${graph.connectionCount}条有向关系`;}
function select(id,move=false){selected=id;render();if(move){const n=graph.nodes.get(id);focus([n,...graph.neighbors(id)]);}}
async function loadScenario(id,initial=false){
    const token=++request;$('map-stats').textContent='正在读取建筑与地理配置…';
    try{
        const response=await fetch(`/api/map?id=${encodeURIComponent(id)}`);if(!response.ok)throw new Error(`读取失败 (${response.status})`);
        const next=await response.json();if(token!==request)return;
        data=next;regionIndex=new Map(data.regions.map(r=>[r.id,r]));graph=createGraph(data,cutoff);if(!graph.nodes.has(selected))selected=data.nodes[0].id;renderKey='';
        $('map-stats').textContent=`${data.nodes.filter(n=>n.kind==='city').length}城市 · ${data.nodes.filter(n=>n.kind!=='city').length}设施 · ${data.nodes.length}个设施节点`;
        $('reference-note').textContent=`${data.reference.landName}＋${data.reference.waterName} · 机动力 ${data.reference.landBudget}/${data.reference.waterBudget} · 静态地形估时`;
        $('source-detail').textContent=`地图配置 ${data.scenario.id} / ${data.scenario.year}年 / ${data.scenario.title}\n输入 SHA-256: ${data.sourceHash}`;
        borders();render();if(initial)focus([graph.nodes.get(selected),...graph.neighbors(selected)]);
    }catch(error){if(token!==request)return;$('map-stats').textContent=error.message;console.error(error);}
}
function zoom(factor,cx,cy){const box=map.getBoundingClientRect(),fx=cx==null?.5:(cx-box.left)/box.width,fy=cy==null?.5:(cy-box.top)/box.height;setView([view[0]+view[2]*(1-factor)*fx,view[1]+view[3]*(1-factor)*fy,view[2]*factor,view[3]*factor]);}
function bind(){
    $('scenario-select').addEventListener('change',e=>loadScenario(e.target.value));$('search').addEventListener('input',listCities);
    document.addEventListener('click',e=>{const button=e.target.closest('[data-select]');if(button)select(Number(button.dataset.select),true);});
    for(const id of ['edges-toggle','admin-toggle','borders-toggle'])$(id).addEventListener('change',render);
    $('cutoff').addEventListener('input',e=>{cutoff=Number(e.target.value);$('cutoff-value').textContent=`${cutoff}日`;if(data){graph=createGraph(data,cutoff);render();}});
    $('map-modes').addEventListener('click',e=>{const b=e.target.closest('[data-mode]');if(!b)return;mode=b.dataset.mode;document.querySelectorAll('[data-mode]').forEach(el=>el.classList.toggle('active',el===b));render();});
    $('zoom-in').addEventListener('click',()=>zoom(.8));$('zoom-out').addEventListener('click',()=>zoom(1.25));$('fit-map').addEventListener('click',()=>setView([0,-10,1600,920]));
    $('focus-map').addEventListener('click',()=>focus([graph.nodes.get(selected),...graph.neighbors(selected)]));
    $('about-button').addEventListener('click',()=>$('about-dialog').showModal());$('close-about').addEventListener('click',()=>$('about-dialog').close());
    $('about-dialog').addEventListener('click',e=>{if(e.target===$('about-dialog'))$('about-dialog').close();});
    let drag=null,didDrag=false;
    map.addEventListener('wheel',e=>{e.preventDefault();zoom(e.deltaY>0?1.12:.89,e.clientX,e.clientY);},{passive:false});
    map.addEventListener('pointerdown',e=>{drag={x:e.clientX,y:e.clientY,view:[...view],node:e.target.closest('[data-node]')?.dataset.node};didDrag=false;map.setPointerCapture(e.pointerId);});
    map.addEventListener('pointermove',e=>{
        if(drag){const box=map.getBoundingClientRect(),scale=Math.max(view[2]/box.width,view[3]/box.height),dx=e.clientX-drag.x,dy=e.clientY-drag.y;if(Math.abs(dx)+Math.abs(dy)>4){didDrag=true;map.classList.add('dragging');$('tooltip').hidden=true;}if(didDrag)setView([drag.view[0]-dx*scale,drag.view[1]-dy*scale,drag.view[2],drag.view[3]]);return;}
        const node=e.target.closest('[data-node]');if(!node){$('tooltip').hidden=true;return;}
        const n=graph.nodes.get(Number(node.dataset.node)),box=$('map-stage').getBoundingClientRect(),city=cityNode(n.city);
        $('tooltip').innerHTML=`${esc(nameOf(n))}<small>${kindName[n.kind]} · 所属${esc(city?nameOf(city):'未指定城市')}</small>`;$('tooltip').hidden=false;
        $('tooltip').style.left=`${Math.max(8,Math.min(box.width-170,e.clientX-box.left+12))}px`;$('tooltip').style.top=`${Math.min(box.height-75,e.clientY-box.top+12)}px`;
    });
    map.addEventListener('pointerup',e=>{const id=drag?.node;if(!didDrag&&id!=null)select(Number(id));drag=null;map.classList.remove('dragging');});
    map.addEventListener('pointercancel',()=>{drag=null;map.classList.remove('dragging');});map.addEventListener('pointerleave',()=>{$('tooltip').hidden=true;});
    map.addEventListener('keydown',e=>{const node=e.target.closest('[data-node]');if(node&&(e.key==='Enter'||e.key===' ')){e.preventDefault();select(Number(node.dataset.node));}});
}
async function boot(){try{const response=await fetch('/api/catalog');if(!response.ok)throw new Error('无法读取剧本目录');const catalog=await response.json();$('scenario-select').innerHTML=catalog.map(s=>`<option value="${esc(s.id)}">${s.year}年 · ${esc(s.title)}</option>`).join('');const id=catalog.some(s=>s.id==='19')?'19':catalog[0].id;$('scenario-select').value=id;bind();await loadScenario(id,true);new ResizeObserver(()=>scaleNodes()).observe($('map-stage'));document.documentElement.dataset.ready='true';}catch(error){$('map-stats').textContent=error.message;console.error(error);}}
boot();
