using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost:6379";
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
    {
        EndPoints = { redisHost },
        Password = redisPassword,
        Ssl = true,
        AbortOnConnectFail = false,
        ConnectTimeout = 10000,
        SyncTimeout = 10000
    };
    options.InstanceName = "checklist_track_";
});

var app = builder.Build();
var cache = app.Services.GetRequiredService<IDistributedCache>();

async Task<List<User>> GetUsersAsync()
{
    var key = "checklist_track_system_users";
    var jsonData = await cache.GetStringAsync(key);
    if (string.IsNullOrEmpty(jsonData))
    {
        var defaultUsers = new List<User> { 
            new("duygu","12345","Duygu", "Yalınkaya"), 
            new("mehmet","12345","Mehmet", "Yalınkaya"), 
            new("huseyin","12345","Hüseyin", "Özkaya") 
        };
        await cache.SetStringAsync(key, JsonSerializer.Serialize(defaultUsers));
        return defaultUsers;
    }
    return JsonSerializer.Deserialize<List<User>>(jsonData)!;
}

async Task<List<Item>> GetItemsAsync(string email)
{
    var key = $"checklist_{email}";
    var jsonData = await cache.GetStringAsync(key);
    if (string.IsNullOrEmpty(jsonData))
    {
        var items = SeedItems();
        await SaveItemsAsync(email, items);
        return items.OrderBy(x => x.Order).ToList();
    }
    var list = JsonSerializer.Deserialize<List<Item>>(jsonData)!;
    return list.OrderBy(x => x.Order).ToList();
}

async Task SaveItemsAsync(string email, List<Item> items)
{
    var key = $"checklist_{email}";
    await cache.SetStringAsync(key, JsonSerializer.Serialize(items));
}

List<Item> SeedItems() =>
[
    new(1, "Pasaport", ["Vize bitişinden sonra +90 gün geçerlilik", "En az 2 boş sayfa"], 1),
    new(2, "Kimlik Fotokopisi", [], 2),
    new(3, "Başvuru Formu", ["Doldurulmuş, imzalanmış ve tarihli"], 3),
    new(4, "Biyometrik Fotoğraf", ["2 adet, 35x40 mm", "Açık renk fonlu"], 4),
    new(5, "Sağlık Sigortası", ["Min. 30.000 Euro teminat"], 5),
    new(6, "Vize Harcı", ["iDATA ofislerinde nakit ödenecek"], 6),
    new(7, "Yerleşim Yeri Belgesi", ["E-devletten barkodlu"], 7),
    new(8, "Nüfus Kayıt Örneği", ["Tam vukuatlı, barkodlu"], 8),
    new(9, "Dilekçe / Bildirge", ["Seyahat detayları", "Gelir beyanı"], 9),
    new(10, "Uçak Rezervasyonu", ["Gidiş-dönüş tarihli"], 10),
    new(11, "Otel Rezervasyonu", ["Tarihleri kapsayan"], 11),
    new(12, "Faaliyet Belgesi", ["Son 3 ay içinde alınmış"], 12),
    new(13, "Ticaret Sicil Gazetesi", [], 13),
    new(14, "Vergi Levhası", [], 14),
    new(15, "Maddi Gelir Evrakları", ["Banka dökümü (6 ay)", "Maaş bordrosu"], 15),
    new(16, "Çalışma & İzin Belgesi", ["Antetli kağıtta, kaşeli"], 16),
    new(17, "İmza Sirküleri", [], 17),
    new(18, "Çalışma Evrakları", ["İşe giriş bildirgesi"], 18),
    new(19, "Teminat", ["İtalya Bakanlık tutarlarına uygun"], 19)
];

