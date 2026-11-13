// Offline layout using exported GlassDesk controls; no UI is generated or redrawn.
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '../..');
const ui = path.join(root, 'artifacts/marketing-ui');
const out = path.join(root, 'docs/marketing/images');
const pages = path.join(root, 'artifacts/marketing-layout');
const chrome = process.env.GLASSDESK_CHROME ||
  'C:/Program Files/Google/Chrome/Application/chrome.exe';
fs.mkdirSync(out, { recursive: true });
fs.mkdirSync(pages, { recursive: true });
const img = (name, cls = '') => `<img class="${cls}" src="data:image/png;base64,${
  fs.readFileSync(path.join(ui, name + '.png')).toString('base64')}" alt="">`;
const brandMark = `<span class="mark"><img alt="" src="data:image/png;base64,${
  fs.readFileSync(path.join(root,
    'src/GlassDesk.App/Assets/GlassDeskBrand.png')).toString('base64')}"></span>`;

const css = `
*{box-sizing:border-box}html,body{margin:0;width:1080px;height:1920px;overflow:hidden}
body{font-family:'Microsoft YaHei UI','Microsoft YaHei',sans-serif;color:#172d30;
background:#f2f4ee;-webkit-font-smoothing:antialiased}
.poster{position:relative;width:1080px;height:1920px;padding:86px 76px;
background:radial-gradient(ellipse at 100% 67%,#c6dfd570,transparent 57%),#f2f4ee}
.brand{display:flex;align-items:center;gap:14px;font-size:26px;font-weight:700;
letter-spacing:1px}.mark{background:transparent;width:44px;height:44px;
border-radius:14px;display:grid;place-items:center;font-size:29px}
.mark img{width:44px;height:44px;object-fit:contain}
.num{margin-left:auto;font-size:19px;color:#60736e;font-weight:400;letter-spacing:2px}
.eyebrow{font-size:24px;margin:78px 0 25px;color:#4e7165;font-weight:600}
h1{font-size:85px;font-weight:700;line-height:1.22;letter-spacing:-3px;margin:0;
max-width:930px}h1 em{font-style:normal;color:#126856}
.sub{font-size:29px;line-height:1.8;color:#59706a;margin:29px 0 0;max-width:880px}
.foot{position:absolute;left:76px;right:96px;bottom:168px;display:flex;
align-items:center;justify-content:space-between;border-top:1px solid #bccfc4;
padding-top:24px;font-size:20px;color:#5e746c}.foot b{color:#254a3e;font-size:24px}
.note{position:absolute;left:76px;bottom:125px;font-size:16px;color:#788880}
.chips{display:flex;gap:12px;flex-wrap:wrap}.chip{border:1px solid #b6cfc0;
border-radius:50px;padding:12px 22px;font-size:23px;background:#e7eee4}
.desktop{position:absolute;left:46px;top:690px;width:988px;height:650px;
overflow:hidden;border:8px solid #263734;border-radius:26px;
box-shadow:0 42px 70px #304f3b26;background:
radial-gradient(ellipse at 18% 80%,#edbbb1,transparent 55%),
radial-gradient(ellipse at 80% 10%,#4b766f,transparent 65%),#adc1b4}
.desktop .bar{position:absolute;top:0;left:0;width:100%;height:auto}
.desktop .launch{position:absolute;width:730px;left:118px;top:75px;
filter:drop-shadow(0 20px 32px #18272840)}
.desktop .dock{position:absolute;left:50%;bottom:25px;width:865px;
transform:translateX(-50%);filter:drop-shadow(0 12px 20px #12241f55)}
.below{position:absolute;left:76px;top:1430px;right:100px}
.below p{font-size:30px;line-height:1.8;margin:24px 0 0}
.dock-card{position:absolute;top:685px;left:45px;width:990px;min-height:220px;
padding:24px;border-radius:34px;background:linear-gradient(145deg,#578879,#b8c9b4);
box-shadow:0 28px 60px #304f3b20}.dock-card img{width:100%;display:block}
.stack-card{position:absolute;top:1010px;left:170px;width:740px;min-height:250px;
display:flex;align-items:center;justify-content:center}
.stack-card img{max-width:100%;max-height:460px;filter:drop-shadow(0 25px 35px #203b3020)}
.launcher-frame{position:absolute;left:48px;right:48px;top:665px;height:760px;
border-radius:32px;background:#d0ddd1;display:flex;align-items:center;justify-content:center;
box-shadow:0 30px 70px #304f3b18;overflow:hidden;padding:28px}
.launcher-frame img{width:100%;height:100%;object-fit:contain}
.bar-crop{position:absolute;top:640px;left:60px;width:960px;height:100px;
overflow:hidden;background:#242424;border-radius:22px;box-shadow:0 20px 35px #233f3020}
.bar-crop img{position:absolute;right:8px;top:23px;width:2150px;max-width:none}
.panels{position:absolute;left:50px;right:50px;top:810px;display:grid;
grid-template-columns:1fr 1fr;gap:18px}
.panel{display:flex;flex-direction:column;align-items:center;min-height:340px}
.panel span{font-size:23px;margin-bottom:12px;color:#48665b}
.panel img{width:100%;height:auto;object-fit:contain;filter:drop-shadow(0 18px 25px #223c2c15)}
.panel.wide{grid-column:1/3;margin:0 auto;width:460px}
.settings-frame{position:absolute;top:640px;left:60px;width:960px;height:910px;
overflow:hidden;border-radius:28px;background:#1d1f26;box-shadow:0 30px 65px #304f3b25}
.settings-frame img{position:absolute;max-width:none;width:1260px;left:-280px;top:-70px}
`;
const footer = `<div class="foot"><b>Windows 11 · 免费使用</b><span>GlassDesk 预览版</span></div>
<div class="note">真实界面 · 演示布局　｜　示例应用图标归各自权利人所有</div>`;
function poster(number, eyebrow, title, sub, content) {
  return `<!doctype html><html lang="zh-CN"><meta charset="utf-8"><style>${css}</style>
  <main class="poster"><div class="brand">${brandMark}GLASSDESK
  <span class="num">${number} / 05</span></div><p class="eyebrow">${eyebrow}</p>
  <h1>${title}</h1><p class="sub">${sub}</p>${content}${footer}</main></html>`;
}
const hasStack = fs.existsSync(path.join(ui, 'dock-stack.png'));
if (!hasStack) throw new Error('Export the real dock stack popup first.');
const scenes = [
  ['01-desktop', poster('01', '给 WINDOWS 换一套桌面布局',
    '常用软件，<br><em>放在手边。</em>', '底部放应用，顶部看状态。<br>打开电脑，桌面就按自己的习惯来。',
    `<div class="desktop">${img('menu-bar','bar')}${img('launcher','launch')}${img('dock-glass','dock')}</div>
    <div class="below"><div class="chips"><span class="chip">悬浮 Dock</span>
    <span class="chip">应用收纳</span><span class="chip">可选顶部菜单栏</span></div>
    <p>GlassDesk<br>一套用在 Windows 上的桌面工具。</p></div>`) ],
  ['02-dock', poster('02', 'DOCK · 固定与收纳',
    '拖到一起，<br><em>就收好了。</em>', '同类工具叠放收纳，常用应用单独固定。<br>需要的时候，再点开。',
    `<div class="dock-card">${img('dock-glass')}</div><div class="stack-card">${
      img('dock-stack')}</div><div class="below" style="top:1490px">
    <div class="chips"><span class="chip">拖拽排序</span><span class="chip">点击展开</span>
    <span class="chip">运行窗口切换</span></div></div>`) ],
  ['03-launcher', poster('03', '启动台 · 浏览与搜索',
    '软件多了，<br><em>按名称找。</em>', '打开启动台，集中浏览应用。<br>不用翻桌面，也不用记住放在哪。',
    `<div class="launcher-frame">${img('launcher')}</div><div class="below" style="top:1480px">
    <div class="chips"><span class="chip">应用搜索</span><span class="chip">分组整理</span>
    <span class="chip">网格可调</span></div></div>`) ],
  ['04-menu-bar', poster('04', '菜单栏 · 独立状态面板',
    '顶上看状态，<br><em>底下开应用。</em>', '网络、音量、电池，各有入口。<br>日期点开，就是日历。',
    `<div class="bar-crop">${img('menu-bar')}</div><div class="panels">
    <div class="panel"><span>调节音量</span>${img('volume')}</div>
    <div class="panel"><span>查看电量</span>${img('battery')}</div>
    <div class="panel wide"><span>日期与日历</span>${img('calendar')}</div></div>`) ],
  ['05-settings', poster('05', '设置 · 按自己的习惯来',
    '大小、透明度，<br><em>自己调。</em>', '缩放、自动隐藏、图标样式，按需设置。<br>任务栏模式也能自己选。',
    `<div class="settings-frame">${img('settings-appearance')}</div>`) ],
];

