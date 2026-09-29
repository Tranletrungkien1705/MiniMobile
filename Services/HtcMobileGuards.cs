// ============================================================================
// Port 1:1 các GUARD (chốt kiểm) của dự án nguồn 2019.3.HTC.TV.MobileApp.
// Đây là phần dễ bị bỏ quên nhất khi port: nguồn để guard rải ở 3 tầng khác nhau
// (controller → biz → utils), build vẫn xanh nếu thiếu.
// ============================================================================

namespace MiniMobile.Services;

/// <summary>Mã lỗi hằng của nguồn — ERP.HTCMobile.Constants/Error.DMS.Chain.cs:19.</summary>
public static class HtcMobileErr
{
    public const string GatewayAuthenticateFailed = "ErrERP.HTCMobile.CmSys_GatewayAuthenticateFailed";
    public const string DateTimeOutOfRange = "MyBiz.DateTimeOutOfRange";
}

/// <summary>Ngoại lệ nghiệp vụ mang mã lỗi — thay cho CmUtils.CMyException.Raise của nguồn.</summary>
public class HtcMobileException : Exception
{
    public string ErrorCode { get; }
    /// <summary>Cặp "tên, giá trị" nguồn nhồi vào alParamsCoupleError rồi in ra Message.</summary>
    public List<string> ParamsCouple { get; }

    public HtcMobileException(string errorCode, params string[] paramsCouple) : base(errorCode)
    {
        ErrorCode = errorCode;
        ParamsCouple = paramsCouple.ToList();
    }
}

public static class HtcMobileGuards
{
    /// <summary>
    /// Trần số bản ghi khi client gửi Count = 0.
    /// Nguồn: ERP.HTCMobile.Constants/Const.Main.BE.cs:54 — RecordCount_Max = "12345600".
    /// </summary>
    public const string RecordCountMax = "12345600";

    /// <summary>
    /// Mốc cuối mặc định khi ToDate rỗng.
    /// Nguồn: ERP.HTCMobile.Biz/HTCMobileTVO.cs:496 (GetServiceApp) và :1170 (GetMembershipCards_New20190310).
    /// </summary>
    public const string ToDateDefault = "2100-01-01 23:59:59";

    private static readonly DateTime MinAllowed = new(1900, 1, 1, 0, 0, 0);
    private static readonly DateTime MaxAllowed = new(2100, 1, 1, 23, 59, 59);

