using System;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Mvc;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.ComponentModel;
using Kooboo.CMS.Sites.Extension;
using Kooboo.CMS.Sites.View;
using Kooboo.CMS.Sites.Models;
using Kooboo.CMS.Sites.Globalization;
using System.Collections.Generic;

namespace Kooboo.CMS.Plugins.Captcha
{
    public partial class CaptchaPlugin
    {
        #region --- SET DEFAULT PARAMS ---
        int CaptchaType = 1; // 1 or 2
        int CaptchaLength = 4;
        string CharacterSet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; //for example "ACDEFGHJKLMNPQRSTUVWXY345679" or "AaBbCcDdEeFf"
        int imageWidth = 200;
        int imageHeight = 70;
        Color BackGroundColor = System.Drawing.ColorTranslator.FromHtml("#FFFFFF");  //Hex color
        Color FontColor = System.Drawing.ColorTranslator.FromHtml("#0000FF"); //Hex color
        double WarpFactor = 1.6; // used only in type 1
        string[] _fonts = { "Times New Roman", "Georgia", "Arial", "Comic Sans MS", "Courier" };
        bool UseBackGroundImage = false; // true or false
        string BackGroundImageBasePath = "/Cms_Data/Contents/SampleSite/Media/captcha"; // for example - "/Cms_Data/Contents/SampleSite/Media/captcha";
        string[] BackGroundImagePath = { "cp1.jpg" }; // for example -  { "cp1.jpg", "cp2.jpg", "cp3.png", "cp4.png", "cp5.png" };
        string ReloadLinkText = "Reload captcha";
        bool IncludeUserInfo = true; // true or false (IP, User agent)
        string MailSubject = "FeedBack from site " + Site.Current.Name;
        string[] FieldsForValidation = { }; // for example -  { "username", "email" };
        string ValidationMessage = "{0} not correct";
        string[] eMailForFeedBack = { }; // for example -  { "admin@domain.com", "office@company.com" }; 
        #endregion
    }


    [Description("Custom Captcha Validator Plugin For FeedBack v3.0 for KooBoo CMS 4 for send feedback-e-mail. More info - http://kooboocaptcha.codeplex.com/")]
    public partial class CaptchaPlugin : IPagePlugin
    {
        IResponseManager _responseManager;
        public CaptchaPlugin(IResponseManager responseManager)
        {
            _responseManager = responseManager;
        }

        #region IPagePlugin Members

        public ActionResult Execute(Page_Context pageViewContext, PagePositionContext positionContext)
        {
            var context = pageViewContext.ControllerContext.HttpContext;
            var controller = pageViewContext.ControllerContext.Controller;
            GetCustomParams(pageViewContext, positionContext);
            if (context.Request.QueryString["m"] == "cp")
            {
                try
                {
                    MemoryStream MemStream = new MemoryStream();
                    MemStream = GenerateCaptchaImage(pageViewContext);
                    MemStream.Position = 0;
                    return new FileStreamResult(MemStream, "image/png");
                }
                catch (Exception e)
                {
                    controller.ViewData["CaptchaFormResult"] = "Error. " + e.Message + " " + e.StackTrace;
                    controller.ViewData.ModelState.AddModelError("", e);
                    Kooboo.HealthMonitoring.Log.LogException(e);
                }
            }
            else
            {
                if (context.Request.HttpMethod.ToUpper() == "POST")
                {
                    bool blnValidateAntiForgeryToken = false;
                    try
                    {
                        System.Web.Helpers.AntiForgery.Validate();
                        blnValidateAntiForgeryToken = true;
                    }
                    catch
                    {
                        controller.ViewData["CaptchaFormResult"] = "Не верный идентификатор AntiForgeryToken".RawLabel("CaptchaFormResult", CaptchaKeys.LabelCategory);
                    };
                    // validate captcha
                    var strCaptchaInput = (string)context.Request.Form["captcha"];
                    var strCaptchaCode = (string)context.Session["captcha"];
                    context.Session["captcha"] = "";
                    // server validation
                    var blnFieldsValid = true;
                    if (FieldsForValidation.Length > 0)
                    {
                        foreach (var strField in FieldsForValidation)
                        {
                            if (String.IsNullOrEmpty(context.Request.Form[strField])) { blnFieldsValid = false; controller.ViewData[strField] = String.Format(ValidationMessage, strField); };
                        }
                    }

                    if (blnFieldsValid && (strCaptchaInput != null) && (strCaptchaCode != null) && (strCaptchaCode.ToUpper() == strCaptchaInput.ToUpper()) && blnValidateAntiForgeryToken)
                    {
                        controller.ViewData["CaptchaSuccess"] = true;
                        // do something if valid
                        ActionsIfCaptchaValid(pageViewContext, positionContext);
                    }
                    else
                    {
                        if ((strCaptchaInput != null) | (strCaptchaCode != null)) { controller.ViewData["CaptchaSuccess"] = false; } // captcha is not valid
                    }
                }

                controller.ViewData["CaptchaImage"] = MvcHtmlString.Create("<a id='captcha_image_link' href='#' onclick=\"javascript:document.getElementById('captcha_image').src = '?m=cp&' + Math.floor(Math.random() * 99999)+11111; return false;\" title='" + ReloadLinkText + "'><img src='?m=cp' id='captcha_image' border='0' alt='" + ReloadLinkText + "' /></a>");
                controller.ViewData["CaptchaImageWithoutLink"] = MvcHtmlString.Create("<img src='?m=cp' id='captcha_image' border='0' alt='' />");
                controller.ViewData["CaptchaReloadLink"] = MvcHtmlString.Create("<a id='captcha_link' href='#' onclick=\"javascript:document.getElementById('captcha_image').src = '?m=cp&' + Math.floor(Math.random() * 99999)+11111; return false;\">" + ReloadLinkText + "</a>");
            }
            return null;
        }

