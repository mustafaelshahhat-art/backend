using System;

namespace Application.Common;

public static class EmailTemplateHelper
{
    private const string PrimaryColor = "#10b981";
    private const string DarkBg = "#080c14";

    public static string CreateOtpTemplate(string title, string userName, string message, string otp, string expiry)
    {
        return $@"
<!DOCTYPE html>
<html lang=""ar"" dir=""rtl"">
<head>
    <meta charset=""UTF-8"">
    <style>
        body {{ font-family: 'Tahoma', 'Arial', sans-serif; background-color: #f3f4f6; margin: 0; padding: 20px; }}
        .wrapper {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); border: 1px solid #e5e7eb; }}
        .header {{ background-color: {DarkBg}; padding: 40px 20px; text-align: center; }}
        .header h1 {{ color: {PrimaryColor}; margin: 0; font-size: 28px; letter-spacing: 2px; text-transform: uppercase; }}
        .body {{ padding: 40px; text-align: center; color: #1f2937; }}
        .body h2 {{ color: #111827; margin-bottom: 24px; font-size: 22px; }}
        .body p {{ margin: 12px 0; line-height: 1.6; color: #4b5563; font-size: 16px; }}
        .otp-container {{ margin: 35px 0; padding: 25px; background: #f9fafb; border: 2px dashed {PrimaryColor}; border-radius: 12px; display: inline-block; }}
        .otp-code {{ font-size: 36px; font-weight: 800; color: {PrimaryColor}; letter-spacing: 12px; font-family: 'Courier New', monospace; }}
        .footer {{ padding: 30px; background-color: #f9fafb; border-top: 1px solid #e5e7eb; text-align: center; }}
        .footer p {{ font-size: 14px; color: #9ca3af; margin: 0; }}
        .brand {{ color: {PrimaryColor}; font-weight: bold; }}
        .ignore-text {{ font-size: 13px; color: #9ca3af; margin-top: 25px !important; border-top: 1px solid #f3f4f6; padding-top: 20px; }}
    </style>
</head>
<body>
    <div class=""wrapper"">
        <div class=""header"">
            <h1>RAMADAN GANA</h1>
        </div>
        <div class=""body"">
            <h2>{title}</h2>
            <p>مرحباً <strong>{userName}</strong>،</p>
            <p>{message}</p>
            <div class=""otp-container"">
                <span class=""otp-code"">{otp}</span>
            </div>
            <p>هذا الرمز صالح لمدة <strong>{expiry}</strong> فقط.</p>
            <p class=""ignore-text"">إذا لم تكن قد طلبت هذا الرمز، يرجى تجاهل هذا البريد الإلكتروني.</p>
        </div>
        <div class=""footer"">
            <p>© {DateTime.Now.Year} <span class=""brand"">RAMADAN GANA</span>. جميع الحقوق محفوظة.</p>
        </div>
    </div>
</body>
</html>";
    }
}
