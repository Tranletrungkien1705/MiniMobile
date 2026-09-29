using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MiniMobile.Models;
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
FleetObs.ReportLicense(ssoAuthority, "minimobile");
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

// ============================================================================
// PORT 1:1 — dự án nguồn 2019.3.HTC.TV.MobileApp (ERP.HTCMobile.Web)
//
// Nguồn định tuyến theo "api/{controller}/{action}/{id}"
// (ERP.HTCMobile.Web/App_Start/WebApiConfig.cs:19-22) nên đường dẫn dưới đây giữ
// NGUYÊN tên controller + tên action của nguồn, không đổi sang dạng REST.
//
// Ba điều giữ đúng ở CẢ BỐN endpoint (nếu bỏ là lệch hợp đồng với client mobile):
//   1) Tất cả là POST, thân JSON — nguồn khai [AcceptVerbs("POST")]
//      (AppointmentServiceController.cs:24, :122; HTCReportController.cs:207, :301).
//   2) Chốt cổng giao dịch GwUserCode/GwPassword chạy TRƯỚC mọi xử lý
//      (AppointmentServiceController.cs:47-54).
//   3) Lỗi vẫn trả HTTP 200, chi tiết nhồi vào "Message"; thành công Message = "0"
//      (ApiControllerBase.cs:37-44, :702-743).
// ============================================================================

// Màn "Đặt hẹn dịch vụ" — tạo phiếu hẹn.
// Nguồn: ERP.HTCMobile.Web/Controllers/AppointmentServiceController.cs:24-120
//      → ERP.HTCMobile.Biz/HTCMobileTVO.cs:554-700 (CreateServiceApp) → WS Ser_App_Create.
app.MapPost("/api/AppointmentService/CreateServiceApp", (CreateServiceAppInput input) =>
{
    var payload = new CreateServiceAppResult();   // AppointmentServiceController.cs:30
    try
    {
        HtcMobileGuards.CheckGatewayAuthentication_OS(input.GwUserCode, input.GwPassword);

        var row = HtcMobileStore.CreateSerApp(input);
        // Nguồn chỉ lấy DUY NHẤT AppNo từ Remark của MyDataSet — AppointmentServiceController.cs:95-96.
        payload.AppNo = row.AppNo;

        return Results.Ok(new ServiceResultMobileSVCreateServiceApp<CreateServiceAppResult>
        {
            CreateServiceApp = payload, Message = "0"   // ApiControllerBase.cs:80-87
        });
    }
    catch (Exception ex)
    {
        // ErrorMobileServiceServiceApp — ApiControllerBase.cs:354-375: vẫn trả payload, đổi Message.
        return Results.Ok(new ServiceResultMobileSVCreateServiceApp<CreateServiceAppResult>
        {
            CreateServiceApp = payload, Message = HtcMobileGuards.BuildErrorMessage(ex)
        });
    }
});

// Màn "Đặt hẹn dịch vụ" — tra cứu danh sách phiếu hẹn.
// Nguồn: AppointmentServiceController.cs:122-214 → HTCMobileTVO.cs:423-552 (GetServiceApp).
app.MapPost("/api/AppointmentService/ServiceApp_Get", (GetServiceAppInput input) =>
{
    var payload = new GetServiceAppResult();      // AppointmentServiceController.cs:128
    try
    {
        HtcMobileGuards.CheckGatewayAuthentication_OS(input.GwUserCode, input.GwPassword);

        // THỨ TỰ 4 bước tinh chỉnh của nguồn — HTCMobileTVO.cs:493-497. Ở hàm này
        // mốc cuối được ĐẶT MẶC ĐỊNH TRƯỚC rồi mới StdDTime; hàm thẻ hội viên làm
        // ngược lại (xem endpoint GetMembershipCards) — khác thật, không gộp chung.
        var count = HtcMobileGuards.RefineCount(input.Count);                       // :493-494
        var strFromDate = HtcMobileGuards.StdDTime(input.FromDate);                 // :495
        var strToDateRaw = string.IsNullOrEmpty(input.ToDate)                       // :496
            ? HtcMobileGuards.ToDateDefault : input.ToDate;
        var strToDate = HtcMobileGuards.StdDTime(strToDateRaw);                     // :497

        // Nguồn CHỈ đẩy strCount sang WS (:508), không có mốc đầu/cuối bản ghi ⇒
        // hiệu lực đúng bằng "lấy count dòng đầu".
        var (start, end) = HtcMobileGuards.FilterRecordRange(count);
        var (itemCount, items) = HtcMobileStore.GetSerApp(strFromDate, strToDate, start, end);

        // ItemCount = MySummaryTable.MyCount (AppointmentServiceController.cs:189),
        // NHƯNG bảng MySummaryTable của màn này được đếm SAU khi cắt trang
        // (2021.1.TCMotor/.../TERP.BizCarSv/BizCarSv.TVO.cs:1023 đếm trên
        // #tbl_Ser_App_Filter) ⇒ nó BẰNG số dòng trả về. Khác màn thẻ hội viên,
        // nơi MyCount đếm trên bảng nháp TRƯỚC khi cắt.
        payload.ItemCount = itemCount;
        payload.Items = items;

        // SuccessMobile đặt payload vào thuộc tính tên "Customers" — ApiControllerBase.cs:37-44.
        return Results.Ok(new ServiceResultMobile<GetServiceAppResult>
        {
            Customers = payload, Message = "0"
        });
    }
    catch (Exception ex)
    {
        // ErrorMoblie (nguồn viết sai chính tả tên hàm) — ApiControllerBase.cs:249-268.
        return Results.Ok(new ServiceResultMobile<GetServiceAppResult>
        {
            Customers = payload, Message = HtcMobileGuards.BuildErrorMessage(ex)
        });
    }
});

