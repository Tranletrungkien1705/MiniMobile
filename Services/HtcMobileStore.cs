// ============================================================================
// Kho dữ liệu trong bộ nhớ cho 3 màn đã port từ 2019.3.HTC.TV.MobileApp.
//
// CỐ Ý ĐƠN GIẢN HOÁ (khai báo rõ, không im lặng):
//   Bên nguồn, cả 3 API đều KHÔNG tự chạy SQL: chúng gọi web service ngoài
//     - Ser_App / Ser_RO  -> WSHTCSV.WSCarSv  (HTCMobileTVO.cs:486-487, :358-359, :635-638)
//     - Crd_Card          -> WSiNOSiCICHyundai.WS (HTCMobileTVO.cs:1183-1184)
//   Trong repo 2019.3.HTC.TV.MobileApp chỉ có PROXY tự sinh, không có SQL. Nhưng
//   ĐÃ TRUY ĐƯỢC phía thực thi của WSCarSv sang repo khác — đường trace đầy đủ:
//     HTCMobileTVO.cs  ->  2021.1.TCMotor/Dev/DMSCarSv/V20/HTCWSCarSvTab/WSCarSvTab.asmx.cs
//        :4697 HTCMobileTVO_GetServiceReminders -> :4742 _biz.*_20210603
//        :4756 HTCMobileTVO_GetSer_App          -> :4807 _biz.HTCMobileTVO_GetSer_App
//        :4822 HTCMobileTVO_Ser_App_Create      -> :4883 _biz.*_New20210423
//     -> 2021.1.TCMotor/Dev/DMSCarSv/V20/TERP.BizCarSv/BizCarSv.TVO.cs
//        :660 GetServiceReminders_20210603 · :888 GetSer_App · :1808 Ser_App_Create
//   ⇒ Ser_App và Ser_RO có SQL THẬT để đối chiếu (xem chú thích từng hàm dưới).
//   Riêng Crd_Card đi qua WS iNOS, SQL của WS đó KHÔNG tìm được ⇒ vẫn dựa vào bản
//   song sinh cục bộ, và điều đó được nói rõ tại hàm GetCrdCard.
//   MiniMobile không có EF/Database nên KHÔNG có seeder/ALTER để thêm.
// ============================================================================

using MiniMobile.Models;

namespace MiniMobile.Services;

public static class HtcMobileStore
{
    // -----------------------------------------------------------------------
    // Bảng Ser_App — phiếu hẹn dịch vụ. Bộ cột trả về: DMSSV_GetServiceApp.cs:11-28.
    //
    // AppStatus KHÔNG nằm trong DTO trả cho client, nhưng là cột LỌC sống còn của
    // câu SQL (BizCarSv.TVO.cs:1006 — "and ro.AppStatus in ('2')") nên phải giữ ở
    // tầng dữ liệu. Vì vậy mỗi dòng kho là cặp (DTO, AppStatus).
    // -----------------------------------------------------------------------
    private sealed class SerAppRow
    {
        public DMSSV_GetServiceApp Dto { get; init; } = new();
        /// <summary>'1' = Mới tạo, '2' = Xác nhận — BizCarSv.TVO.cs:959-964, :2408.</summary>
        public string AppStatus { get; set; } = "1";
    }

