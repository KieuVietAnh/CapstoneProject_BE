using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UrbanService.BLL.Common
{
    public class PayOSConfig
    {
        public string ClientId { get; set; } = null!;

        public string ApiKey { get; set; } = null!;

        public string ChecksumKey { get; set; } = null!;

        public string ReturnUrl { get; set; } = null!;

        public string CancelUrl { get; set; } = null!;
    }
}