// Màn "Nhắc bảo dưỡng".
// Nguồn: ERP.HTCMobile.Web/Controllers/HTCReportController.cs:207-297
//      → ERP.HTCMobile.Biz/HTCMobileTVO.cs:297-420 (GetServiceReminders).
app.MapPost("/api/HTCReport/GetServiceReminders", (GetServiceRemindersInput input) =>
{
    var payload = new GetServiceRemindersResult();
    try
    {
        HtcMobileGuards.CheckGatewayAuthentication_OS(input.GwUserCode, input.GwPassword);

        // HTCMobileTVO.cs:365-367 — chỉ MỘT mốc ngày, StdDTime trước rồi mới chốt Count.
        var strDate = HtcMobileGuards.StdDTime(input.Date);
        var count = HtcMobileGuards.RefineCount(input.Count);
        var (start, end) = HtcMobileGuards.FilterRecordRange(count);

        var items = HtcMobileStore.GetSerRo(strDate, start, end);

        // KHÁC BIỆT CÓ THẬT, phải giữ: màn này lấy ItemCount = SỐ DÒNG ĐÃ TRẢ,
        // không phải MySummaryTable.MyCount — dòng dùng MyCount bị COMMENT ở nguồn
        // (HTCReportController.cs:273). Dùng MyCount ở đây là port SAI.
        payload.ItemCount = items.Count;
        payload.Items = items;

        return Results.Ok(new ServiceResultMobileSVReminders<GetServiceRemindersResult>
        {
            ServiceReminders = payload, Message = "0"   // ApiControllerBase.cs:63-70
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new ServiceResultMobileSVReminders<GetServiceRemindersResult>
        {
            ServiceReminders = payload, Message = HtcMobileGuards.BuildErrorMessage(ex)
        });
    }
});

// Màn "Thẻ hội viên".
// Nguồn: ERP.HTCMobile.Web/Controllers/HTCReportController.cs:301-390
//      → HTCMobileTVO.cs:1107-1241 GetMembershipCards_New20190310 (BẢN LIVE, controller
//        gọi ở :335). Bản HTCMobileTVO.cs:843-1241 KHÔNG được gọi — bẫy sinh đôi.
app.MapPost("/api/HTCReport/GetMembershipCards", (MembershipCardsInput input) =>
{
    var payload = new GetMembershipCardsResult();
    try
    {
        HtcMobileGuards.CheckGatewayAuthentication_OS(input.GwUserCode, input.GwPassword);

        // THỨ TỰ của BẢN LIVE — HTCMobileTVO.cs:1168-1173: StdDTime cho CẢ HAI mốc
        // TRƯỚC, rồi mới thay mặc định nếu mốc cuối rỗng. Bản chết :904-906 làm ngược
        // lại và còn gọi strToDate.Trim() khi chưa chắc khác null ⇒ bản chết NRE với
        // ToDate = null; bản live có chặn "== null" nên an toàn. Port theo bản LIVE.
        var strFromDate = HtcMobileGuards.StdDTime(input.FromDate);                 // :1168
        var strToDate = HtcMobileGuards.StdDTime(input.ToDate);                     // :1169
        if (string.IsNullOrEmpty(strToDate?.Trim()))                                // :1170
            strToDate = HtcMobileGuards.ToDateDefault;
        var count = HtcMobileGuards.RefineCount(input.Count);                       // :1172-1173

        // Bản LIVE có tính nFilterRecordStart/End (:1176-1177) nhưng KHÔNG đẩy sang WS
        // (:1190-1197 chỉ gửi from/to/count) ⇒ hai biến đó VÔ HIỆU ở bản live. Không
        // bịa thêm phân trang; hiệu lực đúng bằng "lấy count dòng đầu".
        var (start, end) = HtcMobileGuards.FilterRecordRange(count);
        var (itemCount, items) = HtcMobileStore.GetCrdCard(strFromDate, strToDate, start, end);

        // HTCReportController.cs:366-367 — ItemCount = MySummaryTable.MyCount (tổng trước cắt).
        payload.ItemCount = itemCount;
        payload.Items = items;

        return Results.Ok(new ServiceResultMobileMembershipCards<GetMembershipCardsResult>
        {
            MembershipCards = payload, Message = "0"   // ApiControllerBase.cs:88-95
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new ServiceResultMobileMembershipCards<GetMembershipCardsResult>
        {
            MembershipCards = payload, Message = HtcMobileGuards.BuildErrorMessage(ex)
        });
    }
});

app.Run();

record LoginDto(string? Email, string? Password);
