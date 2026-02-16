using System.Collections.Generic;
using Domain.Enums;

namespace Application.Common;

public static class NotificationTemplates
{
    public const string ACCOUNT_APPROVED = "ACCOUNT_APPROVED";
    public const string ACCOUNT_SUSPENDED = "ACCOUNT_SUSPENDED";
    public const string ROLE_CHANGED = "ROLE_CHANGED";
    
    public const string MATCH_CREATED = "MATCH_CREATED";
    public const string MATCH_TIME_CHANGED = "MATCH_TIME_CHANGED";
    public const string MATCH_POSTPONED = "MATCH_POSTPONED";
    public const string MATCH_CANCELED = "MATCH_CANCELED";
    public const string MATCH_STARTED = "MATCH_STARTED";
    public const string MATCH_ENDED = "MATCH_ENDED";
    public const string MATCH_EVENT_ADDED = "MATCH_EVENT_ADDED";
    public const string MATCH_SCORE_CHANGED = "MATCH_SCORE_CHANGED";
    
    public const string PLAYER_JOINED_TEAM = "PLAYER_JOINED_TEAM";
    public const string PLAYER_REMOVED = "PLAYER_REMOVED";
    public const string JOIN_REQUEST_RECEIVED = "JOIN_REQUEST_RECEIVED";
    public const string JOIN_REQUEST_REJECTED = "JOIN_REQUEST_REJECTED";
    public const string INVITE_RECEIVED = "INVITE_RECEIVED";
    public const string INVITE_ACCEPTED = "INVITE_ACCEPTED";
    public const string INVITE_REJECTED = "INVITE_REJECTED";
    
    public const string TEAM_APPROVED = "TEAM_APPROVED";
    public const string TEAM_REJECTED = "TEAM_REJECTED";
    public const string TEAM_ACTIVATED = "TEAM_ACTIVATED";
    public const string PAYMENT_APPROVED = "PAYMENT_APPROVED";
    public const string TOURNAMENT_MATCHES_READY = "TOURNAMENT_MATCHES_READY";
    public const string TOURNAMENT_ELIMINATED = "TOURNAMENT_ELIMINATED";
    public const string ADMIN_NEW_USER_REGISTERED = "ADMIN_NEW_USER_REGISTERED";
    public const string ADMIN_USER_VERIFIED_PENDING_APPROVAL = "ADMIN_USER_VERIFIED_PENDING_APPROVAL";
    public const string PASSWORD_CHANGED = "PASSWORD_CHANGED";

    /// <summary>Full template metadata: title, message, category, severity type, priority</summary>
    private sealed record TemplateData(
        string Title,
        string Message,
        NotificationCategory Category,
        NotificationType Type,
        NotificationPriority Priority = NotificationPriority.Normal);

