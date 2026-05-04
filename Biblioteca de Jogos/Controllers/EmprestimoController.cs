using Biblioteca_de_Jogos.Data;
using Biblioteca_de_Jogos.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Biblioteca_de_Jogos.Controllers
{
    public class EmprestimoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EmprestimoController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? UsuarioLogado() =>
            HttpContext.Session.GetString("UsuarioNome");

        // POST: Solicitar empréstimo de um jogo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Solicitar(int jogoId)
        {
            var solicitante = UsuarioLogado();
            if (solicitante == null)
                return RedirectToAction("Loguin", "Home");

            var jogo = await _context.Jogos.FindAsync(jogoId);
            if (jogo == null || jogo.EstaEmprestado || jogo.Dono == solicitante)
            {
                TempData["Erro"] = "Solicitação inválida.";
                return RedirectToAction("Index", "Jogos");
            }

            // Verifica se já existe solicitação pendente
            var jaExiste = await _context.Solicitacoes.AnyAsync(s =>
                s.JogoId == jogoId &&
                s.SolicitanteNome == solicitante &&
                s.Status == StatusSolicitacao.Pendente);

            if (jaExiste)
            {
                TempData["Erro"] = "Você já tem uma solicitação pendente para este jogo.";
                return RedirectToAction("Index", "Jogos");
            }

            var solicitacao = new SolicitacaoEmprestimo
            {
                JogoId          = jogoId,
                SolicitanteNome = solicitante,
                DonoNome        = jogo.Dono,
                Status          = StatusSolicitacao.Pendente,
                DataSolicitacao = DateTime.UtcNow
            };

            _context.Solicitacoes.Add(solicitacao);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Solicitação enviada para {jogo.Dono}!";
            return RedirectToAction("Index", "Jogos");
        }

        // GET: Caixa de notificações do usuário logado
        public async Task<IActionResult> Notificacoes()
        {
            var nomeUsuario = HttpContext.Session.GetString("UsuarioNome");
            if (nomeUsuario == null) return RedirectToAction("Loguin", "Home");

            // Pedidos recebidos nos seus jogos (você é o dono)
            var pedidosRecebidos = await _context.Solicitacoes
                .Include(s => s.Jogo)
                .Where(s => s.DonoNome == nomeUsuario && s.Status == StatusSolicitacao.Pendente)
                .OrderByDescending(s => s.DataSolicitacao)
                .ToListAsync();

            // Respostas dos seus pedidos (você é o solicitante)
            var minhasRespostas = await _context.Solicitacoes
                .Include(s => s.Jogo)
                .Where(s => s.SolicitanteNome == nomeUsuario &&
                            s.Status != StatusSolicitacao.Pendente &&
                            !s.Visualizada)
                .ToListAsync();

            ViewBag.PedidosRecebidos = pedidosRecebidos;
            ViewBag.MinhasRespostas  = minhasRespostas;

            return View();
        }

        // POST: Aceitar solicitação
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Aceitar(int id)
        {
            var usuario = UsuarioLogado();
            var solicitacao = await _context.Solicitacoes
                .Include(s => s.Jogo)
                .FirstOrDefaultAsync(s => s.Id == id && s.DonoNome == usuario);

            if (solicitacao == null)
            {
                TempData["Erro"] = "Solicitação não encontrada.";
                return RedirectToAction("Notificacoes");
            }

            // Atualiza o jogo como emprestado
            solicitacao.Jogo!.EstaEmprestado  = true;
            solicitacao.Jogo!.EmprestadoPara  = solicitacao.SolicitanteNome;
            solicitacao.Status                = StatusSolicitacao.Aceito;

            // Rejeita demais solicitações pendentes para o mesmo jogo
            var outrasSolicitacoes = await _context.Solicitacoes
                .Where(s => s.JogoId == solicitacao.JogoId &&
                            s.Status == StatusSolicitacao.Pendente &&
                            s.Id != id)
                .ToListAsync();

            foreach (var outra in outrasSolicitacoes)
                outra.Status = StatusSolicitacao.Rejeitado;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Empréstimo de '{solicitacao.Jogo.Nome}' aceito para {solicitacao.SolicitanteNome}!";
            return RedirectToAction("Notificacoes");
        }

        // POST: Rejeitar solicitação
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rejeitar(int id)
        {
            var usuario    = UsuarioLogado();
            var solicitacao = await _context.Solicitacoes
                .Include(s => s.Jogo)
                .FirstOrDefaultAsync(s => s.Id == id && s.DonoNome == usuario);

            if (solicitacao == null)
            {
                TempData["Erro"] = "Solicitação não encontrada.";
                return RedirectToAction("Notificacoes");
            }

            solicitacao.Status = StatusSolicitacao.Rejeitado;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Solicitação de '{solicitacao.Jogo!.Nome}' rejeitada.";
            return RedirectToAction("Notificacoes");
        }

        // POST: Devolver jogo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Devolver(int jogoId)
        {
            var usuario = UsuarioLogado();
            var jogo = await _context.Jogos.FindAsync(jogoId);

            if (jogo == null || jogo.Dono != usuario)
            {
                TempData["Erro"] = "Ação inválida.";
                return RedirectToAction("Index", "Jogos");
            }

            jogo.EstaEmprestado = false;
            jogo.EmprestadoPara = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"'{jogo.Nome}' marcado como devolvido!";
            return RedirectToAction("Index", "Jogos");
        }

        [HttpGet]
        public async Task<IActionResult> MinhasSolicitacoes()
        {
            var nomeUsuario = HttpContext.Session.GetString("UsuarioNome");
            if (nomeUsuario == null) return RedirectToAction("Loguin", "Home");

            var solicitacoes = await _context.Solicitacoes
                .Include(s => s.Jogo)
                .Where(s => s.SolicitanteNome == nomeUsuario &&
                            s.Status != StatusSolicitacao.Pendente)
                .OrderByDescending(s => s.DataSolicitacao)
                .ToListAsync();

            return View(solicitacoes);
        }

        // POST: Marcar solicitação como visualizada
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarVisualizada(int id)
        {
            var solicitacao = await _context.Solicitacoes.FindAsync(id);
            if (solicitacao != null)
            {
                solicitacao.Visualizada = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Notificacoes");
        }
    }
}
