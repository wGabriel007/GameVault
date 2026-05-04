using Microsoft.EntityFrameworkCore;
using Biblioteca_de_Jogos.Data;
using Biblioteca_de_Jogos.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSession();

builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

builder.Services.AddScoped<Biblioteca_de_Jogos.Services.IEmailService,
                            Biblioteca_de_Jogos.Services.EmailService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Loguin}/{id?}")
    .WithStaticAssets();

// Seed automático do admin ao iniciar a aplicação
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var adminJaExiste = await context.Usuarios
        .AnyAsync(u => u.Nome == "admin");

    if (!adminJaExiste)
    {
        context.Usuarios.Add(new Usuario
        {
            Nome    = "admin",
            Senha   = BCrypt.Net.BCrypt.HashPassword("admin123"),
            IsAdmin = true
        });

        await context.SaveChangesAsync();

    }
}

app.Run();