    private static readonly Dictionary<string, TemplateData> Templates = new()
    {
        // ── Account ──
        { ACCOUNT_APPROVED,    new("تفعيل الحساب", "تم تفعيل حسابك بنجاح. يمكنك الآن استخدام كافة مميزات النظام.", NotificationCategory.Account, NotificationType.Success) },
        { ACCOUNT_SUSPENDED,   new("إيقاف الحساب", "تم إيقاف حسابك مؤقتاً. يرجى التواصل مع الإدارة للمزيد من التفاصيل.", NotificationCategory.Account, NotificationType.Warning, NotificationPriority.High) },
        { ROLE_CHANGED,        new("تغيير الصلاحيات", "تم تغيير صلاحيات حسابك إلى {role}.", NotificationCategory.Account, NotificationType.Info) },

        // ── Match ──
        { MATCH_CREATED,       new("مباراة جديدة", "تم إضافة مباراة جديدة ضد {opponent} بتاريخ {date}.", NotificationCategory.Match, NotificationType.Info) },
        { MATCH_TIME_CHANGED,  new("تعديل موعد", "تم تعديل موعد مباراتكم ضد {opponent} إلى {date}.", NotificationCategory.Match, NotificationType.Warning) },
        { MATCH_POSTPONED,     new("تأجيل مباراة", "تم تأجيل مباراتكم ضد {opponent}.", NotificationCategory.Match, NotificationType.Warning) },
        { MATCH_CANCELED,      new("إلغاء مباراة", "تم إلغاء مباراتكم ضد {opponent} رسمياً.", NotificationCategory.Match, NotificationType.Error) },
        { MATCH_STARTED,       new("بدء المباراة", "بدأت الآن مباراتكم ضد {opponent}. بالتوفيق!", NotificationCategory.Match, NotificationType.Success) },
        { MATCH_ENDED,         new("انتهاء المباراة", "انتهت مباراتكم ضد {opponent}. النتيجة النهائية: {score}.", NotificationCategory.Match, NotificationType.Info) },
        { MATCH_EVENT_ADDED,   new("حدث في المباراة", "تم تسجيل {eventType} في مباراتكم.", NotificationCategory.Match, NotificationType.Info) },
        { MATCH_SCORE_CHANGED, new("تعديل نتيجة", "تم تعديل نتيجة مباراتكم ضد {opponent} لتصبح {score} بواسطة الإدارة.", NotificationCategory.Match, NotificationType.Warning) },

        // ── Team ──
        { PLAYER_JOINED_TEAM,      new("انضمام لفريق", "تم قبول طلب انضمامك إلى فريق {teamName}.", NotificationCategory.Team, NotificationType.Success) },
        { PLAYER_REMOVED,          new("إزالة من فريق", "تمت إزالتك من فريق {teamName}.", NotificationCategory.Team, NotificationType.Warning, NotificationPriority.High) },
        { JOIN_REQUEST_RECEIVED,   new("طلب انضمام", "وصلك طلب انضمام جديد من اللاعب {playerName}.", NotificationCategory.Team, NotificationType.Info) },
        { JOIN_REQUEST_REJECTED,   new("رفض الطلب", "نأسف، تم رفض طلب انضمامك إلى فريق {teamName}.", NotificationCategory.Team, NotificationType.Warning) },
        { INVITE_RECEIVED,         new("دعوة انضمام", "تلقيت دعوة رسمية للانضمام إلى فريق {teamName}.", NotificationCategory.Team, NotificationType.Info, NotificationPriority.High) },
        { INVITE_ACCEPTED,         new("قبول الدعوة", "قبل اللاعب {playerName} دعوتكم للانضمام للفريق.", NotificationCategory.Team, NotificationType.Success) },
        { INVITE_REJECTED,         new("رفض الدعوة", "رفض اللاعب {playerName} دعوتكم للانضمام للفريق.", NotificationCategory.Team, NotificationType.Warning) },

        // ── Tournament ──
        { TEAM_APPROVED,              new("قبول الفريق", "تمت الموافقة على مشاركة فريقك في بطولة {tournamentName}.", NotificationCategory.Tournament, NotificationType.Success) },
        { TEAM_REJECTED,              new("رفض المشاركة", "نأسف، تم رفض طلب مشاركة فريقك في بطولة {tournamentName}: {reason}", NotificationCategory.Tournament, NotificationType.Error) },
        { TEAM_ACTIVATED,             new("تفعيل الفريق", "تمت إعادة تفعيل فريقك بنجاح. يمكنكم الآن المشاركة في الأنشطة الرياضية.", NotificationCategory.Team, NotificationType.Success) },
        { PAYMENT_APPROVED,           new("توثيق الدفع", "تم توثيق عملية الدفع الخاصة بالبطولة {tournamentName} بنجاح.", NotificationCategory.Payments, NotificationType.Success) },
        { TOURNAMENT_MATCHES_READY,   new("جدول المباريات", "تم توليد جدول المباريات لبطولة {tournamentName}. تحقق من مواعيدكم القادمة.", NotificationCategory.Tournament, NotificationType.Success) },
        { TOURNAMENT_ELIMINATED,      new("إقصاء الفريق", "للأسف، تم إقصاء فريقكم {teamName} من بطولة {tournamentName} من قبل اللجنة المنظمة.", NotificationCategory.Tournament, NotificationType.Error, NotificationPriority.High) },

        // ── Administrative ──
        { ADMIN_NEW_USER_REGISTERED,              new("تسجيل جديد", "قام مستخدم جديد بالتسجيل: {name} ({role}).", NotificationCategory.Administrative, NotificationType.Info) },
        { ADMIN_USER_VERIFIED_PENDING_APPROVAL,   new("مستخدم بانتظار الموافقة", "قام {name} ({email}) بتأكيد بريده الإلكتروني وبانتظار موافقتكم لتفعيل حسابه.", NotificationCategory.Administrative, NotificationType.Info, NotificationPriority.High) },

        // ── Security ──
        { PASSWORD_CHANGED, new("تغيير كلمة المرور", "تم تغيير كلمة مرور حسابك بنجاح.", NotificationCategory.Security, NotificationType.Info) }
    };

    /// <summary>Get full template metadata including category, type, and priority</summary>
    public static (string Title, string Message, NotificationCategory Category, NotificationType Type, NotificationPriority Priority) GetTemplate(
        string? key, Dictionary<string, string>? placeholders = null)
    {
        if (string.IsNullOrEmpty(key) || !Templates.TryGetValue(key, out var template))
            return ("إشعار جديد", "لديك إشعار جديد في النظام.", NotificationCategory.System, NotificationType.Info, NotificationPriority.Normal);

        var message = template.Message;
        if (placeholders != null)
        {
            foreach (var placeholder in placeholders)
            {
                message = message.Replace($"{{{placeholder.Key}}}", placeholder.Value);
            }
        }

        return (template.Title, message, template.Category, template.Type, template.Priority);
    }
}
