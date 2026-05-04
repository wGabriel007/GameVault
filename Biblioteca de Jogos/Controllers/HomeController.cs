using Biblioteca_de_Jogos.Models;
using Biblioteca_de_Jogos.Data;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Biblioteca_de_Jogos.Services;

namespace Biblioteca_de_Jogos.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public HomeController(ILogger<HomeController> logger,
                              ApplicationDbContext context,
                              IEmailService emailService)
        {
            _logger       = logger;
            _context      = context;
            _emailService = emailService;
        }

        public IActionResult Loguin() => View();

        [HttpGet]
        public IActionResult Cadastro() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cadastro(Usuario user)
        {
                if (ModelState.IsValid)
            {

                var emailJaExiste = await _context.Usuarios
                    .AnyAsync(u => u.Email == user.Email);

                if (emailJaExiste)
                {
                    ModelState.AddModelError("Email", "Este Email já está em uso.");
                    return View(user);
                }

                var nomeJaExiste = await _context.Usuarios
                    .AnyAsync(u => u.Nome == user.Nome);

                if (nomeJaExiste)
                {
                    ModelState.AddModelError("Nome", "Este nome de usuário já está em uso.");
                    return View(user);
                }

                // Criptografa a senha antes de salvar
                user.Senha = BCrypt.Net.BCrypt.HashPassword(user.Senha);

                _context.Usuarios.Add(user);
                await _context.SaveChangesAsync();

                // Inicia a sessão automaticamente após o cadastro
                HttpContext.Session.SetString("UsuarioId",   user.Id.ToString());
                HttpContext.Session.SetString("UsuarioNome", user.Nome);
                HttpContext.Session.SetString("IsAdmin",     user.IsAdmin.ToString());
                TempData["SuccessMessage"] = $"Bem-vindo, {user.Nome}!";

                return RedirectToAction("Index", "Jogos");
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Loguin(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Busca apenas pelo nome (não pela senha, pois está criptografada)
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                // Verifica se a senha informada bate com o hash salvo
                if (usuario != null && BCrypt.Net.BCrypt.Verify(model.Password, usuario.Senha))
                {
                    HttpContext.Session.SetString("UsuarioId",   usuario.Id.ToString());
                    HttpContext.Session.SetString("UsuarioNome", usuario.Nome);
                    HttpContext.Session.SetString("IsAdmin",     usuario.IsAdmin.ToString());
                    TempData["SuccessMessage"] = $"Bem-vindo, {usuario.Nome}!";

                    return RedirectToAction("Index", "Jogos");
                }

                ModelState.AddModelError("", "E-mail ou senha inválidos.");
            }

            return View(model);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Loguin");
        }

        public IActionResult Privacy() => View();

        // GET: /Home/EsqueceuSenha
        [HttpGet]
        public IActionResult EsqueceuSenha() => View();

        // POST: /Home/EsqueceuSenha
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EsqueceuSenha(EsqueceuSenhaViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (usuario == null)
            {
                ModelState.AddModelError("", "E-mail não encontrado.");
                return View(model);
            }

            var codigo = new Random().Next(100000, 999999).ToString();

            _context.CodigosRecuperacao.Add(new CodigoRecuperacao
            {
                Email     = model.Email,
                Codigo    = codigo,
                Expiracao = DateTime.UtcNow.AddMinutes(15)
            });
            await _context.SaveChangesAsync();

            await _emailService.EnviarAsync(
                model.Email,
                "Código de Recuperação de Senha",
                $"<p>Seu código de recuperação é: <strong>{codigo}</strong></p>" +
                $"<p>Válido por 15 minutos.</p>");

            TempData["EmailRecuperacao"] = model.Email;
            return RedirectToAction("VerificarCodigo");
        }

        // GET: /Home/VerificarCodigo
        [HttpGet]
        public IActionResult VerificarCodigo()
        {
            var model = new VerificarCodigoViewModel
            {
                Email = TempData["EmailRecuperacao"] as string ?? ""
            };
            return View(model);
        }

        // POST: /Home/VerificarCodigo
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> VerificarCodigo(VerificarCodigoViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var registro = await _context.CodigosRecuperacao
                .Where(c => c.Email  == model.Email  &&
                            c.Codigo == model.Codigo &&
                            !c.Usado &&
                            c.Expiracao > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (registro == null)
            {
                ModelState.AddModelError("", "Código inválido ou expirado.");
                return View(model);
            }

            return RedirectToAction("NovaSenha", new
            {
                email  = model.Email,
                codigo = model.Codigo
            });
        }

        // GET: /Home/NovaSenha
        [HttpGet]
        public IActionResult NovaSenha(string email, string codigo)
            => View(new NovaSenhaViewModel { Email = email, Codigo = codigo });

        // POST: /Home/NovaSenha
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> NovaSenha(NovaSenhaViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var registro = await _context.CodigosRecuperacao
                .FirstOrDefaultAsync(c => c.Email  == model.Email  &&
                                          c.Codigo == model.Codigo &&
                                          !c.Usado &&
                                          c.Expiracao > DateTime.UtcNow);
            if (registro == null)
            {
                ModelState.AddModelError("", "Sessão expirada. Solicite um novo código.");
                return View(model);
            }

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (usuario == null) return NotFound();

            usuario.Senha  = BCrypt.Net.BCrypt.HashPassword(model.NovaSenha);
            registro.Usado = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Senha alterada com sucesso! Faça login.";
            return RedirectToAction("Loguin");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
