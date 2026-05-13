using Biblioteca_de_Jogos.Data;
using Biblioteca_de_Jogos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca_de_Jogos.Controllers
{
    public class JogosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public JogosController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsAdmin() =>
            HttpContext.Session.GetString("IsAdmin") == "True";

        private string? UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioNome");

        private bool TemPermissao(Jogo jogo) =>
            IsAdmin() || jogo.txt_Dono == UsuarioLogado();

        // GET: /Jogos
        public async Task<IActionResult> Index()
        {
            var nomeUsuario = HttpContext.Session.GetString("UsuarioNome");
            if (nomeUsuario == null) return RedirectToAction("Loguin", "Home");

            var jogos = await _context.Jogos.ToListAsync();

            // Solicitações pendentes exibidas nos cards
            var solicitacoes = await _context.Solicitacoes
                .Where(s => s.int_Status == (int)StatusSolicitacao.Pendente)
                .ToListAsync();

            // Pedidos recebidos nos jogos do usuário (ele é o dono)
            var pedidosRecebidos = solicitacoes
                .Where(s => s.str_DonoNome == nomeUsuario)
                .ToList();

            // Respostas dos pedidos feitos pelo usuário (ele é o solicitante), ainda não visualizadas
            var minhasRespostas = await _context.Solicitacoes
                .Where(s => s.str_SolicitanteNome == nomeUsuario &&
                            s.int_Status != (int)StatusSolicitacao.Pendente &&
                            !s.bool_Visualizada)
                .ToListAsync();

            ViewBag.Solicitacoes = solicitacoes;
            ViewBag.TotalPendentes = pedidosRecebidos.Count + minhasRespostas.Count;

            ViewBag.Avaliacoes = await _context.Avaliacoes.ToListAsync();

            ViewBag.TotalMeusJogos = jogos.Count(j => j.txt_Dono == nomeUsuario);
            ViewBag.TotalComunidade = jogos.Count(j => j.txt_Dono != nomeUsuario);

            return View(jogos);
        }

        public async Task<IActionResult> MeusJogos()
        {
            var nomeUsuario = HttpContext.Session.GetString("UsuarioNome");
            if (nomeUsuario == null) return RedirectToAction("Loguin", "Home");

            var meusJogos = await _context.Jogos
                .Where(j => j.txt_Dono == nomeUsuario)
                .ToListAsync();

            return View(meusJogos);
        }

        public async Task<IActionResult> Comunidade()
        {
            var nomeUsuario = HttpContext.Session.GetString("UsuarioNome");
            if (nomeUsuario == null) return RedirectToAction("Loguin", "Home");

            var jogosOutros = await _context.Jogos
                .Where(j => j.txt_Dono != nomeUsuario)
                .ToListAsync();

            return View(jogosOutros);
        }

        private async Task CarregarConsoles(string? consoleSelecionado = null)
        {
            var consoles = await _context.Consoles
                .OrderBy(c => c.str_Grupo)
                .ThenBy(c => c.str_Nome)
                .ToListAsync();

            ViewBag.Consoles = consoles;
            ViewBag.ConsoleSelecionado = consoleSelecionado;
        }

        // GET: /Jogos/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (UsuarioLogado() == null)
                return RedirectToAction("Loguin", "Home");

            await CarregarConsoles();
            return View();
        }

        // POST: /Jogos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Jogo jogo)
        {
            if (ModelState.IsValid)
            {
                jogo.txt_Dono = UsuarioLogado()!;
                _context.Jogos.Add(jogo);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Jogo adicionado com sucesso!";
                return RedirectToAction(nameof(Index));
            }
            await CarregarConsoles(jogo.txt_Console);
            return View(jogo);
        }

        // GET: /Jogos/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (UsuarioLogado() == null)
                return RedirectToAction("Loguin", "Home");

            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo == null) return NotFound();

            if (!TemPermissao(jogo))
            {
                TempData["Erro"] = "Você não tem permissão para editar este jogo.";
                return RedirectToAction(nameof(Index));
            }

            await CarregarConsoles(jogo.txt_Console);
            return View(jogo);
        }

        // POST: /Jogos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Jogo jogo)
        {
            if (id != jogo.int_Id) return NotFound();

            var jogoOriginal = await _context.Jogos.AsNoTracking().FirstOrDefaultAsync(j => j.int_Id == id);
            if (jogoOriginal == null) return NotFound();

            if (!TemPermissao(jogoOriginal))
            {
                TempData["Erro"] = "Você não tem permissão para editar este jogo.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                jogo.txt_Dono = jogoOriginal.txt_Dono;
                _context.Jogos.Update(jogo);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Jogo atualizado com sucesso!";
                return RedirectToAction(nameof(Index));
            }
            await CarregarConsoles(jogo.txt_Console);
            return View(jogo);
        }

        // POST: /Jogos/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var jogo = await _context.Jogos.FindAsync(id);
            if (jogo == null) return RedirectToAction(nameof(Index));

            if (!TemPermissao(jogo))
            {
                TempData["Erro"] = "Você não tem permissão para remover este jogo.";
                return RedirectToAction(nameof(Index));
            }

            _context.Jogos.Remove(jogo);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Jogo removido com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Avaliar(int jogoId, int estrelas, string comentario)
        {
            var usuario = HttpContext.Session.GetString("UsuarioNome");
            if (usuario == null) return RedirectToAction("Login", "Home");

            var anterior = await _context.Avaliacoes
                .FirstOrDefaultAsync(a => a.int_JogoId == jogoId && a.str_UsuarioNome == usuario);
            if (anterior != null) _context.Avaliacoes.Remove(anterior);

            _context.Avaliacoes.Add(new Avaliacao
            {
                int_JogoId = jogoId,
                str_UsuarioNome = usuario,
                int_Estrelas = estrelas,
                txt_Comentario = comentario,
                dat_Data = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }
    }
}