        string GetRandomText()
        {
            char[] letters = CharacterSet.ToCharArray();
            string text = string.Empty; Random r = new Random(); int num = -1;
            for (int i = 0; i < CaptchaLength; i++)
            {
                num = (int)(r.NextDouble() * (letters.Length)); text += letters[num].ToString();
            }
            return text;
        }
        GraphicsPath DeformPath(GraphicsPath graphicsPath)
        {
            double XAmp = WarpFactor * imageWidth / 100;
            double YAmp = WarpFactor * imageHeight / 85;
            double XFreq = 2 * Math.PI / imageWidth;
            double YFreq = 2 * Math.PI / imageHeight;

            var deformed = new PointF[graphicsPath.PathPoints.Length];
            var rng = new Random();
            double xSeed = rng.NextDouble() * 2 * Math.PI;
            double ySeed = rng.NextDouble() * 2 * Math.PI;
            for (int i = 0; i < graphicsPath.PathPoints.Length; i++)
            {
                PointF original = graphicsPath.PathPoints[i];
                double val = XFreq * original.X * YFreq * original.Y;
                var xOffset = (int)(XAmp * Math.Sin(val + xSeed));
                var yOffset = (int)(YAmp * Math.Sin(val + ySeed));
                deformed[i] = new PointF(original.X + xOffset, original.Y + yOffset);
            }
            return new GraphicsPath(deformed, graphicsPath.PathTypes);
        }

