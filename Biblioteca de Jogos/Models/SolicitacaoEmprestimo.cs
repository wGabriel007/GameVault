using System.ComponentModel.DataAnnotations;

namespace Biblioteca_de_Jogos.Models
{
    public enum StatusSolicitacao
    {
        Pendente,
        Aceito,
        Rejeitado
    }

    public class SolicitacaoEmprestimo
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo? Jogo { get; set; }

        [Required]
        public string SolicitanteNome { get; set; } = string.Empty;

        [Required]
        public string DonoNome { get; set; } = string.Empty;

        public StatusSolicitacao Status { get; set; } = StatusSolicitacao.Pendente;

        public DateTime DataSolicitacao { get; set; } = DateTime.UtcNow;

        public bool Visualizada { get; set; } = false;
    }
}
