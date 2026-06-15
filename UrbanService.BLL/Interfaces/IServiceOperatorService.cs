using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UrbanService.BLL.DTOs;

namespace UrbanService.BLL.Interfaces
{
    public interface IServiceOperatorService
    {
        Task<List<ServiceOperatorResponse>>
            GetAllActiveAsync();

        Task<List<ServiceOperatorResponse>>
            GetByCategoryAsync(int categoryId);
    }
}
