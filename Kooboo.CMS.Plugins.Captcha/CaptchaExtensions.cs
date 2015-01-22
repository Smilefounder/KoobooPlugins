using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Kooboo.CMS.Sites.Globalization;
using System.Web.Routing;
using Kooboo.CMS.Plugins.Captcha;

namespace Kooboo.CMS.Sites.View
{
    [Captcha]
    public static class CaptchaExtensions
    {
        public static MvcHtmlString Captcha(this HtmlHelper htmlhelper, string name, object linkAttributes = null, object textboxAttributes = null, object imageAttributes = null)
        {
            RouteValueDictionary htmlAttrs = null;

            var ReloadLinkText = "Click to refresh".RawLabel("RefreshText", "Captcha").ToString();
            var input = new TagBuilder("input");
            var link = new TagBuilder("a");
            var img = new TagBuilder("img");

            input.MergeAttribute("name", name);
            input.MergeAttribute("id", name);
            input.MergeAttribute("type", "text");

            img.MergeAttribute("src", "?m=cp", true);
            if (null != imageAttributes)
            {
                htmlAttrs = new RouteValueDictionary(imageAttributes);
                img.MergeAttributes(htmlAttrs);
            }

            link.MergeAttribute("href", "javascript:;");
            link.MergeAttribute("onclick", "javascript:this.firstChild.src='?m=cp&' + Math.floor(Math.random() * 99999)+11111; return false;");

            link.InnerHtml = img.ToString();



            return MvcHtmlString.Create(input.ToString() + link.ToString());
            //return MvcHtmlString.Create("<a id='captcha_image_link' href='javascript:;' onclick=\"javascript:document.getElementById('captcha_image').src='?m=cp&' + Math.floor(Math.random() * 99999)+11111; return false;\" title='" + ReloadLinkText + "'><img src='?m=cp' id='captcha_image' border='0' alt='" + ReloadLinkText + "' /></a>");
        }
    }
}
