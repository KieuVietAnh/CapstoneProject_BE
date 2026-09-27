using System.Text.RegularExpressions;

namespace UrbanService.BLL.Common.Helpers;

/// <summary>
/// Chuẩn hoá số điện thoại về E.164, mặc định mã quốc gia Việt Nam.
///
/// Firebase chỉ nhận số ở dạng E.164, và số lưu trong database phải cùng dạng thì
/// mới so khớp được với số nằm trong Firebase ID token. Frontend dùng đúng quy tắc
/// này ở `packages/shared-api/src/phone.js`.
/// </summary>
public static partial class PhoneNumberHelper
{
    private const string DefaultCountryCode = "84";

    /// <summary>
    /// "0901 234 567", "84901234567" hay "+84901234567" đều ra "+84901234567".
    /// Trả về null khi số không hợp lệ.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var trimmed = input.Trim();
        if (!AllowedCharsRegex().IsMatch(trimmed))
        {
            return null;
        }

        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        string e164;
        if (trimmed.StartsWith('+'))
        {
            e164 = "+" + digits;
        }
        else if (digits.StartsWith("00"))
        {
            e164 = "+" + digits[2..];
        }
        else if (digits.StartsWith('0'))
        {
            e164 = "+" + DefaultCountryCode + digits[1..];
        }
        else if (digits.StartsWith(DefaultCountryCode) && digits.Length >= 11)
        {
            e164 = "+" + digits;
        }
        else
        {
            e164 = "+" + DefaultCountryCode + digits;
        }

        return E164Regex().IsMatch(e164) ? e164 : null;
    }

    /// <summary>Chuẩn hoá và ném lỗi khi số không hợp lệ.</summary>
    public static string NormalizeRequired(string? input)
    {
        return Normalize(input)
            ?? throw new Exception("Số điện thoại không hợp lệ. Ví dụ: 0901 234 567.");
    }

    /// <summary>"+84901234567" thành "0901 234 567" để hiển thị; số nước ngoài giữ nguyên.</summary>
    public static string ToDisplay(string e164)
    {
        if (!e164.StartsWith("+" + DefaultCountryCode, StringComparison.Ordinal))
        {
            return e164;
        }

        var local = "0" + e164[(DefaultCountryCode.Length + 1)..];
        return LocalGroupRegex().Replace(local, "$1 $2 $3");
    }

    [GeneratedRegex(@"^\+?[0-9 ().-]+$")]
    private static partial Regex AllowedCharsRegex();

    [GeneratedRegex(@"^\+[1-9][0-9]{7,14}$")]
    private static partial Regex E164Regex();

    [GeneratedRegex(@"^(\d{4})(\d{3})(\d+)$")]
    private static partial Regex LocalGroupRegex();
}
