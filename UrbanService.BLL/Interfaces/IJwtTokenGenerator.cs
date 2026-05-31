using UrbanService.DAL.Entities;

namespace UrbanService.BLL.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string Generate(User acc);
    }
}
