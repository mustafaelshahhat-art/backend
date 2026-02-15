using System.Collections.Generic;

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

    private static readonly Dictionary<string, (string Title, string Message)> Templates = new()
    {
        { ACCOUNT_APPROVED, ("تفعيل الحساب", "تم تفعيل حسابك بنجاح. يمكنك الآن استخدام كافة مميزات النظام.") },
        { ACCOUNT_SUSPENDED, ("إيقاف الحساب", "تم إيقاف حسابك مؤقتاً. يرجى التواصل مع الإدارة للمزيد من التفاصيل.") },
        { ROLE_CHANGED, ("تغيير الصلاحيات", "تم تغيير صلاحيات حسابك إلى {role}.") },
        
        { MATCH_CREATED, ("مباراة جديدة", "تم إضافة مباراة جديدة ضد {opponent} بتاريخ {date}.") },
        { MATCH_TIME_CHANGED, ("تعديل موعد", "تم تعديل موعد مباراتكم ضد {opponent} إلى {date}.") },
        { MATCH_POSTPONED, ("تأجيل مباراة", "تم تأجيل مباراتكم ضد {opponent}.") },
        { MATCH_CANCELED, ("إلغاء مباراة", "تم إلغاء مباراتكم ضد {opponent} رسمياً.") },
        { MATCH_STARTED, ("بدء المباراة", "بدأت الآن مباراتكم ضد {opponent}. بالتوفيق!") },
        { MATCH_ENDED, ("انتهاء المباراة", "انتهت مباراتكم ضد {opponent}. النتيجة النهائية: {score}.") },
        { MATCH_EVENT_ADDED, ("حدث في المباراة", "تم تسجيل {eventType} في مباراتكم.") },

        { MATCH_SCORE_CHANGED, ("تعديل نتيجة", "تم تعديل نتيجة مباراتكم ضد {opponent} لتصبح {score} بواسطة الإدارة.") },
        
        { PLAYER_JOINED_TEAM, ("انضمام لفريق", "تم قبول طلب انضمامك إلى فريق {teamName}.") },
        { PLAYER_REMOVED, ("إزالة من فريق", "تمت إزالتك من فريق {teamName}.") },
        { JOIN_REQUEST_RECEIVED, ("طلب انضمام", "وصلك طلب انضمام جديد من اللاعب {playerName}.") },
        { JOIN_REQUEST_REJECTED, ("رفض الطلب", "نأسف، تم رفض طلب انضمامك إلى فريق {teamName}.") },
        { INVITE_RECEIVED, ("دعوة انضمام", "تلقيت دعوة رسمية للانضمام إلى فريق {teamName}.") },
        { INVITE_ACCEPTED, ("قبول الدعوة", "قبل اللاعب {playerName} دعوتكم للانضمام للفريق.") },
        { INVITE_REJECTED, ("رفض الدعوة", "رفض اللاعب {playerName} دعوتكم للانضمام للفريق.") },
        

        
        { TEAM_APPROVED, ("قبول الفريق", "تمت الموافقة على مشاركة فريقك في بطولة {tournamentName}.") },
        { TEAM_REJECTED, ("رفض المشاركة", "نأسف، تم رفض طلب مشاركة فريقك في بطولة {tournamentName}: {reason}") },
        { TEAM_ACTIVATED, ("تفعيل الفريق", "تمت إعادة تفعيل فريقك بنجاح. يمكنكم الآن المشاركة في الأنشطة الرياضية.") },
        { PAYMENT_APPROVED, ("توثيق الدفع", "تم توثيق عملية الدفع الخاصة بالبطولة {tournamentName} بنجاح.") },
        { TOURNAMENT_MATCHES_READY, ("جدول المباريات", "تم توليد جدول المباريات لبطولة {tournamentName}. تحقق من مواعيدكم القادمة.") },
        { TOURNAMENT_ELIMINATED, ("إقصاء الفريق", "للأسف، تم إقصاء فريقكم {teamName} من بطولة {tournamentName} من قبل اللجنة المنظمة.") },
        { ADMIN_NEW_USER_REGISTERED, ("تسجيل جديد", "قام مستخدم جديد بالتسجيل: {name} ({role}).") },
        { ADMIN_USER_VERIFIED_PENDING_APPROVAL, ("مستخدم بانتظار الموافقة", "قام {name} ({email}) بتأكيد بريده الإلكتروني وبانتظار موافقتكم لتفعيل حسابه.") },
        { PASSWORD_CHANGED, ("تغيير كلمة المرور", "تم تغيير كلمة مرور حسابك بنجاح.") }
    };

    public static (string Title, string Message) GetTemplate(string? key, Dictionary<string, string>? placeholders = null)
    {
        if (string.IsNullOrEmpty(key) || !Templates.ContainsKey(key))
            return ("إشعار جديد", "لديك إشعار جديد في النظام.");

        var template = Templates[key];
        var message = template.Message;

        if (placeholders != null)
        {
            foreach (var placeholder in placeholders)
            {
                message = message.Replace($"{{{placeholder.Key}}}", placeholder.Value);
            }
        }

        return (template.Title, message);
    }
}