    private static readonly List<SerAppRow> _serApp = new()
    {
        new SerAppRow { AppStatus = "2", Dto = new DMSSV_GetServiceApp
        {
            CustomerCode = "KH000001", CustomerName = "Trần Lê Trung Kiên",
            CustomerMobile = "0912345678", CustomerIDCardNo = "001089012345",
            PlateNo = "30A-123.45", VIN = "RLMSM2ABCDE000101",
            TradeMarkCode = "HYUNDAI", ModelCode = "ACCENT", ModelName = "Accent 1.4 AT",
            AppDateTime = "2026-09-29 08:30:00", AppTime = "08:30",
            CusRequest = "Bảo dưỡng định kỳ 10.000 km",
            DealerCode = "HTC.HN01", AppCode = "APP0000001", AppNo = "HN01.2026.000001",
            CVDVName = "Nguyễn Văn Dũng",
            CreateDateTime = "2026-09-20 09:12:00", LogLUDTime = "2026-09-25 14:02:11"
        } },
        new SerAppRow { AppStatus = "2", Dto = new DMSSV_GetServiceApp
        {
            CustomerCode = "KH000002", CustomerName = "Phạm Thị Đào",
            CustomerMobile = "0987654321", CustomerIDCardNo = "001192033444",
            PlateNo = "29C-678.90", VIN = "RLMSM2ABCDE000102",
            TradeMarkCode = "HYUNDAI", ModelCode = "SANTAFE", ModelName = "SantaFe 2.2D",
            AppDateTime = "2026-09-30 13:00:00", AppTime = "13:00",
            CusRequest = "Kiểm tra điều hoà, thay dầu hộp số",
            DealerCode = "HTC.HN01", AppCode = "APP0000002", AppNo = "HN01.2026.000002",
            CVDVName = "Lê Thị Hoà",
            CreateDateTime = "2026-09-22 10:40:00", LogLUDTime = "2026-09-27 08:15:40"
        } },
        // Dòng này để AppStatus = '1' (Mới tạo) — CỐ Ý, để thấy rõ guard
        // "AppStatus in ('2')" (BizCarSv.TVO.cs:1006) loại nó khỏi kết quả tra cứu.
        new SerAppRow { AppStatus = "1", Dto = new DMSSV_GetServiceApp
        {
            CustomerCode = "KH000003", CustomerName = "Đỗ Minh Quân",
            CustomerMobile = "0903112233", CustomerIDCardNo = "079087011222",
            PlateNo = "51K-222.33", VIN = "RLMSM2ABCDE000103",
            TradeMarkCode = "HYUNDAI", ModelCode = "TUCSON", ModelName = "Tucson 2.0 AT",
            AppDateTime = "2026-10-02 09:00:00", AppTime = "09:00",
            CusRequest = "Sửa chữa chung — tiếng kêu gầm xe",
            DealerCode = "HTC.SG01", AppCode = "APP0000003", AppNo = "SG01.2026.000011",
            CVDVName = "Trần Quốc Bảo",
            CreateDateTime = "2026-09-28 16:05:00", LogLUDTime = "2026-09-28 16:05:00"
        } }
    };

    /// <summary>Số thứ tự sinh AppNo — xem chú thích ở CreateSerApp.</summary>
    private static int _seqAppNo = 100;

    /// <summary>
    /// Lọc + phân trang Ser_App. BẰNG CHỨNG SQL THẬT:
    /// 2021.1.TCMotor/Dev/DMSCarSv/V20/TERP.BizCarSv/BizCarSv.TVO.cs:948-1024.
    ///   - :1006  and ro.AppStatus in ('2')            ⇒ CHỈ phiếu đã XÁC NHẬN
    ///   - :1007  and ro.LogLUDateTime >= @strFromDate
    ///   - :1008  and ro.LogLUDateTime <= @strToDate
    ///   - :950   identity(bigint, 0, 1) MyIdxSeq      ⇒ thứ tự = thứ tự join, KHÔNG order by
    ///   - :1018-1019 phân trang MyIdxSeq trong [start, end] (cận trên ĐÓNG)
    ///   - :1023  MyCount đếm trên #tbl_Ser_App_Filter, tức SAU khi cắt trang
    ///            ⇒ ItemCount của màn này BẰNG số dòng trả về. Đây là chỗ khác hẳn
    ///            màn thẻ hội viên (đếm trước khi cắt) — trước khi tìm được SQL này
    ///            tôi đã port sai theo hướng "đếm trước", nay sửa lại.
    /// Lưu ý mốc ngày: tầng app chuẩn hoá bằng StdDTime (giữ giờ), tầng WS chuẩn hoá
    /// LẦN HAI bằng StandardizeDTime (BizCarSv.TVO.cs:933-934) — cùng dạng có giờ nên
    /// không đổi giá trị, khác với màn nhắc bảo dưỡng (bị cắt mất giờ).
    /// </summary>
    public static (long itemCount, List<DMSSV_GetServiceApp> items) GetSerApp(
        string? strFromDate, string? strToDate, long start, long end)
    {
        var draft = _serApp
            .Where(x => x.AppStatus == "2")                                  // :1006
            .Where(x => InRange(x.Dto.LogLUDTime, strFromDate, strToDate))   // :1007-1008
            .Select(x => x.Dto)
            .ToList();
        var items = TakeRange(draft, start, end);                            // :1018-1019
        long itemCount = items.Count;                                        // :1023
        return (itemCount, items);
    }

