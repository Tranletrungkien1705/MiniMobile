// ============================================================================
// Port 1:1 các DTO của dự án nguồn 2019.3.HTC.TV.MobileApp (ERP.HTCMobile).
// Giữ NGUYÊN tên thuộc tính của nguồn (kể cả tiền tố "str" ở lớp Input) để hợp
// đồng JSON của app di động không đổi — đây là API cho client mobile, đổi tên là
// phá client.
//
// Nguồn tham chiếu (đường dẫn tương đối từ D:\idocNet\2019.3.HTC.TV.MobileApp\Dev):
//   ERP.HTCMobile.Common/Models/WARQBase.cs:9-29
//   ERP.HTCMobile.Common/Models/CreateServiceAppInput.cs:10-24
//   ERP.HTCMobile.Common/Models/CreateServiceApp.cs:9-12
//   ERP.HTCMobile.Common/Models/GetServiceAppInput.cs:9-14
//   ERP.HTCMobile.Common/Models/GetServiceApp.cs:9-13
//   ERP.HTCMobile.Common/Models/DMSSV_GetServiceApp.cs:9-29
//   ERP.HTCMobile.Common/Models/GetServiceRemindersInput.cs:9-13
//   ERP.HTCMobile.Common/Models/GetServiceReminders.cs:9-13
//   ERP.HTCMobile.Common/Models/DMSSV_GetServiceReminders.cs:9-25
//   ERP.HTCMobile.Common/Models/MembershipCardsInput.cs:9-15
//   ERP.HTCMobile.Common/Models/GetMembershipCards.cs:9-13
//   ERP.HTCMobile.Common/Models/Membership_GetMembershipCards.cs:10-53
//   ERP.HTCMobile.Common/Models/ServiceResultMobile.cs:8-47
// ============================================================================

namespace MiniMobile.Models;

