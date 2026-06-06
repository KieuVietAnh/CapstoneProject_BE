using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class Service
{
    public int ServiceId { get; set; }

    public int CategoryId { get; set; }

    public int OperatorId { get; set; }

    public string ServiceName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsSystemService { get; set; }

    public decimal? BasePrice { get; set; }

    public string Currency { get; set; } = null!;

    public string? ExternalServiceUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual UrbanServiceCategory Category { get; set; } = null!;

    public virtual ServiceOperator Operator { get; set; } = null!;

    public virtual ICollection<ServicePayment> ServicePayments { get; set; } = new List<ServicePayment>();
}
