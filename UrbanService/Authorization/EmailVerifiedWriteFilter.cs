using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using UrbanService.BLL.Common;
using UrbanService.BLL.Common.Constraint;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.Authorization;

/// <summary>
/// Chặn thao tác ghi của tài khoản chưa xác thực email.
///
/// Quyền đọc được giữ nguyên: người dùng chưa xác thực vẫn xem được sự vụ, thông
/// báo và dữ liệu công khai, chỉ không tạo hay sửa được gì cho tới khi xác thực.
/// Ràng buộc đặt ở đây thay vì rải trong từng service để không có endpoint ghi nào
/// lọt lưới chỉ vì người viết quên kiểm tra.
/// </summary>
public sealed class EmailVerifiedWriteFilter : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> ReadOnlyMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get,
        HttpMethods.Head,
        HttpMethods.Options,
        HttpMethods.Trace
    };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (ReadOnlyMethods.Contains(context.HttpContext.Request.Method))
        {
            return;
        }

        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null ||
            endpoint?.Metadata.GetMetadata<AllowUnverifiedEmailAttribute>() != null)
        {
            return;
        }

        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        /*
         * Chỉ áp cho tài khoản người dân. Tài khoản nội bộ do admin tạo và cờ
         * is_verified của chúng mặc định là false trong database, nên áp cho mọi
         * role sẽ khóa sạch thao tác ghi của staff, manager và admin ngay lúc
         * deploy. Xác thực email vốn sinh ra để truy nguyên trách nhiệm người gửi
         * phản ánh, không phải để kiểm soát nhân sự nội bộ.
         */
        if (!string.Equals(
                user.FindFirstValue(ClaimTypes.Role),
                UserRole.SERVICEUSER,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        /*
         * Claim trong token là đường nhanh cho trường hợp phổ biến. Token phát
         * trước lúc xác thực vẫn mang email_verified = false dù tài khoản đã xác
         * thực xong, nên khi claim không nói "true" thì phải hỏi lại database,
         * không thì người vừa nhập OTP sẽ bị chặn cho tới khi token hết hạn.
         */
        if (string.Equals(
                user.FindFirstValue(UserClaimTypes.EmailVerified),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var parsedUserId))
        {
            return;
        }

        var uow = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var isVerified = await uow.GetRepository<User>().Entities
            .AsNoTracking()
            .Where(candidate => candidate.UserId == parsedUserId)
            .Select(candidate => (bool?)candidate.IsVerified)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (isVerified == true)
        {
            return;
        }

        context.Result = new ObjectResult(new ApiResponse<object?>
        {
            Status = StatusCodes.Status403Forbidden,
            Msg = "Bạn cần xác thực email trước khi thực hiện thao tác này.",
            Data = new { code = AuthResultCode.EmailNotVerified }
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
