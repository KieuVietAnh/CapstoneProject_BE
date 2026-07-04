using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs
{
    public class SubmitResolutionRequest
    {
        public Guid FeedbackId { get; set; }

        public Guid StaffUserId { get; set; }

        public int? ProviderReportId { get; set; }

        public string ResolutionSummary { get; set; } = string.Empty;

        public string ActionTaken { get; set; } = string.Empty;

        public string? ResultNote { get; set; }

        public List<string> ImageUrls { get; set; } = [];
    }
}
