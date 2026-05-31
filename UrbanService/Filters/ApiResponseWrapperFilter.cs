using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UrbanService.BLL.Common;

namespace TetGift.Filters
{
    public class ApiResponseWrapperFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            // Bỏ qua các loại result đặc biệt
            if (context.Result is FileResult || context.Result is ContentResult)
            {
                await next();
                return;
            }

            // ObjectResult: Ok(object), Created(...), BadRequest(object)...
            if (context.Result is ObjectResult obj)
            {
                if (obj.Value != null && obj.Value.GetType().IsGenericType &&
                    obj.Value.GetType().GetGenericTypeDefinition() == typeof(ApiResponse<>))
                {
                    await next();
                    return;
                }

                var status = obj.StatusCode ?? 200;

                if (status >= 200 && status < 300)
                {
                    context.Result = new ObjectResult(new ApiResponse<object?>
                    {
                        Status = status,
                        Msg = "OK",
                        Data = obj.Value
                    })
                    { StatusCode = status };
                }

                await next();
                return;
            }

            // StatusCodeResult: Ok(), NoContent(), NotFound()...
            if (context.Result is StatusCodeResult sc)
            {
                var status = sc.StatusCode;

                if (status >= 200 && status < 300)
                {
                    context.Result = new ObjectResult(new ApiResponse<object?>
                    {
                        Status = status,
                        Msg = status == 204 ? "No Content" : "OK",
                        Data = null
                    })
                    { StatusCode = status };
                }

                await next();
                return;
            }

            await next();
        }
    }
}
