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
        return items;
    }
    return JsonSerializer.Deserialize<List<Item>>(jsonData)!;
}

async Task SaveItemsAsync(string email, List<Item> items)
{
    var key = $"checklist_{email}";
    await cache.SetStringAsync(key, JsonSerializer.Serialize(items));
}

List<Item> SeedItems() =>
[
    new(1, "Pasaport",
        ["Vize bitişinden sonra +90 gün geçerlilik", "En az 2 boş sayfa", "Tüm işlem görmüş sayfaların fotokopisi"]),
    new(2, "Kimlik Fotokopisi", []),
    new(3, "Başvuru Formu", ["Doldurulmuş, imzalanmış ve tarihli"]),
    new(4, "Biyometrik Fotoğraf", ["2 adet, 35x40 mm", "Açık renk fonlu"]),
    new(5, "Sağlık Sigortası",
        ["Min. 30.000 Euro teminat", "Seyahat tarihlerini (+/- 1 gün) kapsamalı"]),
    new(6, "Vize Harcı", ["iDATA ofislerinde nakit ödenecek"]),
    new(7, "Yerleşim Yeri Belgesi", ["E-devletten barkodlu"]),
    new(8, "Nüfus Kayıt Örneği", ["Tam vukuatlı, barkodlu"]),
    new(9, "Dilekçe / Bildirge", ["Seyahat detayları", "Gelir beyanı", "Yol arkadaşı bilgileri"]),
    new(10, "Uçak Rezervasyonu", ["Gidiş-dönüş tarihli", "İsimler doğru olmalı"]),
    new(11, "Otel Rezervasyonu", ["Tarihleri kapsayan", "İsim onaylı"]),
    new(12, "Faaliyet Belgesi", ["Son 3 ay içinde alınmış"]),
    new(13, "Ticaret Sicil Gazetesi", []),
    new(14, "Vergi Levhası", []),
    new(15, "Maddi Gelir Evrakları",
        ["Banka dökümü (6 ay)", "Maaş bordrosu (3 ay)", "Ruhsatlar"]),
    new(16, "Çalışma & İzin Belgesi", ["Antetli kağıtta, kaşeli", "4a hizmet dökümü"]),
    new(17, "İmza Sirküleri", []),
    new(18, "Çalışma Evrakları", ["İşe giriş bildirgesi", "Barkodlu hizmet dökümü"]),
    new(19, "Teminat", ["İtalya Bakanlık tutarlarına uygun"])
];

