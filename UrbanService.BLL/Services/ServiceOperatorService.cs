using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UrbanService.BLL.DTOs;
using UrbanService.BLL.Interfaces;
using UrbanService.DAL.Entities;
using UrbanService.DAL.Interfaces;

namespace UrbanService.BLL.Services
{
    public class ServiceOperatorService : IServiceOperatorService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ServiceOperatorService(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<List<ServiceOperatorResponse>>
            GetAllActiveAsync()
        {
            var repo =
                _unitOfWork.GetRepository<ServiceOperator>();

            var operators =
                await repo.GetAllAsync(
                    x => x.IsActive,
                    q => q.Include(x => x.Category));

            return operators
                .Select(x => new ServiceOperatorResponse
                {
                    OperatorId = x.OperatorId,
                    OperatorName = x.OperatorName,
                    ContactEmail = x.ContactEmail,
                    ContactPhone = x.ContactPhone,
                    Address = x.Address,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category.CategoryName,
                    IsActive = x.IsActive
                })
                .ToList();
        }

        public async Task<List<ServiceOperatorResponse>>
            GetByCategoryAsync(int categoryId)
        {
            var repo =
                _unitOfWork.GetRepository<ServiceOperator>();

            var operators =
                await repo.GetAllAsync(
                    x => x.IsActive &&
                         x.CategoryId == categoryId,
                    q => q.Include(x => x.Category));

            return operators
                .Select(x => new ServiceOperatorResponse
                {
                    OperatorId = x.OperatorId,
                    OperatorName = x.OperatorName,
                    ContactEmail = x.ContactEmail,
                    ContactPhone = x.ContactPhone,
                    Address = x.Address,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category.CategoryName,
                    IsActive = x.IsActive
                })
                .ToList();
        }
    }
}