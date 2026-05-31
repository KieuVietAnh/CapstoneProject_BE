namespace UrbanService.BLL.Common
{
    public class ApiResponse<T>
    {
        public int Status { get; set; }
        public string Msg { get; set; } = "";
        public T? Data { get; set; }
    }
}