app.MapGet("/", async ctx =>
{
    var html = @"<!DOCTYPE html><html lang='tr'><head><meta charset='UTF-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'><title>iDATA Giriş</title></head>
    <body style='font-family:sans-serif; background:linear-gradient(135deg, #667eea, #764ba2); margin:0; height:100vh; display:flex; justify-content:center; align-items:center; color:white'>
    <div style='width:100%; height:70px; background:rgba(0,0,0,0.3); position:fixed; top:0; left:0; right:0; display:flex; align-items:center; justify-content:center; font-weight:bold; font-size:20px; backdrop-filter:blur(10px); z-index:1000;'>iDATA Evrak Takip Servisi</div>
    <div style='background:rgba(255, 255, 255, 0.15); backdrop-filter:blur(15px); padding:40px; border-radius:24px; width:340px; text-align:center; border:1px solid rgba(255,255,255,0.2)'>
        <h2 style='font-weight:300;'>Giriş Yap</h2>
        <form onsubmit='event.preventDefault(); login();'>
            <input id='email' type='text' placeholder='Kullanıcı Adı' required style='width:100%; padding:14px; margin-bottom:15px; border-radius:12px; border:none; box-sizing:border-box;'/>
            <input id='pw' type='password' placeholder='Şifre' required style='width:100%; padding:14px; margin-bottom:15px; border-radius:12px; border:none; box-sizing:border-box;'/>
            <div id='error-msg' style='color:#ffb3b3; font-size:14px; margin-bottom:15px; font-weight:bold;'></div>
            <button type='submit' style='width:100%; padding:14px; border-radius:12px; border:none; background:#764ba2; color:white; font-weight:bold; cursor:pointer;'>Giriş</button>
        </form>
    </div>
    <script>
    async function login(){
        const email = document.getElementById('email').value;
        const pw = document.getElementById('pw').value;
        const res = await fetch('/login', { method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify({ email, pw }) });
        if(res.ok) location = '/app'; else document.getElementById('error-msg').innerText = 'Hatalı giriş!';
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
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; background: linear-gradient(135deg,#667eea,#764ba2); margin:0; color:white; padding-top:130px; padding-bottom:50px; min-height: 100vh; }}
        .header-container {{ position: fixed; top: 0; left: 0; right: 0; z-index: 1000; background: rgba(0,0,0,0.5); backdrop-filter: blur(15px); }}
        .header {{ height: 70px; padding: 0 30px; display: flex; justify-content: space-between; align-items: center; }}
        .progress-wrapper {{ height: 30px; background: rgba(255,255,255,0.1); position: relative; display: flex; align-items: center; overflow: hidden; }}
        #progress-bar {{ height: 100%; background: linear-gradient(90deg, #4facfe 0%, #00f2fe 100%); width: 0%; transition: width 0.5s ease; }}
        #progress-text {{ position: absolute; width: 100%; text-align: center; font-size: 12px; font-weight: bold; text-shadow: 1px 1px 2px rgba(0,0,0,0.8); }}
        .grid-container {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; padding: 20px; max-width: 1400px; margin: auto; }}
        
        #congrats-msg {{ grid-column: 1 / -1; background: rgba(46, 213, 115, 0.2); border: 2px solid #2ed573; color: #2ed573; padding: 20px; border-radius: 16px; text-align: center; font-size: 1.5rem; font-weight: bold; margin-bottom: 10px; display: none; animation: pulse 2s infinite; }}
        @keyframes pulse {{ 0% {{ transform: scale(1); }} 50% {{ transform: scale(1.02); }} 100% {{ transform: scale(1); }} }}

        .item-card {{ background: rgba(255, 255, 255, 0.12); padding: 20px; border-radius: 16px; border: 1px solid rgba(255, 255, 255, 0.1); position: relative; cursor: pointer; transition: 0.3s; min-height: 120px; }}
        .item-card:hover {{ background: rgba(255, 255, 255, 0.2); transform: translateY(-3px); }}
        .done-card {{ opacity: 0.8; background: rgba(255, 255, 255, 0.05); border: 1px solid rgba(173, 255, 47, 0.3);}}
        .done-text {{ text-decoration: line-through; color: #adff2f; }}
        .card-actions {{ position: absolute; bottom: 12px; right: 12px; display: flex; gap: 8px; }}
        
        .icon-btn {{ background: rgba(0,0,0,0.3); color: white; border: none; width: 32px; height: 32px; border-radius: 8px; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: 0.2s; }}
        .icon-btn:hover:not(:disabled) {{ background: rgba(255,255,255,0.2); }}
        .icon-btn:disabled {{ opacity: 0.2; cursor: not-allowed; filter: grayscale(1); }}

        /* MODAL STYLES */
        .modal-overlay {{ position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); backdrop-filter: blur(5px); display: none; justify-content: center; align-items: center; z-index: 2000; }}
        .modal-content {{ background: #1e1e2e; width: 90%; max-width: 500px; padding: 30px; border-radius: 20px; border: 1px solid rgba(255,255,255,0.1); box-shadow: 0 10px 40px rgba(0,0,0,0.5); }}
        .modal-content h3 {{ margin-top: 0; font-weight: 400; }}
        .form-group {{ margin-bottom: 20px; }}
        .form-group label {{ display: block; margin-bottom: 8px; font-size: 14px; opacity: 0.7; }}
        .form-input {{ width: 100%; padding: 12px; border-radius: 10px; border: 1px solid rgba(255,255,255,0.1); background: rgba(255,255,255,0.05); color: white; box-sizing: border-box; outline: none; }}
        .desc-row {{ display: flex; gap: 10px; margin-bottom: 8px; }}
        .modal-footer {{ display: flex; justify-content: flex-end; gap: 12px; margin-top: 25px; }}
        
        .btn {{ padding: 10px 20px; border-radius: 10px; border: none; cursor: pointer; font-weight: bold; transition: 0.2s; display: flex; align-items: center; gap: 8px; }}
        .btn-primary {{ background: #764ba2; color: white; }}
        .btn-secondary {{ background: rgba(255,255,255,0.1); color: white; }}
        .btn-danger {{ background: #ff4757; color: white; }}
        .btn-add {{ background: #2ed573; color: white; width: 100%; margin-top: 5px; justify-content: center; }}
        .btn:hover {{ filter: brightness(1.2); transform: scale(1.02); }}

        .btn-reset {{ background: #ff4757; color: white; border: none; padding: 8px 16px; border-radius: 8px; font-weight: bold; cursor: pointer; display: flex; align-items: center; gap: 6px; }}
        ul {{ margin: 10px 0 30px 0; padding-left: 20px; font-size: 13px; opacity: 0.8; pointer-events: none; }}
    </style></head><body>
    
    <div id='edit-modal' class='modal-overlay'>
        <div class='modal-content'>
            <h3><i class='fa-solid fa-pen-to-square'></i> Evrak Düzenle</h3>
            <input type='hidden' id='edit-id'>
            <div class='form-group'>
                <label>Evrak Adı</label>
                <input type='text' id='edit-title' class='form-input' placeholder='Örn: Pasaport'>
            </div>
            <div class='form-group'>
                <label>Detaylar</label>
                <div id='desc-container'></div>
                <button type='button' class='btn btn-add' onclick='addDescField()'><i class='fa-solid fa-plus'></i> Madde Ekle</button>
            </div>
            <div class='modal-footer'>
                <button class='btn btn-secondary' onclick='closeModal(""edit-modal"")'>Vazgeç</button>
                <button class='btn btn-primary' onclick='saveChanges()'><i class='fa-solid fa-save'></i> Kaydet</button>
            </div>
        </div>
    </div>

    <div id='delete-modal' class='modal-overlay'>
        <div class='modal-content' style='max-width: 400px; text-align: center;'>
            <div style='font-size: 50px; color: #ff4757; margin-bottom: 15px;'><i class='fa-solid fa-circle-exclamation'></i></div>
            <h3>Emin misiniz?</h3>
            <p style='opacity: 0.8; font-size: 14px;'>Bu evrak kalıcı olarak silinecektir. Bu işlemi geri alamazsınız.</p>
            <input type='hidden' id='delete-id'>
            <div class='modal-footer' style='justify-content: center;'>
                <button class='btn btn-secondary' onclick='closeModal(""delete-modal"")'>Vazgeç</button>
                <button class='btn btn-danger' onclick='confirmDelete()'><i class='fa-solid fa-trash'></i> Evet, Sil</button>
            </div>
        </div>
    </div>

    <div id='reset-modal' class='modal-overlay'>
        <div class='modal-content' style='max-width: 400px; text-align: center;'>
            <div style='font-size: 50px; color: #ffa502; margin-bottom: 15px;'><i class='fa-solid fa-rotate-left'></i></div>
            <h3>Listeyi Sıfırla?</h3>
            <p style='opacity: 0.8; font-size: 14px;'>Tüm ilerlemeniz silinecek ve liste varsayılan evraklara geri dönecektir.</p>
            <div class='modal-footer' style='justify-content: center;'>
                <button class='btn btn-secondary' onclick='closeModal(""reset-modal"")'>Vazgeç</button>
                <button class='btn btn-danger' onclick='confirmReset()'><i class='fa-solid fa-check'></i> Evet, Sıfırla</button>
            </div>
        </div>
    </div>

    <div class='header-container'>
        <div class='header'>
            <div style='font-weight:bold; font-size: 1.2rem;'><i class='fa-solid fa-passport'></i> iDATA Evrak Takip</div>
            <div style='display:flex; align-items:center;'>
                <button onclick='openResetModal()' class='btn-reset' style='margin-right:20px;'><i class='fa-solid fa-rotate-left'></i> Sıfırla</button>
                <span>👤 <b>{user.Name}</b></span> <a href='/logout' style='color:white; margin-left:15px; text-decoration:none;'><i class='fa-solid fa-right-from-bracket'></i></a>
            </div>
        </div>
        <div class='progress-wrapper'>
            <div id='progress-bar'></div>
            <div id='progress-text'>İlerleme: %0</div>
        </div>
    </div>
    
    <div class='grid-container'>
        <div id='congrats-msg'>
            <i class='fa-solid fa-circle-check'></i> Tebrikler, Vize Randevusuna Hazırsınız 😊
        </div>
        <div id='list' style='display: contents;'></div>
    </div>

    <script>
    let currentItems = [];

    async function load(){{
        const res = await fetch('/items');
        currentItems = await res.json();
        
        const total = currentItems.length;
        const done = currentItems.filter(x => x.done).length;
        const percent = total > 0 ? Math.round((done / total) * 100) : 0;
        
        document.getElementById('progress-bar').style.width = percent + '%';
        document.getElementById('progress-text').innerText = 'Tamamlanan: ' + done + ' / ' + total + ' ( %' + percent + ')';
        
        const congrats = document.getElementById('congrats-msg');
        congrats.style.display = (total > 0 && done === total) ? 'block' : 'none';

        const list = document.getElementById('list');
        list.innerHTML = '';
        currentItems.forEach(i => {{
            const card = document.createElement('div');
            card.className = 'item-card' + (i.done ? ' done-card' : '');
            card.onclick = (e) => {{ if(!e.target.closest('.icon-btn')) toggle(i.id); }};
            
            card.innerHTML = `
                <div style='display:flex; align-items:center; gap:10px;'>
                    <i class='fa-regular ${{i.done ? 'fa-circle-check' : 'fa-circle'}}' style='font-size:1.2rem; color:${{i.done ? '#adff2f' : 'white'}}'></i>
                    <b class='${{i.done ? 'done-text' : ''}}'>${{i.title}}</b>
                </div>
                <ul>${{i.descriptions.map(d => `<li>${{d}}</li>`).join('')}}</ul>
                <div class='card-actions'>
                    <button class='icon-btn' ${{i.done ? 'disabled' : ''}} onclick='event.stopPropagation(); openEditModal(${{i.id}})' title='Düzenle'>
                        <i class='fa-solid fa-pen'></i>
                    </button>
                    <button class='icon-btn' ${{i.done ? 'disabled' : ''}} onclick='event.stopPropagation(); openDeleteModal(${{i.id}})' title='Sil'>
                        <i class='fa-solid fa-trash'></i>
                    </button>
                </div>`;
            list.appendChild(card);
        }});
    }}

    function openEditModal(id) {{
        const item = currentItems.find(x => x.id === id);
        if(!item) return;
        document.getElementById('edit-id').value = id;
        document.getElementById('edit-title').value = item.title;
        const container = document.getElementById('desc-container');
        container.innerHTML = '';
        item.descriptions.forEach(desc => addDescField(desc));
        document.getElementById('edit-modal').style.display = 'flex';
    }}

    function openDeleteModal(id) {{
        document.getElementById('delete-id').value = id;
        document.getElementById('delete-modal').style.display = 'flex';
    }}

    function openResetModal() {{
        document.getElementById('reset-modal').style.display = 'flex';
    }}

    function closeModal(modalId) {{
        document.getElementById(modalId).style.display = 'none';
    }}

    function addDescField(value = '') {{
        const container = document.getElementById('desc-container');
        const row = document.createElement('div');
        row.className = 'desc-row';
        row.innerHTML = `
            <input type='text' class='form-input desc-input' value='${{value.replace(/'/g, ""&apos;"")}}'>
            <button class='icon-btn' style='background:#ff4757;' onclick='this.parentElement.remove()'><i class='fa-solid fa-times'></i></button>
        `;
        container.appendChild(row);
    }}

    async function saveChanges() {{
        const id = document.getElementById('edit-id').value;
        const title = document.getElementById('edit-title').value;
        const descInputs = document.querySelectorAll('.desc-input');
        const descriptions = Array.from(descInputs).map(input => input.value).filter(v => v.trim() !== '');

        await fetch('/items/' + id, {{
            method: 'PUT',
            headers: {{'Content-Type': 'application/json'}},
            body: JSON.stringify({{ title, descriptions }})
        }});
        closeModal('edit-modal');
        load();
    }}

    async function confirmDelete() {{
        const id = document.getElementById('delete-id').value;
        await fetch('/items/' + id, {{ method: 'DELETE' }});
        closeModal('delete-modal');
        load();
    }}

    async function confirmReset() {{
        await fetch('/items/reset', {{ method: 'POST' }});
        closeModal('reset-modal');
        load();
    }}

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

app.MapPut("/items/{id}/toggle", async (int id, HttpContext ctx) => {
    var email = ctx.Request.Cookies["user"];
    if (string.IsNullOrEmpty(email)) return Results.Unauthorized();
    var items = await GetItemsAsync(email);
    var item = items.FirstOrDefault(x => x.Id == id);
    if (item != null) { item.Done = !item.Done; await SaveItemsAsync(email, items); }
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
    public Item(int id, string title, List<string> descriptions) { Id = id; Title = title; Descriptions = descriptions; }
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