for (const [name, html] of scenes) {
  const input = path.join(pages, name + '.html');
  const output = path.join(out, name + '.png');
  fs.writeFileSync(input, html, 'utf8');
  const result = spawnSync(chrome, ['--headless=new', '--disable-gpu',
    '--no-first-run', '--hide-scrollbars', '--force-device-scale-factor=1',
    '--run-all-compositor-stages-before-draw', '--virtual-time-budget=1200',
    '--window-size=1080,1920', '--user-data-dir=' + path.join(pages,'browser'),
    '--screenshot=' + output, 'file:///' + input.replaceAll('\\','/')],
    { timeout: 30000, windowsHide: true, stdio: 'pipe' });
  if (result.status !== 0 || !fs.existsSync(output)) {
    throw new Error('Poster render failed: ' + name + '\n' + result.stderr);
  }
  console.log(output);
}

const hero = `<!doctype html><html lang="zh-CN"><meta charset="utf-8">
<style>${css}
html,body{width:1200px;height:720px}body{padding:54px 60px;background:#eaf0e7}
h1{position:absolute;top:212px;left:60px;font-size:52px;line-height:1.4}
.hero-sub{position:absolute;top:395px;left:62px;font-size:23px;line-height:1.9;color:#59706a}
.desktop{top:158px;left:485px;width:665px;height:420px;border-width:6px;border-radius:20px}
.desktop .launch{width:500px;left:76px;top:50px}.desktop .dock{width:590px;bottom:22px}
.hero-foot{position:absolute;left:62px;bottom:63px;font-size:20px;color:#426657}
</style><div class="brand">${brandMark}GLASSDESK</div>
<h1>常用软件，<br><em>放在手边。</em></h1>
<p class="hero-sub">悬浮 Dock · 应用收纳<br>可选顶部菜单栏</p>
<div class="desktop">${img('menu-bar','bar')}${img('launcher','launch')}${img('dock-glass','dock')}</div>
<p class="hero-foot">Windows 11 x64 · 免费使用 · 预览版</p></html>`;
const input = path.join(pages, 'github-hero.html');
const output = path.join(out, 'github-hero.png');
fs.writeFileSync(input, hero, 'utf8');
const result = spawnSync(chrome, ['--headless=new','--disable-gpu','--no-first-run',
  '--hide-scrollbars','--force-device-scale-factor=1','--virtual-time-budget=1200',
  '--window-size=1200,720','--user-data-dir='+path.join(pages,'browser'),
  '--screenshot='+output,'file:///'+input.replaceAll('\\','/')],
  {timeout:30000,windowsHide:true,stdio:'pipe'});
if (result.status !== 0 || !fs.existsSync(output)) throw new Error('Hero render failed.');
console.log(output);
