using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kooboo.CMS.Sites.Extension.UI.Tabs;
using Kooboo.CMS.Sites.Extension.UI;
using System.Web.Routing;
using Kooboo.Globalization;
using Kooboo.CMS.Sites.Globalization;
using Kooboo.CMS.Common.Runtime.Dependency;

namespace Kooboo.CMS.Plugins.Captcha.UI
{
    [Dependency(typeof(ITabProvider), Key = "CaptchaTab")]
    public class CaptchaTab : ITabProvider
    {
        public MvcRoute[] ApplyTo
        {
            get
            {
                return new[]{
                    new MvcRoute(){
                        Area="Sites",
                        Controller="Site",
                        Action="Settings"
                    }
                };
            }
        }

        public IEnumerable<TabInfo> GetTabs(RequestContext requestContext)
        {
            return new[]{
               new TabInfo(){
                   Name="captcha",
                   DisplayText="Captcha".RawLabel("CaptchaTabName",CaptchaKeys.LabelCategory).ToString(),
                   VirtualPath="Captcha"
               },
               new TabInfo(){
                   Name="captcha1",
                   DisplayText="abcdef",
                   VirtualPath="Captcha"
               }
           };
        }
    }
}