    /// <summary>
    /// Port 1:1 TUtils.CUtils.StdDTime — ERP.HTCMobile.Utils/Utils.cs:106-113.
    /// Ba hành vi phải giữ đúng, thiếu một cái là lệch nghiệp vụ:
    ///   1) rỗng/null  -> trả về null (KHÔNG phải chuỗi rỗng) — Utils.cs:108;
    ///   2) ngoài khoảng [1900-01-01 00:00:00 .. 2100-01-01 23:59:59] -> NÉM lỗi — Utils.cs:110-111;
    ///   3) trả về định dạng "yyyy-MM-dd HH:mm:ss" — Utils.cs:112.
    /// </summary>
    public static string? StdDTime(object? objDTime)
    {
        if (objDTime is null || objDTime.ToString()!.Length == 0) return null;
        if (!DateTime.TryParse(objDTime.ToString(), out var dtime))
            throw new HtcMobileException(HtcMobileErr.DateTimeOutOfRange, "objDTime", objDTime.ToString() ?? "");
        if (dtime < MinAllowed || dtime > MaxAllowed)
            throw new HtcMobileException(HtcMobileErr.DateTimeOutOfRange, "objDTime", objDTime.ToString() ?? "");
        return dtime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Port 1:1 TUtils.CUtils.StandardizeDate của TẦNG WS (khác StdDTime của tầng app!).
    /// Nguồn: 2021.1.TCMotor/Dev/DMSCarSv/V20/TERP.Utils/Utils.cs:235-242.
    /// Khác StdDTime ba điểm: trả về "yyyy-MM-dd" (CẮT giờ), chặn trên là
    /// "2100-01-01" (không có giờ), và thông điệp lỗi là "DateTime out of MyBizRange".
    /// Dùng ở màn nhắc bảo dưỡng, nơi WS chuẩn hoá lại mốc ngày lần thứ hai.
    /// </summary>
    public static string? StandardizeDate(object? strDate)
    {
        if (strDate is null || strDate.ToString()!.Length == 0) return null;
        if (!DateTime.TryParse(strDate.ToString(), out var dtime))
            throw new HtcMobileException("DateTime out of MyBizRange", "strDate", strDate.ToString() ?? "");
        if (dtime < MinAllowed || dtime > new DateTime(2100, 1, 1, 0, 0, 0))
            throw new HtcMobileException("DateTime out of MyBizRange", "strDate", strDate.ToString() ?? "");
        return dtime.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Port 1:1 TUtils.CConnectionManager.CheckGatewayAuthentication_OS — ERP.HTCMobile.Utils/Utils.cs:1763-1788.
    /// Lệch tinh vi PHẢI giữ: tên đăng nhập so KHÔNG phân biệt hoa/thường
    /// (StringEqualIgnoreCase, Utils.cs:1771) còn mật khẩu so CÓ phân biệt hoa/thường
    /// (StringEqual, Utils.cs:1772). Ném đúng mã CmSys_GatewayAuthenticateFailed.
    /// Giá trị đối chiếu lấy từ ERP.HTCMobile.Web/Web.config:42-43
    /// (Biz_GwUserCode_OutSite / Biz_GwPassword_OutSite = "HTC.TV.MobileApp.API"),
    /// cho phép ghi đè bằng biến môi trường để không nhúng cứng bí mật.
    /// </summary>
    public static void CheckGatewayAuthentication_OS(string? strGwUserCode, string? strGwPassword)
    {
        var expectUser = Environment.GetEnvironmentVariable("BIZ_GW_USERCODE_OUTSITE") ?? "HTC.TV.MobileApp.API";
        var expectPass = Environment.GetEnvironmentVariable("BIZ_GW_PASSWORD_OUTSITE") ?? "HTC.TV.MobileApp.API";

        var okUser = string.Equals(strGwUserCode ?? "", expectUser, StringComparison.OrdinalIgnoreCase);
        var okPass = string.Equals(strGwPassword ?? "", expectPass, StringComparison.Ordinal);
        if (!okUser || !okPass)
        {
            // Nguồn nhồi 2 cặp tham số này vào lỗi (Utils.cs:1776-1781) — giữ để dò log.
            throw new HtcMobileException(
                HtcMobileErr.GatewayAuthenticateFailed,
                "Check.strGwUserCode", strGwUserCode ?? "",
                "Check.strGwPassword", strGwPassword ?? "");
        }
    }

    /// <summary>
    /// Port 1:1 khối "if (nCount == 0) strCount = RecordCount_Max"
    /// — HTCMobileTVO.cs:493-494 (GetServiceApp), :1172-1173 (GetMembershipCards_New20190310),
    ///   :366-367 (GetServiceReminders).
    /// </summary>
    public static long RefineCount(long nCount)
        => nCount == 0 ? Convert.ToInt64(RecordCountMax) : nCount;

    /// <summary>
    /// Port 1:1 cặp mốc phân trang — HTCMobileTVO.cs:1176-1177:
    ///   nFilterRecordStart = 0; nFilterRecordEnd = start + count - 1.
    /// Trả về (start, end) để endpoint cắt trang y như nguồn (0-based, cận trên ĐÓNG).
    /// </summary>
    public static (long start, long end) FilterRecordRange(long nCount)
    {
        long start = 0;
        return (start, start + nCount - 1);
    }

    /// <summary>
    /// Port 1:1 ApiControllerBase.AddParamServiceException — ERP.HTCMobile.Web/Controllers/ApiControllerBase.cs:702-743.
    /// Nguồn KHÔNG trả HTTP 4xx/5xx: nó luôn trả 200 và nhồi chi tiết lỗi vào chuỗi
    /// "Message" (thân đáp ứng vẫn giữ payload rỗng). Đường thành công đặt Message = "0"
    /// (ApiControllerBase.cs:42). Vì client mobile cũ đọc theo quy ước này nên port
    /// phải giữ y nguyên, KHÔNG được "sửa cho đúng REST".
    /// </summary>
    public static string BuildErrorMessage(Exception ex)
    {
        const string sep = "<br/>--------------------------------------------------------<br/>";
        var errorCode = ex is HtcMobileException he ? he.ErrorCode : ex.Message;
        var sb = new System.Text.StringBuilder();
        sb.Append($"Error Code: {errorCode}");
        sb.Append(sep);

        var arrObj = ex is HtcMobileException he2 ? he2.ParamsCouple : new List<string>();
        // Nguồn chỉ in khi có > 1 phần tử (ApiControllerBase.cs:723) — giữ đúng điều kiện.
        if (arrObj.Count > 1)
        {
            for (int i = 0; i < arrObj.Count; i++)
            {
                var val = System.Net.WebUtility.HtmlEncode(arrObj[i] ?? "");
                sb.Append(val.Replace("\n", "<br/>"));
                sb.Append(i % 2 == 0 ? " = " : sep);
            }
        }
        return sb.ToString();
    }
}