/// <summary>
/// Gốc mọi request "WA" của nguồn — ERP.HTCMobile.Common/Models/WARQBase.cs:9-29.
/// Chỉ GwUserCode/GwPassword được dùng thật ở tầng controller (chốt cổng giao dịch,
/// xem ERP.HTCMobile.Web/Controllers/AppointmentServiceController.cs:48-53);
/// các trường session (TokenID/AccessToken/RefreshToken...) nguồn khai nhưng KHÔNG
/// đọc trong 4 hàm được port ở đây — giữ lại để hợp đồng JSON không thiếu trường.
/// </summary>
public class WARQBase
{
    public string? Tid { get; set; }
    public string? NetworkID { get; set; }
    public string? OrgID { get; set; }
    public string? TokenID { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? UtcOffset { get; set; }
    public string? GwUserCode { get; set; }
    public string? GwPassword { get; set; }
    public string? WAUserCode { get; set; }
}

// ---------------------------------------------------------------------------
// Màn 1 — Đặt hẹn dịch vụ (bảng Ser_App)
// ---------------------------------------------------------------------------

/// <summary>Input tạo phiếu hẹn — CreateServiceAppInput.cs:12-23 (đủ 12 trường).</summary>
public class CreateServiceAppInput : WARQBase
{
    public string? strCustomerName { get; set; }
    public string? strCustomerMobile { get; set; }
    public string? strCustomerIDCardNo { get; set; }
    public string? strCustomerAddress { get; set; }
    public string? strPlateNo { get; set; }
    public string? strTradeMarkCode { get; set; }
    public string? strModelCode { get; set; }
    public string? strAppDateTime { get; set; }
    public string? strAppTime { get; set; }
    public string? strCusRequest { get; set; }
    public string? strCVDVCode { get; set; }
    public string? strDealerCode { get; set; }
}

/// <summary>
/// Kết quả tạo hẹn — CreateServiceApp.cs:11 chỉ có AppNo.
/// Nguồn lấy AppNo từ "Remark" của MyDataSet do WS trả về
/// (AppointmentServiceController.cs:95-96).
/// </summary>
public class CreateServiceAppResult
{
    public string? AppNo { get; set; }
}

/// <summary>Input tra cứu hẹn — GetServiceAppInput.cs:11-13.</summary>
public class GetServiceAppInput : WARQBase
{
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public long Count { get; set; }
}

/// <summary>Bao ngoài danh sách hẹn — GetServiceApp.cs:11-12.</summary>
public class GetServiceAppResult
{
    public long ItemCount { get; set; }
    public List<DMSSV_GetServiceApp> Items { get; set; } = new();
}

/// <summary>
/// Một dòng phiếu hẹn của bảng Ser_App — DMSSV_GetServiceApp.cs:11-28 (18 cột).
/// Mọi cột đều kiểu string ở nguồn (ngày tháng trả về dạng chuỗi) — giữ nguyên.
/// </summary>
public class DMSSV_GetServiceApp
{
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public string? CustomerIDCardNo { get; set; }
    public string? PlateNo { get; set; }
    public string? VIN { get; set; }
    public string? TradeMarkCode { get; set; }
    public string? ModelCode { get; set; }
    public string? ModelName { get; set; }
    public string? AppDateTime { get; set; }
    public string? AppTime { get; set; }
    public string? CusRequest { get; set; }
    public string? DealerCode { get; set; }
    public string? AppCode { get; set; }
    public string? AppNo { get; set; }
    public string? CVDVName { get; set; }
    public string? CreateDateTime { get; set; }
    public string? LogLUDTime { get; set; }
}

// ---------------------------------------------------------------------------
// Màn 2 — Nhắc bảo dưỡng (bảng Ser_RO)
// ---------------------------------------------------------------------------

/// <summary>Input nhắc bảo dưỡng — GetServiceRemindersInput.cs:11-12 (Date, KHÔNG phải From/To).</summary>
public class GetServiceRemindersInput : WARQBase
{
    public string? Date { get; set; }
    public long Count { get; set; }
}

/// <summary>Bao ngoài — GetServiceReminders.cs:11-12.</summary>
public class GetServiceRemindersResult
{
    public long ItemCount { get; set; }
    public List<DMSSV_GetServiceReminders> Items { get; set; } = new();
}

/// <summary>Một dòng nhắc bảo dưỡng của bảng Ser_RO — DMSSV_GetServiceReminders.cs:11-24 (14 cột).</summary>
public class DMSSV_GetServiceReminders
{
    public string? ROID { get; set; }
    public string? RONo { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerMobile { get; set; }
    public string? CustomerIDCardNo { get; set; }
    public string? CarID { get; set; }
    public string? PlateNo { get; set; }
    public string? VIN { get; set; }
    public string? TradeMarkCode { get; set; }
    public string? ModelCode { get; set; }
    public string? ModelName { get; set; }
    public string? ReminderMaintanceDate { get; set; }
    public string? DealerCode { get; set; }
}

// ---------------------------------------------------------------------------
// Màn 3 — Thẻ hội viên (bảng Crd_Card + Crd_Member)
// ---------------------------------------------------------------------------

/// <summary>Input danh sách thẻ — MembershipCardsInput.cs:11-13.</summary>
public class MembershipCardsInput : WARQBase
{
    public string? FromDate { get; set; }
    public string? ToDate { get; set; }
    public long Count { get; set; }
}

/// <summary>Bao ngoài — GetMembershipCards.cs:11-12.</summary>
public class GetMembershipCardsResult
{
    public long ItemCount { get; set; }
    public List<Membership_GetMembershipCards> Items { get; set; } = new();
}

/// <summary>
/// Một dòng thẻ hội viên — Membership_GetMembershipCards.cs:12-52.
/// Đúng 42 thuộc tính đang bật; ba thuộc tính AmountInThePeriod /
/// SpentValueInThePeriod bị COMMENT ở nguồn (:18-19) nên KHÔNG port — đây là
/// bẫy "dòng bị comment" mà SOP_CONVERT_1TO1 §0.7 đã cảnh báo.
/// Cột số ở nguồn là Double (không phải decimal) — giữ nguyên double để khớp
/// cách JSON hoá của nguồn.
/// </summary>
public class Membership_GetMembershipCards
{
    public string? MemberNo { get; set; }
    public string? CardNo { get; set; }
    public string? CardType { get; set; }
    public string? CardTypeName { get; set; }
    public string? CardSourceCode { get; set; }
    public double PointsInThePeriod { get; set; }
    public double TotalAmountMember { get; set; }
    public double TotalSpentAmountMember { get; set; }
    public double Balance { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerIDCardNo { get; set; }
    public string? CustomerGender { get; set; }
    public string? DateOfBirth { get; set; }
    public string? CustomerMobile { get; set; }
    public string? CustomerAddress { get; set; }
    public string? ProvinceCode { get; set; }
    public string? ProvinceName { get; set; }
    public string? DistrictCode { get; set; }
    public string? DistrictName { get; set; }
    public string? PlateNo { get; set; }
    public string? VIN { get; set; }
    public string? TradeMarkCode { get; set; }
    public string? ModelCode { get; set; }
    public string? ModelName { get; set; }
    public string? EffDateStart { get; set; }
    public string? EffDateEnd { get; set; }
    public string? CardStatus { get; set; }
    public string? MemberStatus { get; set; }
    public string? DealerCode { get; set; }
    public string? CreateDateTime { get; set; }
    public string? LogLUDateTime { get; set; }
    public string? DeletionDateTime { get; set; }
    public string? Remark { get; set; }
    public string? DeletionStatus { get; set; }
    public string? RegistrationDate { get; set; }
    public double PointPurchaseAddCar { get; set; }
    public double PointBonus { get; set; }
    public double QtyVisitAvail { get; set; }
}

// ---------------------------------------------------------------------------
// Bao ngoài kết quả — ServiceResultMobile.cs
// Nguồn KHÔNG dùng một tên chung: mỗi API có một tên thuộc tính payload riêng
// (Customers / ServiceReminders / CreateServiceApp / MembershipCards). Port 1:1
// nghĩa là phải giữ đúng từng tên đó, nên có 4 lớp bao riêng thay vì 1 lớp chung.
// ---------------------------------------------------------------------------

/// <summary>ServiceResultMobile.cs:8-12 — payload tên "Customers" (dùng cho ServiceApp_Get).</summary>
public class ServiceResultMobile<T>
{
    public string? Message { get; set; }
    public T? Customers { get; set; }
}

/// <summary>ServiceResultMobile.cs:38-42 — payload tên "CreateServiceApp".</summary>
public class ServiceResultMobileSVCreateServiceApp<T>
{
    public string? Message { get; set; }
    public T? CreateServiceApp { get; set; }
}

/// <summary>ServiceResultMobile.cs:26-30 — payload tên "ServiceReminders".</summary>
public class ServiceResultMobileSVReminders<T>
{
    public string? Message { get; set; }
    public T? ServiceReminders { get; set; }
}

/// <summary>ServiceResultMobile.cs:43-47 — payload tên "MembershipCards".</summary>
public class ServiceResultMobileMembershipCards<T>
{
    public string? Message { get; set; }
    public T? MembershipCards { get; set; }
}
