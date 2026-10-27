namespace EnglishCenter.Api.Services;

// Các tiêu chí chấm IELTS, cách tính band tổng và mô tả ngắn cho từng mức band.
// Mô tả là bản tóm tắt nội bộ để giáo viên tham khảo nhanh, không phải bản gốc của IELTS.
public static class IeltsBands
{
    public static readonly string[] WritingCriteria = { "TR", "CC", "LR", "GRA" };
    public static readonly string[] SpeakingCriteria = { "FC", "LR", "GRA", "P" };

    public static string[] CriteriaOf(string skill) =>
        skill == "Speaking" ? SpeakingCriteria : WritingCriteria;

    public static readonly Dictionary<string, string> CriterionNames = new()
    {
        ["TR"] = "Task Response / Task Achievement",
        ["CC"] = "Coherence and Cohesion",
        ["FC"] = "Fluency and Coherence",
        ["LR"] = "Lexical Resource",
        ["GRA"] = "Grammatical Range and Accuracy",
        ["P"] = "Pronunciation"
    };

    // Điểm hợp lệ: 0 - 9, bước 0.5
    public static bool IsValid(decimal band) => band >= 0 && band <= 9 && band * 2 == Math.Floor(band * 2);

    // Band tổng = trung bình 4 tiêu chí, làm tròn theo cách của IELTS:
    // .25 làm tròn lên .5, .75 làm tròn lên số nguyên kế tiếp (6.25 -> 6.5; 6.75 -> 7.0; 6.125 -> 6.0)
    public static decimal Overall(IEnumerable<decimal> scores)
    {
        var list = scores.ToList();
        if (list.Count == 0) return 0;
        var avg = list.Average();
        return Math.Floor(avg * 2 + 0.5m) / 2;
    }

