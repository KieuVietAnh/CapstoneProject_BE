using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.DTOs
{
    public class SubmitResolutionRequest
    {
        public int? ProviderAssignmentId { get; set; }

        public string ResolutionSummary { get; set; } = string.Empty;

        public string ActionTaken { get; set; } = string.Empty;

        public string? ResultNote { get; set; }

        public List<string> ImageUrls { get; set; } = [];
    }
}