    /// <summary>
    /// Tạo phiếu hẹn. Nguồn đẩy nguyên 12 tham số sang WS HTCMobileTVO_Ser_App_Create
    /// (HTCMobileTVO.cs:645-665) rồi lấy AppNo từ Remark của MyDataSet
    /// (AppointmentServiceController.cs:95-96).
    ///
    /// HAI HÀNH VI THẬT lấy từ BizCarSv.TVO.cs (bản live *_New20210423, :1808):
    ///   - :2408  AppStatus = "1" (Mới tạo) ⇒ phiếu vừa tạo KHÔNG hiện trong
    ///            ServiceApp_Get cho tới khi được xác nhận thành '2'. Nếu port mà
    ///            cho hiện ngay là SAI nghiệp vụ (và build vẫn xanh).
    ///   - :2399  CreatedDate ghi theo "yyyy-MM-dd HH:mm" — KHÔNG có giây.
    /// CỐ Ý ĐƠN GIẢN HOÁ: AppNo thật do myUtil_GetCmSeqCode + bảng Ser_ROSequence sinh
    /// (:2318-2333) nên KHÔNG tái lập được ở đây; dùng dạng "&lt;DealerCode&gt;-&lt;yyMMdd&gt;&lt;3 số&gt;"
    /// đọc từ khối SQL BỊ COMMENT (:2070-2091) — là mô tả dạng duy nhất có trong repo.
    /// </summary>
    public static DMSSV_GetServiceApp CreateSerApp(CreateServiceAppInput input)
    {
        var dealer = (input.strDealerCode ?? "").Trim();
        var seq = Interlocked.Increment(ref _seqAppNo);
        var now = DateTime.Now;
        var row = new DMSSV_GetServiceApp
        {
            // Nguồn KHÔNG nhận CustomerCode/VIN/ModelName từ input (CreateServiceAppInput.cs:12-23)
            // ⇒ ba cột này do phía WS tự tra ra; ở đích để null, không bịa.
            CustomerCode = null,
            CustomerName = input.strCustomerName,
            CustomerMobile = input.strCustomerMobile,
            CustomerIDCardNo = input.strCustomerIDCardNo,
            PlateNo = input.strPlateNo,
            VIN = null,
            TradeMarkCode = input.strTradeMarkCode,
            ModelCode = input.strModelCode,
            ModelName = null,
            AppDateTime = input.strAppDateTime,
            AppTime = input.strAppTime,
            CusRequest = input.strCusRequest,
            DealerCode = input.strDealerCode,
            AppCode = $"APP{seq:D7}",
            AppNo = $"{dealer}-{now:yyMMdd}{seq % 1000:D3}",
            // Cột trả về là CVDVName (TÊN) và nguồn lấy từ Sys_User.UserName qua
            // join "ro.CVDVCode = suser.UserCode" (BizCarSv.TVO.cs:981, :997-998).
            // Ở đích không có bảng Sys_User ⇒ giữ nguyên MÃ, KHÔNG bịa ra tên.
            CVDVName = input.strCVDVCode,
            // BizCarSv.TVO.cs:2399 — CreatedDate không có giây.
            CreateDateTime = now.ToString("yyyy-MM-dd HH:mm"),
            LogLUDTime = now.ToString("yyyy-MM-dd HH:mm:ss")
        };
        // AppStatus = "1" (Mới tạo) — BizCarSv.TVO.cs:2408.
        lock (_serApp) _serApp.Add(new SerAppRow { AppStatus = "1", Dto = row });
        return row;
    }

