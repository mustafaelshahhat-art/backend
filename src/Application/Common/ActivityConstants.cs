using System.Collections.Generic;

namespace Application.Common;

public static class ActivityConstants
{
    // Activity Codes (Internal strict enums)
    public const string USER_REGISTERED = "USER_REGISTERED";
    public const string USER_LOGIN = "USER_LOGIN";
    public const string MATCH_STARTED = "MATCH_STARTED";
    public const string MATCH_ENDED = "MATCH_ENDED";
    public const string MATCH_SCORE_UPDATED = "MATCH_SCORE_UPDATED";
    public const string MATCH_EVENT_ADDED = "MATCH_EVENT_ADDED";
    public const string MATCH_EVENT_REMOVED = "MATCH_EVENT_REMOVED";
    public const string MATCH_POSTPONED = "MATCH_POSTPONED";
    public const string MATCH_RESCHEDULED = "MATCH_RESCHEDULED";
    public const string MATCH_CANCELLED = "MATCH_CANCELLED";
    public const string GUEST_VISIT = "GUEST_VISIT";

    public const string TOURNAMENT_CREATED = "TOURNAMENT_CREATED";
    public const string TOURNAMENT_GENERATED = "TOURNAMENT_GENERATED";
    public const string TEAM_CREATED = "TEAM_CREATED";
    public const string TEAM_JOINED = "TEAM_JOINED";
    public const string TEAM_REMOVED = "TEAM_REMOVED"; // Player removed
    public const string TEAM_DISABLED = "TEAM_DISABLED";
    public const string TEAM_ACTIVATED = "TEAM_ACTIVATED";
    public const string TEAM_DEACTIVATED = "TEAM_DEACTIVATED";
    public const string TOURNAMENT_FINALIZED = "TOURNAMENT_FINALIZED";
    public const string TOURNAMENT_REGISTRATION_CLOSED = "TOURNAMENT_REGISTRATION_CLOSED";
    public const string TOURNAMENT_DELETED = "TOURNAMENT_DELETED";
    public const string REGISTRATION_APPROVED = "REGISTRATION_APPROVED";
    public const string TEAM_ELIMINATED = "TEAM_ELIMINATED";
    public const string ADMIN_OVERRIDE = "ADMIN_OVERRIDE";

    public const string ADMIN_CREATED = "ADMIN_CREATED";
    public const string PASSWORD_CHANGED = "PASSWORD_CHANGED";
    public const string AVATAR_UPDATED = "AVATAR_UPDATED";
    
    public const string PAYMENT_SUBMITTED = "PAYMENT_SUBMITTED";
    public const string PAYMENT_APPROVED = "PAYMENT_APPROVED";

