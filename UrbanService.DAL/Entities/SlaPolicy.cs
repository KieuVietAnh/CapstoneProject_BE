using System;
using System.Collections.Generic;

namespace UrbanService.DAL.Entities;

public partial class SlaPolicy
{
    public int SlaPolicyId { get; set; }

    /// <summary>
    /// Tên chính sách để quản trị viên dễ nhận biết.
    /// Ví dụ: SLA sự cố giao thông nghiêm trọng.
    /// </summary>
    public string PolicyName { get; set; } = null!;

    /// <summary>
    /// Có thể null để áp dụng cho tất cả khu vực.
    /// </summary>
    public int? AreaId { get; set; }

    /// <summary>
    /// Có thể null để áp dụng cho tất cả category.
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Low, Medium, High hoặc Critical.
    /// </summary>
    public string Priority { get; set; } = null!;

    /// <summary>
    /// Thời gian phản hồi lần đầu, tính bằng phút.
    /// </summary>
    public int ResponseTimeMinutes { get; set; }

    /// <summary>
    /// Thời gian hoàn thành xử lý, tính bằng phút.
    /// </summary>
    public int ResolutionTimeMinutes { get; set; }

    /// <summary>
    /// Thời điểm chính sách bắt đầu có hiệu lực.
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Null nghĩa là chưa xác định thời điểm hết hiệu lực.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual OperatingArea? Area { get; set; }

    public virtual UrbanServiceCategory? Category { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual User? UpdatedByUser { get; set; }

    public virtual ICollection<FeedbackSla> FeedbackSlas { get; set; }
        = new List<FeedbackSla>();
}