    // -----------------------------------------------------------------------
    // Bảng Ser_RO — nhắc bảo dưỡng. Bộ cột: DMSSV_GetServiceReminders.cs:11-24.
    // -----------------------------------------------------------------------
    private static readonly List<DMSSV_GetServiceReminders> _serRo = new()
    {
        new DMSSV_GetServiceReminders
        {
            ROID = "RO0000001", RONo = "HN01.RO.2026.000451",
            CustomerCode = "KH000001", CustomerName = "Trần Lê Trung Kiên",
            CustomerMobile = "0912345678", CustomerIDCardNo = "001089012345",
            CarID = "CAR0000001", PlateNo = "30A-123.45", VIN = "RLMSM2ABCDE000101",
            TradeMarkCode = "HYUNDAI", ModelCode = "ACCENT", ModelName = "Accent 1.4 AT",
            ReminderMaintanceDate = "2026-09-25 00:00:00", DealerCode = "HTC.HN01"
        },
        new DMSSV_GetServiceReminders
        {
            ROID = "RO0000002", RONo = "HN01.RO.2026.000452",
            CustomerCode = "KH000002", CustomerName = "Phạm Thị Đào",
            CustomerMobile = "0987654321", CustomerIDCardNo = "001192033444",
            CarID = "CAR0000002", PlateNo = "29C-678.90", VIN = "RLMSM2ABCDE000102",
            TradeMarkCode = "HYUNDAI", ModelCode = "SANTAFE", ModelName = "SantaFe 2.2D",
            ReminderMaintanceDate = "2026-09-29 00:00:00", DealerCode = "HTC.HN01"
        },
        new DMSSV_GetServiceReminders
        {
            ROID = "RO0000003", RONo = "SG01.RO.2026.001120",
            CustomerCode = "KH000003", CustomerName = "Đỗ Minh Quân",
            CustomerMobile = "0903112233", CustomerIDCardNo = "079087011222",
            CarID = "CAR0000003", PlateNo = "51K-222.33", VIN = "RLMSM2ABCDE000103",
            TradeMarkCode = "HYUNDAI", ModelCode = "TUCSON", ModelName = "Tucson 2.0 AT",
            ReminderMaintanceDate = "2026-10-15 00:00:00", DealerCode = "HTC.SG01"
        }
    };

    /// <summary>
    /// Nhắc bảo dưỡng. BẰNG CHỨNG SQL THẬT:
    /// 2021.1.TCMotor/Dev/DMSCarSv/V20/TERP.BizCarSv/BizCarSv.TVO.cs:705-793.
    ///
    /// Điểm QUAN TRỌNG NHẤT, và là chỗ tôi đã suy luận SAI trước khi tìm được SQL:
    ///   :705-706  strFromDate = StandardizeDate(strDate);
    ///             strToDate   = StandardizeDate(strDate);   ⇒ CẢ HAI mốc lấy từ
    ///             CÙNG một tham số strDate, và StandardizeDate CẮT MẤT GIỜ
    ///             (TERP.Utils/Utils.cs:241 trả "yyyy-MM-dd").
    ///   :732-733  and ro.ReminderMaintanceDate >= @strFromDate
    ///             and ro.ReminderMaintanceDate <= @strToDate
    ///   ⇒ đây KHÔNG phải "đến hạn tính tới ngày X" mà là khớp ĐÚNG mốc
    ///     "&lt;ngày X&gt; 00:00:00". Bản ghi có giờ khác 00:00:00 trong chính ngày đó
    ///     cũng bị loại — hành vi thật của nguồn, port nguyên, không "sửa cho đúng".
    ///
    /// Bước thứ hai không thể suy ra từ tham số (:738-778): chỉ giữ RO MỚI NHẤT của
    /// mỗi cặp (PlateNo, DealerCode) — Max(ROID) tính trên TOÀN bảng Ser_RO, không
    /// giới hạn trong khoảng ngày — rồi giao lại với tập nháp.
    ///   :734-735  order by bị COMMENT ⇒ KHÔNG sắp xếp.
    ///   :791-792  phân trang MyIdxSeq trong [start, end].
    /// </summary>
    public static List<DMSSV_GetServiceReminders> GetSerRo(string? strDate, long start, long end)
    {
        // Tầng WS chuẩn hoá LẦN HAI, cắt mất giờ — BizCarSv.TVO.cs:705-706.
        var strFromDate = HtcMobileGuards.StandardizeDate(strDate);
        var strToDate = HtcMobileGuards.StandardizeDate(strDate);

        // #tbl_Ser_RO_Draft_01 — :726-736.
        var draft01 = _serRo
            .Where(x => InRange(x.ReminderMaintanceDate, strFromDate, strToDate))
            .ToList();

        // #tbl_PlateNo_DealerCode (:739-749) + #tblSer_RO_Max (:752-768):
        // với mỗi cặp (PlateNo, DealerCode) xuất hiện trong tập nháp, lấy Max(ROID)
        // trên TOÀN bảng Ser_RO.
        var maxRoIds = draft01
            .Select(x => (x.PlateNo, x.DealerCode))
            .Distinct()
            .Select(k => _serRo
                .Where(r => r.PlateNo == k.PlateNo && r.DealerCode == k.DealerCode)
                .Max(r => r.ROID))
            .ToHashSet();

        // #tbl_Ser_RO_Draft_02 = giao của nháp với tập Max(ROID) — :771-778.
        var draft02 = draft01.Where(x => maxRoIds.Contains(x.ROID)).ToList();

        return TakeRange(draft02, start, end);   // :791-792
    }

