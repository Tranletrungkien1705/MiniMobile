using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MiniMobile.Services;
using Serilog;

// Giữ nguyên claim type như IdP phát ("role","name","email","sub") — không remap kiểu cũ.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

FleetObs.ConfigureLogger("minimobile");
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
builder.WebHost.UseUrls($"http://0.0.0.0:{Environment.GetEnvironmentVariable("PORT") ?? "8080"}");

// Định danh: TIN token do MiniSSO cấp (OIDC). Authority → tự nạp discovery + JWKS để xác thực RS256.
var ssoAuthority = Environment.GetEnvironmentVariable("SSO_AUTHORITY") ?? "https://minisso.onrender.com";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = ssoAuthority;
        o.RequireHttpsMetadata = ssoAuthority.StartsWith("https");
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = ssoAuthority,
            ValidateAudience = false,     // BFF chấp mọi client của IdP
            ValidateLifetime = true,
            NameClaimType = "name", RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpClient();
builder.Services.AddFleetObs();

var app = builder.Build();
app.UseFleetObs();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => "ok");

// Đăng nhập: BFF đổi email/mật khẩu lấy token từ MiniSSO (Resource Owner Password) — app mobile giữ token.
app.MapPost("/api/auth/login", async (LoginDto dto, IHttpClientFactory hf) =>
{
    var http = hf.CreateClient();
    var form = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"] = "password", ["client_id"] = "fleet-service", ["client_secret"] = "service-secret-demo",
        ["username"] = dto.Email ?? "", ["password"] = dto.Password ?? "", ["scope"] = "openid profile email roles"
    });
    var resp = await http.PostAsync($"{ssoAuthority}/oauth/token", form);
    var body = await resp.Content.ReadAsStringAsync();
    return Results.Content(body, "application/json", null, (int)resp.StatusCode);
});

// Hồ sơ người dùng (từ token đã xác thực bởi MiniSSO)
app.MapGet("/api/me", (ClaimsPrincipal u) => Results.Ok(new
{
    sub = u.FindFirstValue("sub"),
    name = u.FindFirstValue("name"),
    email = u.FindFirstValue("email"),
    tenant = u.FindFirstValue("tenant"),
    roles = u.FindAll("role").Select(c => c.Value)
})).RequireAuthorization();

// Màn hình chính mobile (BFF): tổng hợp từ nhiều dịch vụ fleet + module tiles.
app.MapGet("/api/home", async (ClaimsPrincipal u, IHttpClientFactory hf) =>
{
    var http = hf.CreateClient();
    http.Timeout = TimeSpan.FromSeconds(12);
    // Widget "live" thật: tổng tồn kho từ MiniWMS (fallback nếu dịch vụ ngủ).
    int? stock = null;
    try
    {
        using var r = await http.GetAsync("https://miniwms.onrender.com/api/balance");
        if (r.IsSuccessStatusCode)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            stock = doc.RootElement.EnumerateArray().Sum(x => x.GetProperty("qty").GetInt32());
        }
    }
    catch { }

    return Results.Ok(new
    {
        greeting = $"Chào {u.FindFirstValue("name")}",
        tenant = u.FindFirstValue("tenant"),
        widgets = new object[]
        {
            new { title = "Tồn kho (MiniWMS)", value = stock?.ToString("N0") ?? "—", unit = "sản phẩm", icon = "box-seam", live = stock != null },
            new { title = "Đơn hàng chờ xử lý", value = "3", unit = "đơn", icon = "cart", live = false },
            new { title = "Thông báo mới", value = "2", unit = "", icon = "bell", live = false }
        },
        modules = new object[]
        {
            new { name = "Đơn hàng", icon = "cart", url = "https://minidms.onrender.com" },
            new { name = "Kho", icon = "hdd-stack", url = "https://miniwms.onrender.com" },
            new { name = "Bán xe", icon = "car-front", url = "https://minishowroom.onrender.com" },
            new { name = "Dịch vụ", icon = "tools", url = "https://miniservice-hytf.onrender.com" },
            new { name = "Bảo hiểm", icon = "shield-shaded", url = "https://miniinsurance.onrender.com" },
            new { name = "Truy xuất", icon = "diagram-3", url = "https://minitrace.onrender.com" }
        }
    });
}).RequireAuthorization();

app.Run();

record LoginDto(string? Email, string? Password);