    // Centralized Arabic Dictionary
    // Key: Activity Code
    // Value: (CategoryAr, ActionTitleAr, MessageTemplateAr)
    public static readonly Dictionary<string, (string CategoryAr, string TitleAr, string MessageTemplate)> Library = new()
    {
        { USER_REGISTERED, ("مستخدم", "تسجيل جديد", "تم تسجيل مستخدم جديد باسم {userName}") },
        { USER_LOGIN, ("مستخدم", "تسجيل دخول", "قام المستخدم {userName} بتسجيل الدخول") },
        { GUEST_VISIT, ("زائر", "زيارة ضيف", "دخل زائر جديد إلى المنصة كضيف") },
        { ADMIN_CREATED, ("إدارة", "إنشاء مشرف", "تم إنشاء حساب مشرف جديد باسم {adminName}") },
        { PASSWORD_CHANGED, ("مستخدم", "تغيير كلمة المرور", "قام المستخدم بتغيير كلمة المرور الخاصة به") },
        { AVATAR_UPDATED, ("مستخدم", "تحديث صورة", "قام المستخدم بتحديث الصورة الشخصية") },
        
        { MATCH_STARTED, ("مباراة", "بدء مباراة", "تم بدء المباراة {matchInfo}") }, // matchInfo = Home vs Away
        { MATCH_ENDED, ("مباراة", "انتهاء مباراة", "انتهت المباراة {matchInfo} بنتيجة {score}") },
        { MATCH_SCORE_UPDATED, ("مباراة", "تحديث النتيجة", "تم تحديث نتيجة المباراة {matchInfo} إلى {score}") },
        { MATCH_EVENT_ADDED, ("مباراة", "إضافة حدث", "تم إضافة {eventType} للاعب {playerName} في مباراة {matchInfo}") },
        { MATCH_EVENT_REMOVED, ("مباراة", "إلغاء حدث", "تم إلغاء حدث من مباراة {matchInfo}") },
        { MATCH_POSTPONED, ("مباراة", "تأجيل مباراة", "تم تأجيل مباراة {matchInfo}") },
        { MATCH_RESCHEDULED, ("مباراة", "إعادة جدولة", "تم تحديد موعد جديد للمباراة {matchInfo} بتاريخ {newDate}") },
        { MATCH_CANCELLED, ("مباراة", "إلغاء مباراة", "تم إلغاء المباراة {matchInfo}") },


        { TOURNAMENT_CREATED, ("بطولة", "إنشاء بطولة", "تم إنشاء بطولة جديدة: {tournamentName}") },
        { TOURNAMENT_GENERATED, ("بطولة", "توليد مباريات", "تم توليد جدول المباريات لبطولة {tournamentName}") },
        { TOURNAMENT_FINALIZED, ("بطولة", "انتهاء بطولة", "انتهت بطولة {tournamentName} بفوز فريق {winnerName}") },
        { TOURNAMENT_REGISTRATION_CLOSED, ("بطولة", "إغلاق التسجيل", "تم إغلاق باب التسجيل في بطولة {tournamentName}") },
        { TOURNAMENT_DELETED, ("بطولة", "حذف بطولة", "تم حذف بطولة {tournamentName}") },
        { REGISTRATION_APPROVED, ("بطولة", "قبول فريق", "تم قبول مشاركة فريق {teamName} في بطولة {tournamentName}") },
        { TEAM_ELIMINATED, ("بطولة", "إقصاء فريق", "تم إقصاء فريق {teamName} من بطولة {tournamentName}") },

        { TEAM_CREATED, ("فريق", "إنشاء فريق", "تم إنشاء فريق جديد: {teamName}") },
        { TEAM_JOINED, ("فريق", "انضمام لاعب", "انضم اللاعب {playerName} إلى فريق {teamName}") },
        { TEAM_REMOVED, ("فريق", "مغادرة لاعب", "غادر اللاعب {playerName} فريق {teamName}") },
        { TEAM_DISABLED, ("إدارة", "تعطيل فريق", "تم تعطيل فريق {teamName} من قبل الإدارة") },
        { TEAM_ACTIVATED, ("إدارة", "تنشيط فريق", "تم تنشيط فريق {teamName}") },
        { TEAM_DEACTIVATED, ("إدارة", "إلغاء تنشيط", "تم إلغاء تنشيط فريق {teamName}") },




        { PAYMENT_SUBMITTED, ("دفع", "إيصال دفع", "تم رفع إيصال دفع من قبل فريق {teamName}") },
        { PAYMENT_APPROVED, ("دفع", "قبول دفع", "تم اعتماد دفع الرسوم لفرقة {teamName}") },
        { ADMIN_OVERRIDE, ("إدارة", "تدخل إداري", "تم تنفيذ إجراء طارئ: {action} في {tournamentName}. التفاصيل: {details}") }
    };

    public static (string Category, string Title, string Message) GetLocalized(string? code, Dictionary<string, string>? placeholders)
    {
        if (string.IsNullOrEmpty(code) || !Library.ContainsKey(code))
             return ("نظام", "إجراء نظام", "تم تنفيذ إجراء غير محدد في النظام");

        var template = Library[code];
        var message = template.MessageTemplate;

        if (placeholders != null)
        {
            foreach (var kvp in placeholders)
            {
                message = message.Replace($"{{{kvp.Key}}}", kvp.Value ?? "");
            }
        }

        return (template.CategoryAr, template.TitleAr, message);
    }
}