    // -----------------------------------------------------------------------
    // Bảng Crd_Card (+ Crd_Member) — thẻ hội viên. Bộ cột:
    // Membership_GetMembershipCards.cs:12-52 (42 cột đang bật).
    // -----------------------------------------------------------------------
    private static readonly List<Membership_GetMembershipCards> _crdCard = new()
    {
        new Membership_GetMembershipCards
        {
            MemberNo = "MB0000001", CardNo = "HYU0000000001",
            CardType = "S", CardTypeName = "Thẻ Bạc", CardSourceCode = "DEALER",
            PointsInThePeriod = 1250, TotalAmountMember = 685_000_000,
            TotalSpentAmountMember = 42_500_000, Balance = 1250,
            CustomerCode = "KH000001", CustomerName = "Trần Lê Trung Kiên",
            CustomerIDCardNo = "001089012345", CustomerGender = "M",
            DateOfBirth = "1989-05-17", CustomerMobile = "0912345678",
            CustomerAddress = "Số 9 Đào Duy Anh, Đống Đa",
            ProvinceCode = "01", ProvinceName = "Thành phố Hà Nội",
            DistrictCode = "006", DistrictName = "Quận Đống Đa",
            PlateNo = "30A-123.45", VIN = "RLMSM2ABCDE000101",
            TradeMarkCode = "HYUNDAI", ModelCode = "ACCENT", ModelName = "Accent 1.4 AT",
            EffDateStart = "2024-06-01", EffDateEnd = "2027-05-31",
            CardStatus = "A", MemberStatus = "A", DealerCode = "HTC.HN01",
            CreateDateTime = "2024-06-01 09:00:00", LogLUDateTime = "2026-09-26 11:20:00",
            DeletionDateTime = null, Remark = "Nâng hạng từ thẻ Thường",
            DeletionStatus = "0", RegistrationDate = "2024-06-01",
            PointPurchaseAddCar = 800, PointBonus = 200, QtyVisitAvail = 4
        },
        new Membership_GetMembershipCards
        {
            MemberNo = "MB0000002", CardNo = "HYU0000000002",
            CardType = "G", CardTypeName = "Thẻ Vàng", CardSourceCode = "HTC",
            PointsInThePeriod = 4300, TotalAmountMember = 1_420_000_000,
            TotalSpentAmountMember = 96_800_000, Balance = 3980,
            CustomerCode = "KH000002", CustomerName = "Phạm Thị Đào",
            CustomerIDCardNo = "001192033444", CustomerGender = "F",
            DateOfBirth = "1992-11-03", CustomerMobile = "0987654321",
            CustomerAddress = "Số 120 Trần Duy Hưng, Cầu Giấy",
            ProvinceCode = "01", ProvinceName = "Thành phố Hà Nội",
            DistrictCode = "005", DistrictName = "Quận Cầu Giấy",
            PlateNo = "29C-678.90", VIN = "RLMSM2ABCDE000102",
            TradeMarkCode = "HYUNDAI", ModelCode = "SANTAFE", ModelName = "SantaFe 2.2D",
            EffDateStart = "2023-02-15", EffDateEnd = "2026-02-14",
            CardStatus = "A", MemberStatus = "A", DealerCode = "HTC.HN01",
            CreateDateTime = "2023-02-15 14:30:00", LogLUDateTime = "2026-09-27 09:05:00",
            DeletionDateTime = null, Remark = null,
            DeletionStatus = "0", RegistrationDate = "2023-02-15",
            PointPurchaseAddCar = 2600, PointBonus = 700, QtyVisitAvail = 9
        },
        new Membership_GetMembershipCards
        {
            MemberNo = "MB0000003", CardNo = "HYU0000000003",
            CardType = "N", CardTypeName = "Thẻ Thường", CardSourceCode = "DEALER",
            PointsInThePeriod = 150, TotalAmountMember = 799_000_000,
            TotalSpentAmountMember = 3_200_000, Balance = 150,
            CustomerCode = "KH000003", CustomerName = "Đỗ Minh Quân",
            CustomerIDCardNo = "079087011222", CustomerGender = "M",
            DateOfBirth = "1987-01-29", CustomerMobile = "0903112233",
            CustomerAddress = "Số 45 Điện Biên Phủ, Bình Thạnh",
            ProvinceCode = "79", ProvinceName = "Thành phố Hồ Chí Minh",
            DistrictCode = "765", DistrictName = "Quận Bình Thạnh",
            PlateNo = "51K-222.33", VIN = "RLMSM2ABCDE000103",
            TradeMarkCode = "HYUNDAI", ModelCode = "TUCSON", ModelName = "Tucson 2.0 AT",
            EffDateStart = "2026-08-20", EffDateEnd = "2029-08-19",
            CardStatus = "A", MemberStatus = "A", DealerCode = "HTC.SG01",
            CreateDateTime = "2026-08-20 10:15:00", LogLUDateTime = "2026-08-20 10:15:00",
            DeletionDateTime = null, Remark = null,
            DeletionStatus = "0", RegistrationDate = "2026-08-20",
            PointPurchaseAddCar = 150, PointBonus = 0, QtyVisitAvail = 0
        }
    };

