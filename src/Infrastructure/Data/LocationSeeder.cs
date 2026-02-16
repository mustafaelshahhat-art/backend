using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data;

/// <summary>
/// Seeds Egyptian governorates, cities, and areas into the database.
/// Runs idempotently — skips if data already exists.
/// </summary>
public static class LocationSeeder
{
    public static void Seed(AppDbContext db, ILogger logger)
    {
        if (db.Governorates.Any())
        {
            logger.LogInformation("Location data already seeded — skipping.");
            return;
        }

        logger.LogInformation("Seeding Egyptian location data …");

        var now = DateTime.UtcNow;
        var governorates = BuildLocationTree(now);

        db.Governorates.AddRange(governorates);
        db.SaveChanges();

        var govCount = governorates.Count;
        var cityCount = governorates.Sum(g => g.Cities.Count);
        var areaCount = governorates.Sum(g => g.Cities.Sum(c => c.Areas.Count));

        logger.LogInformation(
            "Seeded {Govs} governorates, {Cities} cities, {Areas} areas.",
            govCount, cityCount, areaCount);
    }

    // ─────────────────────────────────────────────────────────
    //  Data: Egyptian governorates → cities → areas
    // ─────────────────────────────────────────────────────────

    private static List<Governorate> BuildLocationTree(DateTime now)
    {
        int govSort = 0;

        Governorate Gov(string ar, string en, params (string ar, string en, (string ar, string en)[] areas)[] cities)
        {
            int citySort = 0;
            var gov = new Governorate
            {
                Id = Guid.NewGuid(),
                NameAr = ar,
                NameEn = en,
                IsActive = true,
                SortOrder = ++govSort,
                CreatedAt = now,
                UpdatedAt = now,
                Cities = new List<City>()
            };

            foreach (var (cAr, cEn, areas) in cities)
            {
                int areaSort = 0;
                var city = new City
                {
                    Id = Guid.NewGuid(),
                    NameAr = cAr,
                    NameEn = cEn,
                    GovernorateId = gov.Id,
                    IsActive = true,
                    SortOrder = ++citySort,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Areas = new List<Area>()
                };

                foreach (var (aAr, aEn) in areas)
                {
                    city.Areas.Add(new Area
                    {
                        Id = Guid.NewGuid(),
                        NameAr = aAr,
                        NameEn = aEn,
                        CityId = city.Id,
                        IsActive = true,
                        SortOrder = ++areaSort,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }

                gov.Cities.Add(city);
            }

            return gov;
        }

        return new List<Governorate>
        {
            // ── Cairo ──
            Gov("القاهرة", "Cairo",
                ("المعادي", "Maadi", new[]
                {
                    ("المعادي الجديدة", "New Maadi"),
                    ("المعادي السرايات", "Maadi Sarayat"),
                    ("زهراء المعادي", "Zahraa El Maadi"),
                    ("دجلة", "Degla"),
                    ("الحي التاسع", "9th District")
                }),
                ("القاهرة الجديدة", "New Cairo", new[]
                {
                    ("التجمع الخامس", "5th Settlement"),
                    ("التجمع الثالث", "3rd Settlement"),
                    ("الرحاب", "Rehab City"),
                    ("مدينتي", "Madinaty"),
                    ("بيت الوطن", "Beit El Watan")
                }),
                ("مصر الجديدة", "Heliopolis", new[]
                {
                    ("الكوربة", "Korba"),
                    ("ميدان الحجاز", "Hegaz Square"),
                    ("روكسي", "Roxi"),
                    ("ألماظة", "Almaza")
                }),
                ("مدينة نصر", "Nasr City", new[]
                {
                    ("المنطقة الأولى", "Zone 1"),
                    ("المنطقة السادسة", "Zone 6"),
                    ("المنطقة الثامنة", "Zone 8"),
                    ("المنطقة العاشرة", "Zone 10"),
                    ("عباس العقاد", "Abbas El Akkad")
                }),
                ("وسط البلد", "Downtown", new[]
                {
                    ("التحرير", "Tahrir"),
                    ("عابدين", "Abdeen"),
                    ("باب اللوق", "Bab El Louk")
                }),
                ("المقطم", "Mokattam", new[]
                {
                    ("المقطم الهضبة الوسطى", "Mokattam Mid Plateau"),
                    ("الهضبة العليا", "Upper Plateau")
                }),
                ("شبرا", "Shubra", new[]
                {
                    ("شبرا الخيمة", "Shubra El Kheima"),
                    ("روض الفرج", "Rod El Farag")
                })
            ),

            // ── Giza ──
            Gov("الجيزة", "Giza",
                ("الدقي", "Dokki", new[]
                {
                    ("ميدان المساحة", "Mesaha Square"),
                    ("شارع التحرير", "Tahrir Street"),
                    ("البحوث", "Bohouth")
                }),
                ("المهندسين", "Mohandessin", new[]
                {
                    ("شارع جامعة الدول", "Gameat El Dowal St"),
                    ("ميدان سفنكس", "Sphinx Square"),
                    ("شارع أحمد عرابي", "Ahmad Orabi St")
                }),
                ("6 أكتوبر", "6th of October", new[]
                {
                    ("الحي الأول", "1st District"),
                    ("الحي المتميز", "Premium District"),
                    ("الحي الثامن", "8th District"),
                    ("الشيخ زايد", "Sheikh Zayed")
                }),
                ("الهرم", "Haram", new[]
                {
                    ("فيصل", "Faisal"),
                    ("الطالبية", "Talbeya"),
                    ("المريوطية", "Marioteya")
                }),
                ("العجوزة", "Agouza", new[]
                {
                    ("شارع النيل", "Nile Street"),
                    ("شارع وادي النيل", "Wadi El Nile")
                })
            ),

            // ── Alexandria ──
            Gov("الإسكندرية", "Alexandria",
                ("وسط الإسكندرية", "Downtown Alex", new[]
                {
                    ("محطة الرمل", "Raml Station"),
                    ("المنشية", "Manshiya"),
                    ("العطارين", "Attarin")
                }),
                ("سموحة", "Smouha", new[]
                {
                    ("سموحة الرئيسي", "Smouha Main"),
                    ("سيدي جابر", "Sidi Gaber")
                }),
                ("المنتزه", "Montazah", new[]
                {
                    ("المعمورة", "Maamoura"),
                    ("كليوباترا", "Cleopatra"),
                    ("سان ستيفانو", "San Stefano")
                }),
                ("العجمي", "Agami", new[]
                {
                    ("البيطاش", "Bitash"),
                    ("الهانوفيل", "Hannoville")
                }),
                ("برج العرب", "Borg El Arab", new[]
                {
                    ("برج العرب الجديدة", "New Borg El Arab"),
                    ("المنطقة الصناعية", "Industrial Zone")
                })
            ),

            // ── Qalyubia ──
            Gov("القليوبية", "Qalyubia",
                ("شبرا الخيمة", "Shubra El Kheima", new[]
                {
                    ("المنطقة الأولى", "Zone 1"),
                    ("المنطقة الثانية", "Zone 2")
                }),
                ("بنها", "Benha", new[]
                {
                    ("وسط بنها", "Downtown Benha"),
                    ("كفر الجزار", "Kafr El Gazzar")
                }),
                ("العبور", "Obour", new[]
                {
                    ("الحي الأول", "1st District"),
                    ("الحي السادس", "6th District"),
                    ("الجولف", "Golf District")
                })
            ),

            // ── Dakahlia ──
            Gov("الدقهلية", "Dakahlia",
                ("المنصورة", "Mansoura", new[]
                {
                    ("حي الجامعة", "University District"),
                    ("توريل", "Tawriel"),
                    ("حي الجلاء", "Galaa District")
                }),
                ("طلخا", "Talkha", new[]
                {
                    ("وسط طلخا", "Downtown Talkha")
                }),
                ("ميت غمر", "Mit Ghamr", new[]
                {
                    ("وسط المدينة", "City Center")
                })
            ),

            // ── Sharqia ──
            Gov("الشرقية", "Sharqia",
                ("الزقازيق", "Zagazig", new[]
                {
                    ("حي أول", "1st Neighborhood"),
                    ("حي ثان", "2nd Neighborhood")
                }),
                ("العاشر من رمضان", "10th of Ramadan", new[]
                {
                    ("الحي الأول", "1st District"),
                    ("الحي الثاني", "2nd District"),
                    ("المنطقة الصناعية", "Industrial Zone")
                })
            ),

            // ── Gharbia ──
            Gov("الغربية", "Gharbia",
                ("طنطا", "Tanta", new[]
                {
                    ("حي أول", "1st District"),
                    ("حي ثان", "2nd District"),
                    ("المحلة الكبرى", "El Mahalla El Kubra")
                }),
                ("المحلة الكبرى", "El Mahalla", new[]
                {
                    ("وسط المحلة", "Downtown Mahalla"),
                    ("شبرا النملة", "Shubra El Namla")
                })
            ),

            // ── Monufia ──
            Gov("المنوفية", "Monufia",
                ("شبين الكوم", "Shebin El Kom", new[]
                {
                    ("وسط المدينة", "City Center")
                }),
                ("منوف", "Menouf", new[]
                {
                    ("وسط منوف", "Downtown Menouf")
                }),
                ("السادات", "Sadat City", new[]
                {
                    ("المنطقة الصناعية", "Industrial Zone"),
                    ("المنطقة السكنية", "Residential Zone")
                })
            ),

            // ── Beheira ──
            Gov("البحيرة", "Beheira",
                ("دمنهور", "Damanhour", new[]
                {
                    ("وسط دمنهور", "Downtown Damanhour")
                }),
                ("كفر الدوار", "Kafr El Dawwar", new[]
                {
                    ("وسط المدينة", "City Center")
                })
            ),

            // ── Kafr El Sheikh ──
            Gov("كفر الشيخ", "Kafr El Sheikh",
                ("كفر الشيخ", "Kafr El Sheikh City", new[]
                {
                    ("وسط المدينة", "City Center")
                }),
                ("دسوق", "Desouk", new[]
                {
                    ("وسط دسوق", "Downtown Desouk")
                })
            ),

            // ── Damietta ──
            Gov("دمياط", "Damietta",
                ("دمياط", "Damietta City", new[]
                {
                    ("وسط دمياط", "Downtown Damietta"),
                    ("رأس البر", "Ras El Bar")
                }),
                ("دمياط الجديدة", "New Damietta", new[]
                {
                    ("المنطقة الأولى", "Zone 1")
                })
            ),

            // ── Port Said ──
            Gov("بورسعيد", "Port Said",
                ("بورسعيد", "Port Said City", new[]
                {
                    ("حي الشرق", "East District"),
                    ("حي العرب", "Arab District"),
                    ("حي الزهور", "Zohour District")
                })
            ),

            // ── Ismailia ──
            Gov("الإسماعيلية", "Ismailia",
                ("الإسماعيلية", "Ismailia City", new[]
                {
                    ("حي أول", "1st District"),
                    ("حي ثان", "2nd District"),
                    ("حي ثالث", "3rd District")
                })
            ),

            // ── Suez ──
            Gov("السويس", "Suez",
                ("السويس", "Suez City", new[]
                {
                    ("حي الأربعين", "Arbaeen District"),
                    ("حي عتاقة", "Ataka District")
                })
            ),

            // ── Fayoum ──
            Gov("الفيوم", "Fayoum",
                ("الفيوم", "Fayoum City", new[]
                {
                    ("وسط الفيوم", "Downtown Fayoum")
                }),
                ("الفيوم الجديدة", "New Fayoum", new[]
                {
                    ("المنطقة السكنية", "Residential Zone")
                })
            ),

            // ── Beni Suef ──
            Gov("بني سويف", "Beni Suef",
                ("بني سويف", "Beni Suef City", new[]
                {
                    ("وسط المدينة", "City Center")
                }),
                ("بني سويف الجديدة", "New Beni Suef", new[]
                {
                    ("المنطقة السكنية", "Residential Zone")
                })
            ),

            // ── Minya ──
            Gov("المنيا", "Minya",
                ("المنيا", "Minya City", new[]
                {
                    ("وسط المنيا", "Downtown Minya")
                }),
                ("المنيا الجديدة", "New Minya", new[]
                {
                    ("المنطقة السكنية", "Residential Zone")
                })
            ),

            // ── Assiut ──
            Gov("أسيوط", "Assiut",
                ("أسيوط", "Assiut City", new[]
                {
                    ("وسط أسيوط", "Downtown Assiut")
                }),
                ("أسيوط الجديدة", "New Assiut", new[]
                {
                    ("المنطقة السكنية", "Residential Zone")
                })
            ),

            // ── Sohag ──
            Gov("سوهاج", "Sohag",
                ("سوهاج", "Sohag City", new[]
                {
                    ("وسط سوهاج", "Downtown Sohag")
                })
            ),

            // ── Qena ──
            Gov("قنا", "Qena",
                ("قنا", "Qena City", new[]
                {
                    ("وسط قنا", "Downtown Qena")
                })
            ),

            // ── Luxor ──
            Gov("الأقصر", "Luxor",
                ("الأقصر", "Luxor City", new[]
                {
                    ("وسط الأقصر", "Downtown Luxor"),
                    ("الكرنك", "Karnak")
                })
            ),

            // ── Aswan ──
            Gov("أسوان", "Aswan",
                ("أسوان", "Aswan City", new[]
                {
                    ("وسط أسوان", "Downtown Aswan")
                })
            ),

            // ── Red Sea ──
            Gov("البحر الأحمر", "Red Sea",
                ("الغردقة", "Hurghada", new[]
                {
                    ("الدهار", "Dahar"),
                    ("السقالة", "Sekalla"),
                    ("الممشى السياحي", "Tourist Promenade")
                })
            ),

            // ── Matrouh ──
            Gov("مطروح", "Matrouh",
                ("مرسى مطروح", "Marsa Matrouh", new[]
                {
                    ("وسط المدينة", "City Center"),
                    ("العلمين الجديدة", "New Alamein")
                })
            ),

            // ── North Sinai ──
            Gov("شمال سيناء", "North Sinai",
                ("العريش", "Arish", new[]
                {
                    ("وسط العريش", "Downtown Arish")
                })
            ),

            // ── South Sinai ──
            Gov("جنوب سيناء", "South Sinai",
                ("شرم الشيخ", "Sharm El Sheikh", new[]
                {
                    ("نعمة باي", "Naama Bay"),
                    ("خليج نعمة", "Naama Gulf"),
                    ("هضبة أم السيد", "Um El Sid Plateau")
                })
            ),

            // ── New Valley ──
            Gov("الوادي الجديد", "New Valley",
                ("الخارجة", "Kharga", new[]
                {
                    ("وسط الخارجة", "Downtown Kharga")
                })
            )
        };
    }
}
