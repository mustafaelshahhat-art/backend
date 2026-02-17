# دليل النشر على Production - RunASP.NET

## معلومات Production

### الروابط
- **Backend API:** https://korazon365.runasp.net/
- **Frontend:** https://korazone365.com
- **Frontend (www):** https://www.korazone365.com

### قاعدة البيانات
- **Server:** db41621.public.databaseasp.net
- **Database:** db41621
- **User:** db41621

---

## خطوات النشر

### 1️⃣ البناء والنشر
```powershell
cd RamadanBackend
dotnet clean
dotnet restore
dotnet build --configuration Release
dotnet publish --configuration Release --output ./publish
```

### 2️⃣ التحقق من الملفات
تأكد من وجود الملفات التالية في مجلد `publish`:
- ✅ `Api.dll`
- ✅ `appsettings.json`
- ✅ `appsettings.Production.json`
- ✅ `web.config`
- ✅ جميع ملفات DLL المطلوبة

### 3️⃣ رفع الملفات إلى RunASP.NET
1. افتح لوحة تحكم RunASP.NET
2. انتقل إلى File Manager أو استخدم FTP
3. احذف المحتويات القديمة (أو اعمل backup)
4. ارفع محتويات مجلد `publish` بالكامل

### 4️⃣ إعدادات البيئة
تأكد من تفعيل:
```
ASPNETCORE_ENVIRONMENT=Production
```

في لوحة تحكم RunASP.NET → Configuration → Environment Variables

---

## التحقق من الإعدادات ✅

### CORS Settings
```json
"AllowedOrigins": [
  "https://korazone365.com",
  "https://www.korazone365.com"
]
```
✅ **صحيح** - يسمح للفرونت إند بالاتصال

### Connection String
```
Server=db41621.public.databaseasp.net; 
Database=db41621; 
User Id=db41621; 
Password=qG!76-bTr3P=; 
Encrypt=True; 
TrustServerCertificate=True; 
MultipleActiveResultSets=True;
```
✅ **صحيح**

### JWT Settings
```json
"JwtSettings": {
  "Issuer": "RamadanTournamentApi",
  "Audience": "RamadanTournamentApp",
  "Secret": "RamadanTournament_SecretKey_2024_Production_Secure_Must_Be_Long"
}
```
✅ **صحيح** - Secret طويل بما يكفي (16+ حرف)

### Redis
```json
"Redis": {
  "ConnectionString": ""
},
"DistributedLock": {
  "Provider": "Sql"
}
```
✅ **صحيح** - Redis معطل، استخدام SQL Distributed Lock

### Admin Settings
```json
"AdminSettings": {
  "Password": "Admin@123"
}
```
✅ **صحيح** - سيتم إنشاء Admin user عند أول تشغيل

---

## ما سيحدث عند أول تشغيل

1. ✅ التطبيق يقرأ `appsettings.Production.json`
2. ✅ يتصل بقاعدة البيانات على `db41621.public.databaseasp.net`
3. ✅ يطبق جميع Migrations تلقائياً (41 migration)
4. ✅ ينشئ Admin user:
   - Email: `admin@test.com`
   - Password: `Admin@123`
   - Role: Admin
5. ✅ يبدأ التطبيق ويصبح جاهزاً

---

## تجربة API بعد النشر

### Health Check
```bash
GET https://korazon365.runasp.net/health/live
```
**المتوقع:** 200 OK - "Healthy"

### Swagger UI
```
https://korazon365.runasp.net/swagger
```
**المتوقع:** واجهة Swagger مع جميع Endpoints

### تسجيل الدخول كـ Admin
```bash
POST https://korazon365.runasp.net/api/auth/login
Content-Type: application/json

{
  "email": "admin@test.com",
  "password": "Admin@123"
}
```
**المتوقع:** 200 OK + JWT Token

---

## استكشاف الأخطاء

### خطأ 500.30 - ANCM In-Process Start Failure

**الأسباب المحتملة:**
1. ❌ `ASPNETCORE_ENVIRONMENT` غير مضبوط على `Production`
2. ❌ Connection String خاطئ
3. ❌ JwtSettings:Secret مفقود أو قصير
4. ❌ ملفات DLL مفقودة

**الحل:** 
- تحقق من Logs في لوحة التحكم
- تأكد من رفع جميع الملفات من مجلد `publish`

### خطأ Database Migration

**المشكلة:** فشل في تطبيق Migrations

**الحل:**
```powershell
# تطبيق Migrations يدوياً:
dotnet ef database update --project src/Infrastructure --startup-project src/Api --configuration Release
```

### CORS Error في Frontend

**المشكلة:** `Access-Control-Allow-Origin` error

**التحقق:**
1. تأكد أن `AllowedOrigins` يحتوي على:
   - `https://korazone365.com`
   - `https://www.korazone365.com`
2. تأكد من **عدم** وجود `/` في النهاية
3. استخدم **HTTPS** وليس HTTP

---

## SignalR Hubs

### Notification Hub
```
wss://korazon365.runasp.net/hubs/notifications
```

### Match Chat Hub
```
wss://korazon365.runasp.net/hubs/chat
```

**ملاحظة:** SignalR يعمل مع In-Memory backplane (بدون Redis)

---

## الأداء والمراقبة

### Logs
- **الموقع:** `/logs/log-{date}.txt`
- **Format:** Structured logging with Serilog
- **Levels:** Warning و Error فقط في Production

### Health Endpoints
- `/health/live` - App is running
- `/health/ready` - Database is accessible

### Rate Limiting
- **Max Requests:** 100 request per minute per IP
- **Response:** 429 Too Many Requests عند التجاوز

---

## Frontend Configuration

تأكد من تحديث Frontend `environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  apiUrl: 'https://korazon365.runasp.net/api',
  hubUrl: 'https://korazon365.runasp.net'
};
```

---

## نصائح مهمة

### 🔒 Security
- ✅ HTTPS مفعّل تلقائياً على RunASP.NET
- ✅ CORS محدد للنطاقات المسموحة فقط
- ✅ JWT Token expiry = 60 minutes
- ✅ Security Headers مفعّلة

### ⚡ Performance
- ✅ Response Compression مفعّل
- ✅ Distributed Lock على SQL (بدون Redis)
- ✅ In-Memory caching للبيانات المتكررة
- ✅ Connection Pooling مفعّل

### 📊 Monitoring
- تحقق من Logs يومياً
- راقب Database size
- استخدم `/health/ready` للمراقبة

---

## تحديثات مستقبلية

لرفع تحديث جديد:
```powershell
# 1. Build
dotnet publish --configuration Release --output ./publish

# 2. Backup الملفات القديمة على الخادم

# 3. رفع الملفات الجديدة
# (استخدم FTP أو File Manager)

# 4. إعادة تشغيل التطبيق من لوحة التحكم
```

**ملاحظة:** Migrations الجديدة ستطبق تلقائياً عند إعادة التشغيل

---

## الدعم

### روابط مفيدة
- **Swagger:** https://korazon365.runasp.net/swagger
- **Health:** https://korazon365.runasp.net/health/ready
- **Frontend:** https://korazone365.com

### معلومات تسجيل الدخول الافتراضية
- **Email:** admin@test.com
- **Password:** Admin@123
- **Role:** Admin

⚠️ **مهم:** غيّر كلمة مرور Admin بعد أول تسجيل دخول!

---

**تاريخ الإعداد:** 16 فبراير 2026  
**حالة التطبيق:** ✅ جاهز للنشر  
**Backend URL:** https://korazon365.runasp.net/  
**Frontend URL:** https://korazone365.com