    // Mô tả theo band nguyên (band lẻ .5 dùng mô tả của band nguyên bên dưới)
    private static readonly Dictionary<string, Dictionary<int, string>> Descriptors = new()
    {
        ["TR"] = new()
        {
            [9] = "Trả lời đầy đủ mọi phần của đề, lập luận phát triển trọn vẹn, ý rất rõ ràng.",
            [8] = "Trả lời đầy đủ các phần của đề, ý được mở rộng tốt, rất ít chỗ thiếu sót.",
            [7] = "Bao quát các yêu cầu của đề, quan điểm rõ ràng xuyên suốt, ý chính được mở rộng nhưng đôi khi khái quát quá.",
            [6] = "Trả lời các phần của đề nhưng có phần chưa đều; quan điểm có nhưng kết luận có thể chưa rõ; một số ý chưa phát triển đủ.",
            [5] = "Chỉ trả lời một phần đề; quan điểm chưa rõ; ý chính hạn chế, thiếu ví dụ hoặc lạc đề một phần.",
            [4] = "Trả lời tối thiểu hoặc lệch đề; ý chính khó nhận ra, lặp lại hoặc không liên quan.",
            [3] = "Không trả lời được yêu cầu của đề; rất ít ý và hầu như không phát triển.",
            [2] = "Hầu như không liên quan đến đề.",
            [1] = "Bài không liên quan đến đề.",
            [0] = "Không làm bài hoặc bài học thuộc lòng."
        },
        ["CC"] = new()
        {
            [9] = "Liên kết mạch lạc tự nhiên, phân đoạn hợp lý, từ nối dùng tinh tế không gây chú ý.",
            [8] = "Sắp xếp ý logic, liên kết tốt, phân đoạn hợp lý.",
            [7] = "Bố cục rõ ràng, có tiến trình; dùng nhiều phương tiện liên kết, đôi chỗ dùng chưa chuẩn hoặc quá nhiều.",
            [6] = "Bố cục chung mạch lạc nhưng liên kết giữa các câu đôi khi máy móc hoặc sai; phân đoạn chưa luôn hợp lý.",
            [5] = "Có sắp xếp nhưng thiếu tiến trình rõ; từ nối hạn chế, dùng sai hoặc lặp; phân đoạn chưa hợp lý.",
            [4] = "Ý sắp xếp rời rạc; từ nối cơ bản, dùng sai nhiều; có thể không phân đoạn.",
            [3] = "Ý không được sắp xếp; liên kết rất hạn chế.",
            [2] = "Hầu như không kiểm soát được bố cục.",
            [1] = "Không truyền tải được thông điệp.",
            [0] = "Không làm bài."
        },
        ["FC"] = new()
        {
            [9] = "Nói trôi chảy, hiếm khi ngập ngừng; ý mạch lạc, phát triển đầy đủ và phù hợp.",
            [8] = "Nói trôi chảy, chỉ thỉnh thoảng lặp hoặc tự sửa; ngập ngừng chủ yếu để tìm ý.",
            [7] = "Nói dài không mấy khó khăn; đôi khi ngập ngừng vì tìm từ; dùng từ nối linh hoạt.",
            [6] = "Sẵn sàng nói dài nhưng đôi khi mất mạch do lặp, tự sửa hoặc ngập ngừng; từ nối chưa luôn phù hợp.",
            [5] = "Duy trì được lời nói nhưng hay lặp, tự sửa, nói chậm; lạm dụng một số từ nối.",
            [4] = "Không trả lời liên tục được, ngắt quãng nhiều; câu đơn giản liên kết kém.",
            [3] = "Ngập ngừng rất lâu; khó truyền đạt ý đơn giản.",
            [2] = "Ngắt quãng dài trước hầu hết các từ.",
            [1] = "Không giao tiếp được.",
            [0] = "Không tham gia."
        },
        ["LR"] = new()
        {
            [9] = "Từ vựng phong phú, chính xác, tự nhiên; dùng thành ngữ linh hoạt.",
            [8] = "Vốn từ rộng, dùng linh hoạt và chính xác; hiếm khi sai collocation hoặc chính tả.",
            [7] = "Đủ từ vựng để diễn đạt linh hoạt; có dùng từ ít phổ biến; đôi khi sai cách kết hợp từ.",
            [6] = "Vốn từ đủ cho yêu cầu; có cố gắng dùng từ khó nhưng còn sai; lỗi chính tả / cấu tạo từ chưa cản trở hiểu.",
            [5] = "Vốn từ hạn chế nhưng tạm đủ; lỗi chính tả / cấu tạo từ đáng kể có thể gây khó hiểu.",
            [4] = "Chỉ dùng từ cơ bản, lặp lại; lỗi từ vựng nhiều gây khó hiểu.",
            [3] = "Vốn từ rất hạn chế, kiểm soát kém.",
            [2] = "Chỉ dùng được vài từ rời rạc.",
            [1] = "Hầu như không có từ vựng phù hợp.",
            [0] = "Không làm bài."
        },
        ["GRA"] = new()
        {
            [9] = "Dùng đa dạng cấu trúc, chính xác gần như tuyệt đối.",
            [8] = "Đa dạng cấu trúc; phần lớn câu không lỗi, chỉ lỗi nhỏ hiếm gặp.",
            [7] = "Dùng nhiều câu phức; nhiều câu không lỗi; kiểm soát ngữ pháp tốt dù còn vài lỗi.",
            [6] = "Kết hợp câu đơn và câu phức; có lỗi nhưng hiếm khi làm khó hiểu.",
            [5] = "Cấu trúc hạn chế; cố dùng câu phức nhưng kém chính xác hơn câu đơn; lỗi thường xuyên.",
            [4] = "Rất ít cấu trúc; câu phức hiếm; lỗi chiếm ưu thế.",
            [3] = "Cố dùng câu nhưng lỗi ngữ pháp dày đặc làm sai nghĩa.",
            [2] = "Hầu như không tạo được câu.",
            [1] = "Không dùng được cấu trúc câu.",
            [0] = "Không làm bài."
        },
        ["P"] = new()
        {
            [9] = "Phát âm chính xác, tự nhiên, dễ hiểu hoàn toàn; ngữ điệu linh hoạt.",
            [8] = "Phát âm rõ ràng, dễ hiểu; giọng địa phương ảnh hưởng rất ít.",
            [7] = "Dễ hiểu xuyên suốt; dùng tốt trọng âm, ngữ điệu; đôi khi lỗi âm riêng lẻ.",
            [6] = "Nhìn chung dễ hiểu dù đôi chỗ phát âm sai một số âm; ngữ điệu kiểm soát chưa đều.",
            [5] = "Có thể hiểu được nhưng lỗi phát âm thường xuyên làm người nghe phải cố gắng.",
            [4] = "Lỗi phát âm nhiều, thường gây khó hiểu.",
            [3] = "Phần lớn lời nói khó hiểu.",
            [2] = "Lời nói thường không hiểu được.",
            [1] = "Không hiểu được.",
            [0] = "Không tham gia."
        }
    };

    public static string? Describe(string criterion, decimal band)
    {
        if (!Descriptors.TryGetValue(criterion, out var map)) return null;
        var key = (int)Math.Floor(Math.Clamp(band, 0, 9));
        return map.TryGetValue(key, out var text) ? text : null;
    }

    public static object DescriptorTable(string skill) =>
        CriteriaOf(skill).Select(c => new
        {
            criterion = c,
            name = CriterionNames[c],
            bands = Descriptors[c].OrderByDescending(x => x.Key)
                .Select(x => new { band = x.Key, description = x.Value })
        });
}
