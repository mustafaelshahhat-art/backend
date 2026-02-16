using System.Collections.Generic;
using Domain.Enums;

namespace Application.Common;

public static class ActivityConstants
{
    // ── Activity Codes ──
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
    public const string TEAM_REMOVED = "TEAM_REMOVED";
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
    public const string GROUPS_FINISHED = "GROUPS_FINISHED";
    public const string KNOCKOUT_STARTED = "KNOCKOUT_STARTED";

    // ── Centralized Library: Code → (CategoryAr, TitleAr, MessageTemplate, Severity, EntityType) ──
    public static readonly Dictionary<string, (string CategoryAr, string TitleAr, string MessageTemplate, ActivitySeverity Severity, string? EntityType)> Library = new()
    {
        // User
        { USER_REGISTERED,  ("مستخدم", "تسجيل جديد",       "تم تسجيل مستخدم جديد باسم {userName}",                  ActivitySeverity.Info,     "User") },
        { USER_LOGIN,       ("مستخدم", "تسجيل دخول",       "قام المستخدم {userName} بتسجيل الدخول",                  ActivitySeverity.Info,     "User") },
        { GUEST_VISIT,      ("زائر",   "زيارة ضيف",        "دخل زائر جديد إلى المنصة كضيف",                         ActivitySeverity.Info,     null) },
        { ADMIN_CREATED,    ("إدارة",  "إنشاء مشرف",       "تم إنشاء حساب مشرف جديد باسم {adminName}",              ActivitySeverity.Warning,  "User") },
        { PASSWORD_CHANGED, ("مستخدم", "تغيير كلمة المرور", "قام المستخدم بتغيير كلمة المرور الخاصة به",              ActivitySeverity.Info,     "User") },
        { AVATAR_UPDATED,   ("مستخدم", "تحديث صورة",       "قام المستخدم بتحديث الصورة الشخصية",                     ActivitySeverity.Info,     "User") },

        // Match
        { MATCH_STARTED,       ("مباراة", "بدء مباراة",    "تم بدء المباراة {matchInfo}",                                             ActivitySeverity.Info,     "Match") },
        { MATCH_ENDED,         ("مباراة", "انتهاء مباراة",  "انتهت المباراة {matchInfo} بنتيجة {score}",                                ActivitySeverity.Info,     "Match") },
        { MATCH_SCORE_UPDATED, ("مباراة", "تحديث النتيجة",  "تم تحديث نتيجة المباراة {matchInfo} إلى {score}",                         ActivitySeverity.Warning,  "Match") },
        { MATCH_EVENT_ADDED,   ("مباراة", "إضافة حدث",     "تم إضافة {eventType} للاعب {playerName} في مباراة {matchInfo}",             ActivitySeverity.Info,     "Match") },
        { MATCH_EVENT_REMOVED, ("مباراة", "إلغاء حدث",     "تم إلغاء حدث من مباراة {matchInfo}",                                      ActivitySeverity.Warning,  "Match") },
        { MATCH_POSTPONED,     ("مباراة", "تأجيل مباراة",   "تم تأجيل مباراة {matchInfo}",                                             ActivitySeverity.Warning,  "Match") },
        { MATCH_RESCHEDULED,   ("مباراة", "إعادة جدولة",   "تم تحديد موعد جديد للمباراة {matchInfo} بتاريخ {newDate}",                  ActivitySeverity.Info,     "Match") },
        { MATCH_CANCELLED,     ("مباراة", "إلغاء مباراة",   "تم إلغاء المباراة {matchInfo}",                                           ActivitySeverity.Critical, "Match") },

        // Tournament
        { TOURNAMENT_CREATED,             ("بطولة", "إنشاء بطولة",   "تم إنشاء بطولة جديدة: {tournamentName}",                                    ActivitySeverity.Info,     "Tournament") },
        { TOURNAMENT_GENERATED,           ("بطولة", "توليد مباريات", "تم توليد جدول المباريات لبطولة {tournamentName}",                              ActivitySeverity.Info,     "Tournament") },
        { TOURNAMENT_FINALIZED,           ("بطولة", "انتهاء بطولة",  "انتهت بطولة {tournamentName} بفوز فريق {winnerName}",                          ActivitySeverity.Info,     "Tournament") },
        { TOURNAMENT_REGISTRATION_CLOSED, ("بطولة", "إغلاق التسجيل", "تم إغلاق باب التسجيل في بطولة {tournamentName}",                              ActivitySeverity.Info,     "Tournament") },
        { TOURNAMENT_DELETED,             ("بطولة", "حذف بطولة",     "تم حذف بطولة {tournamentName}",                                               ActivitySeverity.Critical, "Tournament") },
        { REGISTRATION_APPROVED,          ("بطولة", "قبول فريق",     "تم قبول مشاركة فريق {teamName} في بطولة {tournamentName}",                     ActivitySeverity.Info,     "Tournament") },
        { TEAM_ELIMINATED,                ("بطولة", "إقصاء فريق",    "تم إقصاء فريق {teamName} من بطولة {tournamentName}",                           ActivitySeverity.Warning,  "Tournament") },
        { GROUPS_FINISHED,                ("بطولة", "انتهاء المجموعات","انتهت مرحلة المجموعات في بطولة {tournamentName}",                             ActivitySeverity.Info,     "Tournament") },
        { KNOCKOUT_STARTED,               ("بطولة", "بدء خروج المغلوب","بدأت مرحلة خروج المغلوب في بطولة {tournamentName}",                          ActivitySeverity.Info,     "Tournament") },

        // Team
        { TEAM_CREATED,     ("فريق", "إنشاء فريق",   "تم إنشاء فريق جديد: {teamName}",                        ActivitySeverity.Info,     "Team") },
        { TEAM_JOINED,      ("فريق", "انضمام لاعب",   "انضم اللاعب {playerName} إلى فريق {teamName}",           ActivitySeverity.Info,     "Team") },
        { TEAM_REMOVED,     ("فريق", "مغادرة لاعب",   "غادر اللاعب {playerName} فريق {teamName}",               ActivitySeverity.Warning,  "Team") },
        { TEAM_DISABLED,    ("إدارة", "تعطيل فريق",   "تم تعطيل فريق {teamName} من قبل الإدارة",               ActivitySeverity.Critical, "Team") },
        { TEAM_ACTIVATED,   ("إدارة", "تنشيط فريق",   "تم تنشيط فريق {teamName}",                              ActivitySeverity.Info,     "Team") },
        { TEAM_DEACTIVATED, ("إدارة", "إلغاء تنشيط",  "تم إلغاء تنشيط فريق {teamName}",                        ActivitySeverity.Warning,  "Team") },

        // Payment
        { PAYMENT_SUBMITTED, ("دفع", "إيصال دفع", "تم رفع إيصال دفع من قبل فريق {teamName}",    ActivitySeverity.Info,    "Payment") },
        { PAYMENT_APPROVED,  ("دفع", "قبول دفع",  "تم اعتماد دفع الرسوم لفرقة {teamName}",      ActivitySeverity.Info,    "Payment") },

        // Administrative
        { ADMIN_OVERRIDE, ("إدارة", "تدخل إداري", "تم تنفيذ إجراء طارئ: {action} في {tournamentName}. التفاصيل: {details}", ActivitySeverity.Critical, "System") }
    };

    /// <summary>Get localized metadata for an activity code.</summary>
    public static (string Category, string Title, string Message, ActivitySeverity Severity, string? EntityType) GetLocalized(
        string? code, Dictionary<string, string>? placeholders)
    {
        if (string.IsNullOrEmpty(code) || !Library.TryGetValue(code, out var template))
            return ("نظام", "إجراء نظام", "تم تنفيذ إجراء غير محدد في النظام", ActivitySeverity.Info, null);

        var message = template.MessageTemplate;
        if (placeholders != null)
        {
            foreach (var kvp in placeholders)
            {
                message = message.Replace($"{{{kvp.Key}}}", kvp.Value ?? "");
            }
        }

        return (template.CategoryAr, template.TitleAr, message, template.Severity, template.EntityType);
    }
}