    /// <summary>
    /// Thẻ hội viên. ĐÂY là màn duy nhất trong ba màn có BẰNG CHỨNG SQL trong repo:
    /// bản song sinh cục bộ HTCMobileTVO.cs:925-941 lọc
    ///   (cc.LogLUDTime trong khoảng) HOẶC (cm.LogLUDTime trong khoảng)
    /// rồi "order by cc.CardNo desc", phân trang bằng MyIdxSeq (:953-954).
    /// LƯU Ý bẫy sinh đôi: controller LIVE gọi bản _New20190310
    /// (HTCReportController.cs:335) — bản đó đẩy sang WS iNOS nên KHÔNG chứa SQL.
    /// Điều kiện lọc ở đây lấy từ bản cục bộ; ở đích chỉ có một cột LogLUDateTime
    /// (DTO gộp thẻ + hội viên thành một dòng) nên phép "OR hai bảng" thu về một cột.
    /// </summary>
    public static (long itemCount, List<Membership_GetMembershipCards> items) GetCrdCard(
        string? strFromDate, string? strToDate, long start, long end)
    {
        var draft = _crdCard
            .Where(x => InRange(x.LogLUDateTime, strFromDate, strToDate))
            .OrderByDescending(x => x.CardNo, StringComparer.Ordinal)   // HTCMobileTVO.cs:939-940
            .ToList();
        long itemCount = draft.Count;                                   // HTCMobileTVO.cs:944
        var items = TakeRange(draft, start, end);
        return (itemCount, items);
    }

    // -----------------------------------------------------------------------
    // Trợ giúp chung
    // -----------------------------------------------------------------------

    /// <summary>
    /// So mốc ngày trong khoảng ĐÓNG [from, to], mô phỏng đúng cách SQL Server so
    /// cột datetime với tham số chuỗi: chuỗi chỉ có ngày ("yyyy-MM-dd") được ngầm
    /// hiểu là "&lt;ngày&gt; 00:00:00". Bẫy đã tránh: nếu so thẳng chuỗi thì
    /// "2026-09-29 00:00:00" &lt;= "2026-09-29" cho ra FALSE (chuỗi dài hơn) — ngược
    /// hẳn với SQL, và sẽ làm màn nhắc bảo dưỡng trả về 0 dòng một cách CÂM.
    /// </summary>
    private static bool InRange(string? value, string? from, string? to)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var v = Pad(value);
        if (!string.IsNullOrEmpty(from) && string.CompareOrdinal(v, Pad(from)) < 0) return false;
        if (!string.IsNullOrEmpty(to) && string.CompareOrdinal(v, Pad(to)) > 0) return false;
        return true;
    }

    /// <summary>Bù " 00:00:00" cho mốc chỉ có ngày — đúng phép ngầm chuyển của SQL Server.</summary>
    private static string Pad(string s) => s.Length == 10 ? s + " 00:00:00" : s;

    /// <summary>
    /// Cắt trang y như nguồn: MyIdxSeq do "identity(bigint, 0, 1)" sinh nên bắt đầu
    /// từ 0 (HTCMobileTVO.cs:929), điều kiện là "&gt;= start AND &lt;= end" — cận trên
    /// ĐÓNG (HTCMobileTVO.cs:953-954). Sai chỗ này là lệch đúng 1 bản ghi.
    /// </summary>
    private static List<T> TakeRange<T>(List<T> draft, long start, long end)
    {
        var result = new List<T>();
        for (long i = 0; i < draft.Count; i++)
            if (i >= start && i <= end) result.Add(draft[(int)i]);
        return result;
    }
}
