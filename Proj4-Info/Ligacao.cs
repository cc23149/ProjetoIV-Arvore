using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Proj4
{
    public class Ligacao : IComparable<Ligacao>
    {
        string origem, destino;
        int distancia;

        public string Origem { get => origem; set => origem = value; }
        public string Destino { get => destino; set => destino = value; }
        public int Distancia { get => distancia; set => distancia = value; }

        public Ligacao()
        {
            origem = destino = "";
            distancia = 0;
        }

        public Ligacao(string origem, string destino, int distancia)
        {
            this.origem = origem;
            this.destino = destino;
            this.distancia = distancia;
        }

        public int CompareTo(Ligacao outra)
        {
            return (origem + destino).CompareTo(outra.origem + outra.destino);
        }

        public override string ToString()
        {
            return origem + " - " + destino + ": " + distancia + " km";
        }
    }
}