app.MapGet("/", async ctx =>
{
    var html = @"<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'><title>iDATA Giriş</title>
    <style>
        .loader { border: 3px solid rgba(255, 255, 255, 0.3); border-radius: 50%; border-top: 3px solid #fff; width: 18px; height: 18px; animation: spin 1s linear infinite; display: none; margin-right: 10px; }
        @keyframes spin { 0% { transform: rotate(0deg); } 100% { transform: rotate(360deg); } }
    </style>
    </head>
    <body style='font-family:sans-serif; background:linear-gradient(135deg, #667eea, #764ba2); margin:0; height:100vh; display:flex; justify-content:center; align-items:center; color:white'>
    <div style='width:100%; height:70px; background:rgba(0,0,0,0.3); position:fixed; top:0; left:0; right:0; display:flex; align-items:center; justify-content:center; font-weight:bold; font-size:20px; backdrop-filter:blur(10px); z-index:1000;'>iDATA Evrak Takip Servisi</div>
    <div style='background:rgba(255, 255, 255, 0.15); backdrop-filter:blur(15px); padding:40px; border-radius:24px; width:340px; text-align:center; border:1px solid rgba(255,255,255,0.2)'>
        <h2 style='font-weight:300;'>Giriş Yap</h2>
        <form onsubmit='event.preventDefault(); login();'>
            <input id='email' type='text' placeholder='Kullanıcı Adı' required style='width:100%; padding:14px; margin-bottom:15px; border-radius:12px; border:none; box-sizing:border-box;'/>
            <input id='pw' type='password' placeholder='Şifre' required style='width:100%; padding:14px; margin-bottom:15px; border-radius:12px; border:none; box-sizing:border-box;'/>
            <div id='error-msg' style='color:#ffb3b3; font-size:14px; margin-bottom:15px; font-weight:bold; min-height:20px;'></div>
            <button id='login-btn' type='submit' style='width:100%; padding:14px; border-radius:12px; border:none; background:#764ba2; color:white; font-weight:bold; cursor:pointer; display:flex; justify-content:center; align-items:center;'>
                <div id='loader' class='loader'></div><span id='btn-text'>Giriş</span>
            </button>
        </form>
    </div>
    <script>
    async function login(){
        const btn = document.getElementById('login-btn');
        const loader = document.getElementById('loader');
        const btnText = document.getElementById('btn-text');
        btn.disabled = true; btn.style.opacity = '0.8'; loader.style.display = 'block'; btnText.innerText = 'Giriş Yapılıyor...';
        const email = document.getElementById('email').value;
        const pw = document.getElementById('pw').value;
        const res = await fetch('/login', { method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify({ email, pw }) });
        if(res.ok) { location = '/app'; } 
        else { document.getElementById('error-msg').innerText = 'Hatalı giriş!'; btn.disabled = false; btn.style.opacity = '1'; loader.style.display = 'none'; btnText.innerText = 'Giriş'; }
    }
    </script></body></html>";
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.WriteAsync(html, Encoding.UTF8);
});

app.MapPost("/login", async (LoginRequest req, HttpContext ctx) => {
    var usersFromRedis = await GetUsersAsync();
    var user = usersFromRedis.FirstOrDefault(x => x.Email.ToLower() == req.email?.ToLower() && x.Password == req.pw);
    if (user == null) return Results.Unauthorized();
    ctx.Response.Cookies.Append("user", user.Email, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
    return Results.Ok();
});

app.MapGet("/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("user"); return Results.Redirect("/"); });

app.MapGet("/app", async ctx => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) { ctx.Response.Redirect("/"); return; }
    var usersFromRedis = await GetUsersAsync();
    var user = usersFromRedis.FirstOrDefault(x => x.Email == email);
    if (user == null) { ctx.Response.Redirect("/"); return; }

    var html = $@"<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'><title>Vize Takip Pro</title>
    <link rel='stylesheet' href='https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css'>
    <script src='https://cdn.jsdelivr.net/npm/sortablejs@1.15.0/Sortable.min.js'></script>
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; background: linear-gradient(135deg,#667eea,#764ba2); margin:0; color:white; padding-top:130px; padding-bottom:90px; min-height: 100vh; box-sizing:border-box; }}
        .header-container {{ position: fixed; top: 0; left: 0; right: 0; z-index: 1000; background: rgba(0,0,0,0.5); backdrop-filter: blur(15px); }}
        .header {{ height: 70px; padding: 0 30px; display: flex; justify-content: space-between; align-items: center; position: relative; }}
        .progress-wrapper {{ height: 30px; background: rgba(255,255,255,0.1); position: relative; display: flex; align-items: center; overflow: hidden; }}
        #progress-bar {{ height: 100%; background: linear-gradient(90deg, #4facfe 0%, #00f2fe 100%); width: 0%; transition: width 0.5s ease; }}
        #progress-text {{ position: absolute; width: 100%; text-align: center; font-size: 12px; font-weight: bold; text-shadow: 1px 1px 2px rgba(0,0,0,0.8); }}
        .grid-container {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; padding: 20px; max-width: 1400px; margin: auto; }}
        #congrats-msg {{ grid-column: 1 / -1; background: rgba(46, 213, 115, 0.2); border: 2px solid #2ed573; color: #2ed573; padding: 20px; border-radius: 16px; text-align: center; font-size: 1.5rem; font-weight: bold; margin-bottom: 10px; display: none; animation: pulse 2s infinite; }}
        @keyframes pulse {{ 0% {{ transform: scale(1); }} 50% {{ transform: scale(1.02); }} 100% {{ transform: scale(1); }} }}
        
        .item-card {{ background: rgba(255, 255, 255, 0.12); padding: 20px; border-radius: 16px; border: 1px solid rgba(255, 255, 255, 0.1); position: relative; cursor: default; transition: 0.3s; min-height: 120px; box-sizing: border-box; }}
        .item-card:hover {{ background: rgba(255, 255, 255, 0.2); transform: translateY(-3px); }}
        .done-card {{ opacity: 0.8; background: rgba(255, 255, 255, 0.05); border: 1px solid rgba(173, 255, 47, 0.3);}}
        .selected-card {{ box-shadow: 0 0 0 2px #2ed573; background: rgba(46, 213, 115, 0.1); opacity: 0.5; }}
        .done-text {{ text-decoration: line-through; color: #adff2f; }}
        
        .drag-handle {{ position: absolute; top: 10px; right: 10px; cursor: grab; opacity: 0.3; transition: 0.2s; padding: 5px; color: white; z-index: 5; }}
        .item-card:hover .drag-handle {{ opacity: 1; }}
        .drag-handle:active {{ cursor: grabbing; }}

        .card-actions {{ position: absolute; bottom: 12px; right: 12px; display: flex; gap: 8px; }}
        .icon-btn {{ background: rgba(0,0,0,0.3); color: white; border: none; width: 32px; height: 32px; border-radius: 8px; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: 0.2s; }}
        .icon-btn:hover:not(:disabled) {{ background: rgba(255,255,255,0.2); }}
        .icon-btn:disabled {{ opacity: 0.2; cursor: not-allowed; filter: grayscale(1); }}
        
        .modal-overlay {{ position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); backdrop-filter: blur(5px); display: none; justify-content: center; align-items: center; z-index: 2000; }}
        .modal-content {{ background: #1e1e2e; width: 90%; max-width: 500px; padding: 30px; border-radius: 20px; border: 1px solid rgba(255,255,255,0.1); box-shadow: 0 10px 40px rgba(0,0,0,0.5); }}
        .btn {{ padding: 10px 20px; border-radius: 10px; border: none; cursor: pointer; font-weight: bold; transition: 0.2s; display: flex; align-items: center; gap: 8px; }}
        .btn-primary {{ background: #764ba2; color: white; }}
        .btn-secondary {{ background: rgba(255,255,255,0.1); color: white; }}
        .btn-danger {{ background: #ff4757; color: white; }}
        .btn-add {{ background: #2ed573; color: white; width: 100%; margin-top: 5px; justify-content: center; }}
        ul {{ margin: 10px 0 30px 0; padding-left: 20px; font-size: 13px; opacity: 0.8; pointer-events: none; }}
        .user-nav {{ display: flex; align-items: center; gap: 10px; cursor: pointer; padding: 8px 12px; border-radius: 10px; transition: 0.2s; }}
        .menu-dropdown {{ position: absolute; top: 60px; right: 20px; background: #1e1e2e; border-radius: 12px; min-width: 180px; display: none; flex-direction: column; overflow: hidden; box-shadow: 0 10px 30px rgba(0,0,0,0.8); border: 1px solid rgba(255,255,255,0.1); z-index: 10001; }}
        .menu-dropdown.active {{ display: flex; }}
        .menu-item {{ padding: 15px 20px; color: white; border: none; background: none; text-align: left; cursor: pointer; display: flex; align-items: center; gap: 12px; transition: 0.2s; font-family: inherit; font-size: 14px; width:100%; box-sizing: border-box; text-decoration:none; }}
        .selection-bar {{ position: fixed; bottom: 20px; left: 50%; transform: translateX(-50%); background: rgba(30, 30, 46, 0.95); backdrop-filter: blur(10px); padding: 15px 25px; border-radius: 20px; display: none; gap: 15px; z-index: 10000; box-shadow: 0 10px 40px rgba(0,0,0,0.8); border: 1px solid rgba(255,255,255,0.1); align-items: center; flex-wrap: wrap; justify-content: center; width: max-content; max-width: 90%; }}
        .selection-bar.active {{ display: flex; animation: slideUp 0.3s ease-out; }}
        @keyframes slideUp {{ from {{ bottom: -50px; opacity: 0; }} to {{ bottom: 20px; opacity: 1; }} }}
    </style></head><body>
    
    <div id='edit-modal' class='modal-overlay'>
        <div class='modal-content'>
            <h3><i class='fa-solid fa-pen-to-square'></i> Evrak Düzenle</h3>
            <input type='hidden' id='edit-id'>
            <div style='margin-bottom:20px;'><label style='display:block;margin-bottom:8px;opacity:0.7;'>Evrak Adı</label><input type='text' id='edit-title' style='width:100%;padding:12px;border-radius:10px;border:1px solid rgba(255,255,255,0.1);background:rgba(255,255,255,0.05);color:white;box-sizing:border-box;'></div>
            <div style='margin-bottom:20px;'><label style='display:block;margin-bottom:8px;opacity:0.7;'>Detaylar</label><div id='desc-container'></div><button class='btn btn-add' onclick='addDescField()'><i class='fa-solid fa-plus'></i> Madde Ekle</button></div>
            <div style='display:flex;justify-content:flex-end;gap:12px;'><button class='btn btn-secondary' onclick='closeModal(""edit-modal"")'>Vazgeç</button><button class='btn btn-primary' onclick='saveChanges()'>Kaydet</button></div>
        </div>
    </div>

    <div id='delete-modal' class='modal-overlay'>
        <div class='modal-content' style='max-width:400px;text-align:center;'><i class='fa-solid fa-circle-exclamation' style='font-size:50px;color:#ff4757;margin-bottom:15px;'></i><h3>Emin misiniz?</h3><input type='hidden' id='delete-id'><div style='display:flex;justify-content:center;gap:12px;'><button class='btn btn-secondary' onclick='closeModal(""delete-modal"")'>Vazgeç</button><button class='btn btn-danger' onclick='confirmDelete()'>Evet, Sil</button></div></div>
    </div>

    <div id='reset-modal' class='modal-overlay'>
        <div class='modal-content' style='max-width:400px;text-align:center;'><i class='fa-solid fa-rotate-left' style='font-size:50px;color:#ffa502;margin-bottom:15px;'></i><h3>Sıfırla?</h3><div style='display:flex;justify-content:center;gap:12px;'><button class='btn btn-secondary' onclick='closeModal(""reset-modal"")'>Vazgeç</button><button class='btn btn-danger' onclick='confirmReset()'>Sıfırla</button></div></div>
    </div>

    <div class='header-container'>
        <div class='header'>
            <div style='font-weight:bold; font-size:1.2rem;'><i class='fa-solid fa-passport'></i> iDATA Evrak Takip</div>
            <div style='display:flex; align-items:center; gap:10px;'>
                <div class='user-nav' onclick='toggleMenu(event)'>
                    <i class='fa-solid fa-circle-user' style='font-size:24px;'></i><span><b>{user.Name}</b></span><i class='fa-solid fa-bars' style='font-size:16px;margin-left:5px;opacity:0.8;'></i>
                </div>
                <div id='nav-menu' class='menu-dropdown'>
                    <button class='menu-item' onclick='toggleSelectionMode()'><i class='fa-solid fa-list-check' style='color:#2ed573;'></i> Seçim Modu</button>
                    <button class='menu-item' onclick='openResetModal()'><i class='fa-solid fa-rotate-left' style='color:#ffa502;'></i> Listeyi Sıfırla</button>
                    <a href='/logout' class='menu-item' style='color:#ff4757; border-top: 1px solid rgba(255,255,255,0.05);'><i class='fa-solid fa-right-from-bracket'></i> Çıkış Yap</a>
                </div>
            </div>
        </div>
        <div class='progress-wrapper'><div id='progress-bar'></div><div id='progress-text'>İlerleme: %0</div></div>
    </div>
    
    <div class='grid-container'><div id='congrats-msg'><i class='fa-solid fa-circle-check'></i> Tebrikler, Hazırsınız 😊</div><div id='list' style='display: contents;'></div></div>

    <div id='selection-actions' class='selection-bar'>
        <span id='sel-count' style='font-weight:bold;font-size:14px;'>0 Seçildi</span>
        <button class='btn btn-secondary' onclick='toggleSelectAll()'>Tümünü Seç</button>
        <button class='btn btn-add' style='width:auto;margin:0;' onclick='bulkAction(true)'><i class='fa-solid fa-check'></i> Tamamlandı</button>
        <button class='btn btn-danger' style='width:auto;margin:0;' onclick='bulkAction(false)'><i class='fa-solid fa-xmark'></i> Yapılmadı</button>
        <button class='btn btn-secondary' onclick='toggleSelectionMode()'>İptal</button>
    </div>

    <script>
    let currentItems = [];
    let isSelectionMode = false;
    let selectedIds = [];
    let sortableInstance = null;

    function toggleMenu(e) {{ e.stopPropagation(); document.getElementById('nav-menu').classList.toggle('active'); }}
    window.onclick = function() {{ const menu = document.getElementById('nav-menu'); if (menu) menu.classList.remove('active'); }}

    function toggleSelectionMode() {{
        isSelectionMode = !isSelectionMode;
        selectedIds = [];
        document.getElementById('selection-actions').classList.toggle('active', isSelectionMode);
        updateSelectionUI();
        loadUIOnly();
    }}

    function toggleSelectAll() {{
        selectedIds = (selectedIds.length === currentItems.length) ? [] : currentItems.map(x => x.id);
        updateSelectionUI();
        loadUIOnly();
    }}

    function updateSelectionUI() {{ document.getElementById('sel-count').innerText = selectedIds.length + ' Seçildi'; }}

    async function bulkAction(status) {{
        if(selectedIds.length === 0) return;
        await fetch('/items/bulk', {{ method: 'PUT', headers: {{ 'Content-Type': 'application/json' }}, body: JSON.stringify({{ ids: selectedIds, status: status }}) }});
        isSelectionMode = false; selectedIds = []; document.getElementById('selection-actions').classList.remove('active');
        load();
    }}

    async function load(){{
        const res = await fetch('/items');
        currentItems = await res.json();
        loadUIOnly();
    }}

    function loadUIOnly() {{
        const done = currentItems.filter(x => x.done).length;
        const percent = currentItems.length > 0 ? Math.round((done / currentItems.length) * 100) : 0;
        document.getElementById('progress-bar').style.width = percent + '%';
        document.getElementById('progress-text').innerText = `Tamamlanan: ${{done}} / ${{currentItems.length}} (%${{percent}})`;
        document.getElementById('congrats-msg').style.display = (currentItems.length > 0 && done === currentItems.length) ? 'block' : 'none';

        const list = document.getElementById('list');
        list.innerHTML = '';
        currentItems.forEach(i => {{
            const isSelected = selectedIds.includes(i.id);
            const card = document.createElement('div');
            card.className = `item-card ${{i.done ? 'done-card' : ''}} ${{isSelectionMode && isSelected ? 'selected-card' : ''}}`;
            card.setAttribute('data-id', i.id);
            if(isSelectionMode) card.style.cursor = 'pointer';

            card.onclick = (e) => {{ 
                if(e.target.closest('.icon-btn') || e.target.closest('.drag-handle')) return;
                if(isSelectionMode) {{
                    if(isSelected) selectedIds = selectedIds.filter(id => id !== i.id);
                    else selectedIds.push(i.id);
                    updateSelectionUI(); loadUIOnly();
                }} else {{ toggle(i.id); }}
            }};
            
            card.innerHTML = `
                ${{!isSelectionMode ? `<div class='drag-handle' title='Taşımak için basılı tutun'>
                    <i class='fa-solid fa-grip-vertical'></i>
                </div>` : ''}}
                <div style='display:flex;align-items:center;gap:10px;'>
                    <i class='fa-solid ${{isSelectionMode ? (isSelected ? 'fa-square-check' : 'fa-square') : (i.done ? 'fa-circle-check' : 'fa-circle')}}' style='font-size:1.2rem;color:${{i.done || isSelected ? '#adff2f' : 'white'}}'></i>
                    <b class='${{i.done ? 'done-text' : ''}}'>${{i.title}}</b>
                </div>
                <ul style='padding-right: 20px;'>${{i.descriptions.map(d => `<li>${{d}}</li>`).join('')}}</ul>
                ${{!isSelectionMode ? `
                <div class='card-actions'>
                    <button class='icon-btn' ${{i.done ? 'disabled' : ''}} onclick='event.stopPropagation(); openEditModal(${{i.id}})'><i class='fa-solid fa-pen'></i></button>
                    <button class='icon-btn' ${{i.done ? 'disabled' : ''}} onclick='event.stopPropagation(); openDeleteModal(${{i.id}})'><i class='fa-solid fa-trash'></i></button>
                </div>` : ''}}`;
            list.appendChild(card);
        }});

        if (sortableInstance) sortableInstance.destroy();
        if (!isSelectionMode) {{
            sortableInstance = new Sortable(list, {{
                animation: 150,
                handle: '.drag-handle',
                ghostClass: 'selected-card',
                onEnd: async () => {{
                    const ids = Array.from(list.querySelectorAll('.item-card')).map(el => parseInt(el.getAttribute('data-id')));
                    await fetch('/items/reorder', {{ method: 'PUT', headers: {{ 'Content-Type': 'application/json' }}, body: JSON.stringify(ids) }});
                }}
            }});
        }}
    }}

    function openEditModal(id) {{
        const item = currentItems.find(x => x.id === id); if(!item) return;
        document.getElementById('edit-id').value = id; document.getElementById('edit-title').value = item.title;
        const container = document.getElementById('desc-container'); container.innerHTML = '';
        item.descriptions.forEach(desc => addDescField(desc));
        document.getElementById('edit-modal').style.display = 'flex';
    }}

    function addDescField(value = '') {{
        const row = document.createElement('div'); row.style.display='flex'; row.style.gap='10px'; row.style.marginBottom='8px';
        row.innerHTML = `<input type='text' class='desc-input' value='${{value}}' style='flex:1;padding:10px;border-radius:8px;border:none;background:rgba(255,255,255,0.1);color:white;'><button class='icon-btn' style='background:#ff4757;' onclick='this.parentElement.remove()'><i class='fa-solid fa-times'></i></button>`;
        document.getElementById('desc-container').appendChild(row);
    }}

    async function saveChanges() {{
        const id = document.getElementById('edit-id').value;
        const descriptions = Array.from(document.querySelectorAll('.desc-input')).map(input => input.value).filter(v => v.trim() !== '');
        await fetch('/items/' + id, {{ method: 'PUT', headers: {{'Content-Type': 'application/json'}}, body: JSON.stringify({{ title: document.getElementById('edit-title').value, descriptions }}) }});
        closeModal('edit-modal'); load();
    }}

    function openDeleteModal(id) {{ document.getElementById('delete-id').value = id; document.getElementById('delete-modal').style.display = 'flex'; }}
    function openResetModal() {{ document.getElementById('reset-modal').style.display = 'flex'; }}
    function closeModal(id) {{ document.getElementById(id).style.display = 'none'; }}
    async function confirmDelete() {{ await fetch('/items/' + document.getElementById('delete-id').value, {{ method: 'DELETE' }}); closeModal('delete-modal'); load(); }}
    async function confirmReset() {{ await fetch('/items/reset', {{ method: 'POST' }}); closeModal('reset-modal'); load(); }}
    async function toggle(id) {{ await fetch('/items/'+id+'/toggle', {{method:'PUT'}}); load(); }}
    
    load();
    </script></body></html>";
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.WriteAsync(html, Encoding.UTF8);
});

app.MapGet("/items", async (HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    return Results.Ok(await GetItemsAsync(email));
});

app.MapPut("/items/reorder", async (List<int> sortedIds, HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    var items = await GetItemsAsync(email);
    for (int i = 0; i < sortedIds.Count; i++) {
        var item = items.FirstOrDefault(x => x.Id == sortedIds[i]);
        if (item != null) item.Order = i;
    }
    await SaveItemsAsync(email, items);
    return Results.Ok();
});

app.MapPut("/items/{id}/toggle", async (int id, HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    var items = await GetItemsAsync(email);
    var item = items.FirstOrDefault(x => x.Id == id);
    if (item != null) { item.Done = !item.Done; await SaveItemsAsync(email, items); }
    return Results.Ok();
});

app.MapPut("/items/bulk", async (BulkStatusRequest req, HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    var items = await GetItemsAsync(email);
    foreach(var item in items) if(req.Ids.Contains(item.Id)) item.Done = req.Status;
    await SaveItemsAsync(email, items);
    return Results.Ok();
});

app.MapPut("/items/{id}", async (int id, ItemUpdate req, HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    var items = await GetItemsAsync(email);
    var item = items.FirstOrDefault(x => x.Id == id);
    if (item != null) { item.Title = req.Title; item.Descriptions = req.Descriptions; await SaveItemsAsync(email, items); }
    return Results.Ok();
});

app.MapDelete("/items/{id}", async (int id, HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    var items = await GetItemsAsync(email);
    items.RemoveAll(x => x.Id == id);
    await SaveItemsAsync(email, items);
    return Results.Ok();
});

app.MapPost("/items/reset", async (HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    await cache.RemoveAsync($"checklist_{email}");
    return Results.Ok();
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"Uygulama {port} portu üzerinde başlatılıyor...");
app.Run($"http://0.0.0.0:{port}");

public class Item {
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public List<string> Descriptions { get; set; } = [];
    public bool Done { get; set; }
    public int Order { get; set; }
    public Item(int id, string title, List<string> descriptions, int order = 0) { Id = id; Title = title; Descriptions = descriptions; Order = order; }
    public Item() { }
}
public class User {
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Name { get; set; } = "";
    public string Surname { get; set; } = "";
    public User(string email, string password, string name, string surname) { Email = email; Password = password; Name = name; Surname = surname; }
    public User() { }
}
public class LoginRequest { public string email { get; set; } = ""; public string pw { get; set; } = ""; }
public class ItemUpdate { public string Title { get; set; } = ""; public List<string> Descriptions { get; set; } = []; }
public class BulkStatusRequest { public List<int> Ids { get; set; } = []; public bool Status { get; set; } }