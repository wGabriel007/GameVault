using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Biblioteca_de_Jogos.Models
{
    [Table("Jogos")]
    public class Jogo
    {
        [Key]
        [Column("Id")]
        public int int_Id { get; set; }

        [Required(ErrorMessage = "O nome do jogo é obrigatório")]
        [StringLength(100)]
        [Column("Nome")]
        public string txt_Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O gênero é obrigatório")]
        [StringLength(50)]
        [Column("Genero")]
        public string txt_Genero { get; set; } = string.Empty;

        [Required(ErrorMessage = "As horas para zerar são obrigatórias")]
        [Range(0, 9999)]
        [Column("HorasParaZerar")]
        public int int_HorasParaZerar { get; set; }

        [StringLength(500)]
        [Column("FotoUrl")]
        public string? txt_FotoUrl { get; set; }

        [Column("EstaEmprestado")]
        public bool bool_EstaEmprestado { get; set; }

        [Column("EmprestadoPara")]
        public string? str_EmprestadoPara { get; set; }

        [Required(ErrorMessage = "O console é obrigatório")]
        [Column("Console")]
        public string txt_Console { get; set; } = string.Empty;

        [Column("Dono")]
        public string txt_Dono { get; set; } = string.Empty;
        [Column("Disponivel")]
        public bool bool_Disponivel { get; set; } = true;

        [Column("AdicionadoEm")]
        public DateTime dt_AdicionadoEm {  get; set; } = DateTime.UtcNow;
    }
}