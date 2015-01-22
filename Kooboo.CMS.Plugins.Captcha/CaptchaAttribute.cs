using Kooboo.CMS.Sites.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace Kooboo.CMS.Plugins.Captcha
{

    public class CaptchaAttribute : ActionFilterAttribute, IPagePlugin
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            //base.OnActionExecuting(filterContext);

        }


        public ActionResult Execute(Sites.View.Page_Context pageContext, Sites.View.PagePositionContext positionContext)
        {
            pageContext.ViewDataContext.ViewData["abc"] = DateTime.Now;

            return null;
        }
    }
}
