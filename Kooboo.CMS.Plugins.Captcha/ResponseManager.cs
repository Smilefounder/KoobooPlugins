using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;

namespace Kooboo.CMS.Plugins.Captcha
{
    [Kooboo.CMS.Common.Runtime.Dependency.Dependency(typeof(IResponseManager))]
    public class ResponseManager : IResponseManager
    {
        public void SetHeader(string name, string value)
        {
            HttpContext.Current.Response.AddHeader(name, "GET");
        }
    }
}
