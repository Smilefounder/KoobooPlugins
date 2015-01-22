using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kooboo.CMS.Plugins.Captcha
{
    public interface IResponseManager
    {
        void SetHeader(string name, string value);
    }
}
