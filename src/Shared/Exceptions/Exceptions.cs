using System;

namespace Shared.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
    public NotFoundException(string name, object key) : base($"العنصر المطلوب ({name}: {key}) غير موجود.") { }
}

public class BadRequestException : Exception
{
    public BadRequestException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors) 
        : base("يرجى مراجعة البيانات المدخلة وتصحيح الأخطاء.")
    {
        Errors = errors;
    }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public class EmailNotVerifiedException : Exception
{
    public string Email { get; }
    public EmailNotVerifiedException(string email, string message = "يجب تأكيد البريد الإلكتروني قبل تسجيل الدخول") 
        : base(message) 
    {
        Email = email;
    }
}