        MemoryStream GenerateCaptchaImage(Page_Context pageViewContext)
        {
            var curContext = pageViewContext.ControllerContext.HttpContext;
            curContext.Response.Cache.SetExpires(DateTime.UtcNow.AddYears(-4));
            curContext.Response.Cache.SetValidUntilExpires(false);
            curContext.Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            curContext.Response.Cache.SetRevalidation(System.Web.HttpCacheRevalidation.AllCaches);
            curContext.Response.Cache.SetNoStore();
            curContext.Response.ExpiresAbsolute = DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0));
            curContext.Response.Expires = 0;
            curContext.Response.CacheControl = "no-cache";
            curContext.Response.AppendHeader("Pragma", "no-cache");
            curContext.Response.ContentType = "image/png";
            string text = GetRandomText();
            curContext.Session[CaptchaKeys.SessionKey] = text;
            var rect = new Rectangle(0, 0, imageWidth, imageHeight);
            Bitmap bmp = new Bitmap(imageWidth, imageHeight);
            var Grph = Graphics.FromImage(bmp);
            Grph.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            Grph.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            Grph.FillRectangle(new SolidBrush(BackGroundColor), 0, 0, bmp.Width, bmp.Height);
            if (UseBackGroundImage)
            {
                Random rndBackGroundImage = new Random();
                string rndBackGroundImagePath = BackGroundImagePath[rndBackGroundImage.Next(BackGroundImagePath.Length)];
                var grp = Graphics.FromImage(bmp);
                Image background = Image.FromFile(curContext.Server.MapPath(BackGroundImageBasePath + "/" + rndBackGroundImagePath));
                grp.DrawImage(background, new Rectangle(0, 0, bmp.Width, bmp.Height));
            }
            Random rnd1 = new Random();
            string family = _fonts[rnd1.Next(_fonts.Length)].Trim();
            int size = (imageWidth * 2 / text.Length);
            var font = new Font(family, size);
            var meas = new SizeF(0, 0);
            while (size > 2 && (meas = Grph.MeasureString(text, font)).Width > imageWidth || meas.Height > imageHeight)
            {
                font.Dispose(); size -= 1; font = new Font(family, size);
            }
            if (CaptchaType == 1)
            {
                using (var fontFormat = new StringFormat())
                {
                    fontFormat.Alignment = StringAlignment.Center;
                    fontFormat.LineAlignment = StringAlignment.Center;
                    var path = new GraphicsPath();
                    path.AddString(text, font.FontFamily, (int)font.Style, font.Size, rect, fontFormat);
                    using (var solidBrush = new SolidBrush(FontColor))
                    {
                        Grph.FillPath(solidBrush, DeformPath(path));
                    }
                }
            }
            else
            {
                char[] textArray = text.ToCharArray();
                int yPosition = 0;
                Random r = new Random();
                int xPos = 10;
                for (int i = 0; i < textArray.Length; i++)
                {
                    var rRand = r.Next(5);
                    var FontFactor = (int)Math.Round(font.SizeInPoints / 2);
                    if (i % 2 == 0) { rRand = rRand * (-1); };
                    Grph.RotateTransform(rRand);

                    yPosition = (int)(r.NextDouble() * FontFactor / 2);
                    //yPosition = i;
                    Grph.DrawString(textArray[i].ToString(), font, new SolidBrush(FontColor), xPos, yPosition);
                    xPos += FontFactor + r.Next(5);
                }
            }
            font.Dispose();
            MemoryStream MemStream = new MemoryStream();
            bmp.Save(MemStream, System.Drawing.Imaging.ImageFormat.Png);
            return MemStream;
        }

        void ActionsIfCaptchaValid(Page_Context pageViewContext, PagePositionContext positionContext)
        {
            var context = pageViewContext.ControllerContext.HttpContext;
            var controller = pageViewContext.ControllerContext.Controller;
            try
            {
                controller.ViewData["CaptchaFormResult"] = "";
                controller.ViewData["CaptchaSuccess"] = true;
                // do action, written in form field "CaptchaFormAction".
                var strFormAction = context.Request.Form["CaptchaFormAction"]; // It must be SENDEMAIL
                if (strFormAction.ToUpper() == "SENDEMAIL")
                {
                    var strBody = "";
                    foreach (string key in context.Request.Form)
                    {
                        if (!(key == "__RequestVerificationToken" | key == "CaptchaFormAction" | key == "submit" | key == "captcha"))
                        {
                            if (!string.IsNullOrEmpty(context.Request.Form[key])) { strBody += Environment.NewLine + key + ": " + context.Request.Form[key] + Environment.NewLine; };
                        }
                    }
                    if (!string.IsNullOrEmpty(strBody))
                    {
                        if (IncludeUserInfo)
                        {
                            strBody += Environment.NewLine + "IP: " + context.Request.ServerVariables["REMOTE_ADDR"];
                            strBody += Environment.NewLine + "User agent: " + context.Request.ServerVariables["HTTP_USER_AGENT"];
                        };
                        controller.ViewData["CaptchaFormResult"] = SendEmail(Site.Current.Smtp.From, MailSubject, strBody, Site.Current.Smtp);
                    }
                    else
                    {
                        controller.ViewData["CaptchaFormResult"] = "Mail was not sent. Body is empty.";
                    }
                };
                // redirect to the provided url.
                if (!string.IsNullOrEmpty(context.Request.Form[CaptchaKeys.RedirectUrl]))
                {
                    context.Response.Redirect(context.Request.Form[CaptchaKeys.RedirectUrl], true);
                }
            }
            catch (Exception e)
            {
                controller.ViewData["CaptchaFormResult"] = "Error. " + e.Message + " " + e.StackTrace;
                controller.ViewData.ModelState.AddModelError("", e);
                Kooboo.HealthMonitoring.Log.LogException(e);
            }
        }

        private IHtmlString SendEmail(string from, string subject, string body, Smtp SmtpSettings)
        {
            IHtmlString strSendRezult = new HtmlString("");
            MailMessage message = new MailMessage() { From = new MailAddress(from) };
            if (eMailForFeedBack.Length > 0)
            {
                foreach (var strField in eMailForFeedBack)
                {
                    message.To.Add(strField);
                }
            }
            else
            {
                foreach (var item in SmtpSettings.To)
                {
                    if (!string.IsNullOrEmpty(item))
                    {
                        message.To.Add(item);
                    }
                }
            }
            message.Subject = subject;
            message.Body = body;
            SmtpClient smtpClient = new SmtpClient();
            smtpClient.Host = SmtpSettings.Host;
            smtpClient.Port = SmtpSettings.Port;
            smtpClient.EnableSsl = SmtpSettings.EnableSsl;
            if (!string.IsNullOrEmpty(SmtpSettings.UserName))
            {
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential(SmtpSettings.UserName, SmtpSettings.Password);
            }
            try
            {
                smtpClient.Send(message);
                strSendRezult = "Mail was sent".RawLabel("MailSentSuccess", CaptchaKeys.LabelCategory);
            }
            catch (Exception e)
            {
                strSendRezult = "Mail was not sent. Error sendig mail. ".RawLabel("MailSentFailed");// +new HtmlString(e.Message + e.StackTrace);
            };
            return strSendRezult;
        }

        void GetCustomParams(Page_Context pageViewContext, PagePositionContext positionContext)
        {
            try
            {
                ParamValues ResultParam = new ParamValues();

                ResultParam = ReadParamFromUser(positionContext, "Captcha_Type", "int");
                if (ResultParam.IsValidValue) CaptchaType = (int)ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_Length", "int");
                if (ResultParam.IsValidValue) CaptchaLength = (int)ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_CharacterSet", "string");
                if (ResultParam.IsValidValue) CharacterSet = ResultParam.Value.ToString();

                ResultParam = ReadParamFromUser(positionContext, "Captcha_ImageWidth", "int");
                if (ResultParam.IsValidValue) imageWidth = (int)ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_ImageHeight", "int");
                if (ResultParam.IsValidValue) imageHeight = (int)ResultParam.Value;

                ResultParam = ReadParamFromUser(positionContext, "Captcha_BackGroundColor", "color");
                if (ResultParam.IsValidValue) BackGroundColor = (Color)ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_FontColor", "color");
                if (ResultParam.IsValidValue) FontColor = (Color)ResultParam.Value;

                ResultParam = ReadParamFromUser(positionContext, "Captcha_WarpFactor", "double");
                if (ResultParam.IsValidValue) WarpFactor = (Double)ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_Fonts", "string_array");
                if (ResultParam.IsValidValue) _fonts = (string[])ResultParam.Value;

                ResultParam = ReadParamFromUser(positionContext, "Captcha_UseBackGroundImage", "bool");
                if (ResultParam.IsValidValue) UseBackGroundImage = (bool)ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_BackGroundImageBasePath", "string");
                if (ResultParam.IsValidValue) BackGroundImageBasePath = ResultParam.Value.ToString();
                ResultParam = ReadParamFromUser(positionContext, "Captcha_BackGroundImagePath", "string_array");
                if (ResultParam.IsValidValue) BackGroundImagePath = (string[])ResultParam.Value;

                ResultParam = ReadParamFromUser(positionContext, "Captcha_ReloadLinkText", "string");
                if (ResultParam.IsValidValue) ReloadLinkText = ResultParam.Value.ToString();
                ResultParam = ReadParamFromUser(positionContext, "Captcha_IncludeUserInfo", "bool");
                if (ResultParam.IsValidValue) IncludeUserInfo = (bool)ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_MailSubject", "string");
                if (ResultParam.IsValidValue) MailSubject = ResultParam.Value.ToString();
                ResultParam = ReadParamFromUser(positionContext, "Captcha_FieldsForValidation", "string_array");
                if (ResultParam.IsValidValue) FieldsForValidation = (string[])ResultParam.Value;
                ResultParam = ReadParamFromUser(positionContext, "Captcha_ValidationMessage", "string");
                if (ResultParam.IsValidValue) ValidationMessage = ResultParam.Value.ToString();
                ResultParam = ReadParamFromUser(positionContext, "Captcha_eMailForFeedBack", "string_array");
                if (ResultParam.IsValidValue) eMailForFeedBack = (string[])ResultParam.Value;
            }
            catch (Exception e)
            {
                pageViewContext.ControllerContext.Controller.ViewData["CaptchaFormResult"] = "Error. " + e.Message + " " + e.StackTrace;
            }

        }
        ParamValues ReadParamFromUser(PagePositionContext positionContext, string NameParam, string TypeParam)
        {
            ParamValues ResultParam = new ParamValues();
            ResultParam.IsValidValue = false;
            object objParam = null;
            try
            {
                if (positionContext.Parameters[NameParam] != null) { objParam = positionContext.Parameters[NameParam]; }
            }
            catch { }

            if (objParam == null)
            {
                try
                {
                    if (Site.Current.CustomFields[NameParam] != null) { objParam = Site.Current.CustomFields[NameParam]; }
                }
                catch { }
            }

            try
            {
                if (objParam != null)
                {
                    switch (TypeParam)
                    {
                        case "int":
                            int intOutParse;
                            if (Int32.TryParse(objParam.ToString(), out intOutParse)) { ResultParam.IsValidValue = true; ResultParam.Value = intOutParse; };
                            break;
                        case "string":
                            ResultParam.IsValidValue = true; ResultParam.Value = objParam.ToString();
                            break;
                        case "color":
                            try
                            {
                                Color TempColor = System.Drawing.ColorTranslator.FromHtml(objParam.ToString());
                                ResultParam.IsValidValue = true; ResultParam.Value = TempColor;
                            }
                            catch { }
                            break;
                        case "double":
                            Double dblOutParse;
                            if (Double.TryParse(objParam.ToString(), out dblOutParse)) { ResultParam.IsValidValue = true; ResultParam.Value = dblOutParse; };
                            break;
                        case "bool":
                            bool blnOutParse;
                            if (Boolean.TryParse(objParam.ToString(), out blnOutParse)) { ResultParam.IsValidValue = true; ResultParam.Value = blnOutParse; };
                            break;
                        case "string_array":
                            string[] objParamAr = objParam.ToString().Split(',');
                            ResultParam.IsValidValue = true; ResultParam.Value = objParamAr;
                            break;
                        default:
                            break;
                    }
                }
            }
            catch { }

            return ResultParam;
        }
        class ParamValues
        {
            public bool IsValidValue = false;
            public object Value;
        }

        public ActionResult HttpGet(Page_Context context, PagePositionContext positionContext)
        {
            _responseManager.SetHeader("CaptchaPlugin", "GET");
            return null;
        }

        public ActionResult HttpPost(Page_Context context, PagePositionContext positionContext)
        {
            _responseManager.SetHeader("CaptchaPlugin", "POST");
            return null;
        }
        #endregion

        public string GetOrSet(string key)
        {
            var defaultValue = "";
            var ddd = Site.Current.CustomFields;
            Site.Current.CustomFields["b"] = "dd";
            //Site.Current.CustomFields.Add("a", "a");
            Sites.Services.ServiceFactory.SiteManager.Update(Site.Current);
            //{
            //    var dummy = Site.Current.CustomFields[key];
            //}
            //var customFields = Site.Current.CustomFields;
            //if (null == customFields)
            //{
            //    Site.Current.CustomFields = new Dictionary<string, string>();
            //    Site.Current.CustomFields.TryGetValue(key, out defaultValue);
            //    Site.Current.CustomFields.Add(key, "aaa");
            //}
            return defaultValue;
        }
    }
}