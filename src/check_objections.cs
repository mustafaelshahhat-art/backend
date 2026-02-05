using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true);
var configuration = builder.Build();

var services = new ServiceCollection();
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=../Api/ramadan.db")); // Path might need adjustment

var serviceProvider = services.BuildServiceProvider();
using var scope = serviceProvider.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var count = dbContext.Set<Domain.Entities.Objection>().Count();
Console.WriteLine($"TOTAL_OBJECTIONS_COUNT: {count}");
