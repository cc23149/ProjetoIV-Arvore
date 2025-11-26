using AgendaAlfabetica;
using System;
using System.IO;
using System.Windows.Forms;

namespace Proj4
{
    public class Cidade : IComparable<Cidade>, IRegistro
    {
        string nome;
        double x, y;
        ListaSimples<Ligacao> ligacoes = new ListaSimples<Ligacao>();

        const int tamanhoNome = 25;
        const int tamanhoRegistro = tamanhoNome + (2 * sizeof(double));

        public string Nome
        {
            get => nome;
            set => nome = value.PadRight(tamanhoNome, ' ').Substring(0, tamanhoNome);
        }

        public Cidade(string nome, double x, double y)
        {
            this.Nome = nome;
            this.x = x;
            this.y = y;
        }
        public override string ToString()
        {
            return Nome.TrimEnd() + " (" + ligacoes.QuantosNos + ")";
        }

        public Cidade()
        {
            this.Nome = "";
            this.x = 0;
            this.y = 0;
        }

        public Cidade(string nome)
        {
            this.Nome = nome;
        }

        public int CompareTo(Cidade outraCid)
        {
            return Nome.CompareTo(outraCid.Nome);
        }

        public int TamanhoRegistro { get => tamanhoRegistro; }
        public double X { get => x; set => x = value; }
        public double Y { get => y; set => y = value; }

        // propriedade pública para acessar a lista de ligações
        public ListaSimples<Ligacao> Ligacoes { get => ligacoes; }

        public void LerRegistro(BinaryReader arquivo, long qualRegistro)
        {
            arquivo.BaseStream.Seek(qualRegistro * TamanhoRegistro, SeekOrigin.Begin);
            char[] nomeLido = arquivo.ReadChars(tamanhoNome);
            Nome = new string(nomeLido);
            x = arquivo.ReadDouble();
            y = arquivo.ReadDouble();
        }

        public void GravarRegistro(BinaryWriter arquivo)
        {
            arquivo.Write(Nome.ToCharArray());
            arquivo.Write(x);
            arquivo.Write(y);
        }




        public static string NormalizarNome(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                return "";

            nome = nome.Trim();

            // Remove acentos
            string form = nome.Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (char ch in form)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            nome = sb.ToString().Normalize(System.Text.NormalizationForm.FormC);

            // Tudo minúsculo
            nome = nome.ToLowerInvariant();

            // Title Case (primeira letra maiúscula)
            string[] partes = nome.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < partes.Length; i++)
            {
                if (partes[i].Length > 0)
                    partes[i] = char.ToUpper(partes[i][0]) + partes[i].Substring(1);
            }

            return string.Join(" ", partes);
        }

    }

}
