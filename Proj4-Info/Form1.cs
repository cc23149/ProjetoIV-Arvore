//Matheus Ferreira Fagundes - 23149
//Yasmin Victoria Lopes da Silva - 23581

using AgendaAlfabetica;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Proj4
{
    public partial class Form1 : Form
    {
        Arvore<Cidade> arvore = new Arvore<Cidade>();

        // cidade selecionada por clique / para arrastar
        private Cidade cidadeSelecionada = null;
        private bool arrastando = false;
        private Point pontoArrasteOffset;

        // caminho resultado da busca (lista de ligações em ordem)
        private ListaSimples<Ligacao> caminhoAtual = new ListaSimples<Ligacao>();

        // raio em pixels para detectar clique próximo a um nó
        private const int RAIO_NO = 8;

        public Form1()
        {
            InitializeComponent();

            // eventos do mapa
            this.pbMapa.Paint += PbMapa_Paint;
            this.pbMapa.MouseDown += PbMapa_MouseDown;
            this.pbMapa.MouseMove += PbMapa_MouseMove;
            this.pbMapa.MouseUp += PbMapa_MouseUp;
            this.pbMapa.MouseClick += PbMapa_MouseClick;
        }

        #region Load / Closing (arquivos)

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                // ====== Localização dos arquivos ======
                string pastaBase = Application.StartupPath;
                DirectoryInfo dir = new DirectoryInfo(pastaBase);
                string pastaProjeto = dir.Parent.Parent.FullName; // sobe duas pastas
                string pastaDados = Path.Combine(pastaProjeto, "Dados");

                string arqCidades = Path.Combine(pastaDados, "cidades.dat");
                string arqCaminhos = Path.Combine(pastaDados, "GrafoOnibusSaoPaulo.txt");

                // ====== Leitura das cidades ======
                if (File.Exists(arqCidades))
                {
                    arvore.LerArquivoDeRegistros(arqCidades);
                    MessageBox.Show("Cidades carregadas com sucesso!", "Leitura de Arquivo");
                }
                else
                {
                    MessageBox.Show("Arquivo de cidades não encontrado!", "Aviso");
                }

                // ====== Leitura dos caminhos (somente entre cidades existentes) ======
                if (File.Exists(arqCaminhos))
                {
                    using (StreamReader leitor = new StreamReader(arqCaminhos))
                    {
                        string linha;
                        while ((linha = leitor.ReadLine()) != null)
                        {
                            string[] partes = linha.Split(';');
                            if (partes.Length < 3)
                                continue;

                            string nomeOrigem = partes[0].Trim();
                            string nomeDestino = partes[1].Trim();

                            // ignora auto-ligações
                            if (string.Equals(nomeOrigem, nomeDestino, StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (!int.TryParse(partes[2].Trim(), out int distancia))
                                continue;

                            // cria objetos temporários para busca
                            Cidade procuraOrigem = new Cidade(nomeOrigem);
                            Cidade procuraDestino = new Cidade(nomeDestino);

                            // só adiciona se as duas cidades existirem na árvore
                            if (arvore.Existe(procuraOrigem) && arvore.Existe(procuraDestino))
                            {
                                // garante que Atual esteja posicionado na origem e destino
                                arvore.Existe(procuraOrigem);
                                Cidade cidadeOrigem = arvore.Atual.Info;

                                arvore.Existe(procuraDestino);
                                Cidade cidadeDestino = arvore.Atual.Info;

                                // cria ligações
                                Ligacao ida = new Ligacao(nomeOrigem, nomeDestino, distancia);
                                Ligacao volta = new Ligacao(nomeDestino, nomeOrigem, distancia);

                                // evita duplicatas
                                if (!cidadeOrigem.Ligacoes.ExisteDado(ida))
                                    cidadeOrigem.Ligacoes.InserirEmOrdem(ida);

                                if (!cidadeDestino.Ligacoes.ExisteDado(volta))
                                    cidadeDestino.Ligacoes.InserirEmOrdem(volta);
                            }
                        }
                    }

                    MessageBox.Show("Caminhos carregados com sucesso!", "Leitura de Arquivo");
                }
                else
                {
                    MessageBox.Show("Arquivo de caminhos não encontrado!", "Aviso");
                }

                // ====== Atualiza interface ======
                pnlArvore.Refresh();
                cbxCidadeDestino.Items.Clear();
                PreencherComboCidades(arvore.Raiz);
                pbMapa.Invalidate(); // desenha mapa com nós/arestas
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro ao carregar os arquivos:\n" + erro.Message,
                                "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            string pastaBase = Application.StartupPath;
            DirectoryInfo dir = new DirectoryInfo(pastaBase);
            string pastaProjeto = dir.Parent.Parent.FullName;
            string pastaDados = Path.Combine(pastaProjeto, "Dados");
            string arqCidades = Path.Combine(pastaDados, "cidades.dat");

            try
            {
                arvore.GravarArquivoDeRegistros(arqCidades);
                MessageBox.Show("Alterações salvas com sucesso!", "Gravação de Arquivo");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao salvar as cidades: " + ex.Message, "Erro");
            }

            // ====== Grava as ligações ======
            string arqCaminhos = Path.Combine(pastaDados, "GrafoOnibusSaoPaulo.txt");
            using (StreamWriter gravador = new StreamWriter(arqCaminhos))
            {
                GravarLigacoes(arvore.Raiz, gravador);
            }
        }

        #endregion

        #region Desenho do mapa / nós / arestas / rota destacada

        private void PbMapa_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            // desenha arestas vermelhas
            DesenharArestas(g);
            // desenha rota atual (se houver) em azul e mais grossa
            DesenharRotaAtual(g);
            // desenha nós (cidades) por cima
            DesenharNos(g);
        }

        private void DesenharArestas(Graphics g)
        {
            Pen pen = new Pen(Color.FromArgb(180, 150, 0), 1f); // cor suave (vermelho/laranja)
            NoArvore<Cidade> no = arvore.Raiz;
            DesenharArestasRec(no, g, pen);
            pen.Dispose();
        }

        private void DesenharArestasRec(NoArvore<Cidade> no, Graphics g, Pen pen)
        {
            if (no == null) return;

            DesenharArestasRec(no.Esq, g, pen);

            // percorre ligações da cidade atual
            NoLista<Ligacao> atual = no.Info.Ligacoes.Primeiro;
            while (atual != null)
            {
                Cidade destino = new Cidade(atual.Info.Destino);
                if (arvore.Existe(destino))
                {
                    Cidade cOrig = no.Info;
                    Cidade cDest = arvore.Atual.Info;

                    Point p1 = ProporcionalParaPixel(cOrig);
                    Point p2 = ProporcionalParaPixel(cDest);

                    g.DrawLine(pen, p1, p2);
                }
                atual = atual.Prox;
            }

            DesenharArestasRec(no.Dir, g, pen);
        }

        private void DesenharRotaAtual(Graphics g)
        {
            if (caminhoAtual == null) return;

            NoLista<Ligacao> atual = caminhoAtual.Primeiro;
            if (atual == null) return;

            Pen pen = new Pen(Color.Blue, 3f);
            while (atual != null)
            {
                Cidade cOrig = new Cidade(atual.Info.Destino); // note: guardamos destino em cada Ligacao inserida no caminho
                // Para desenhar corretamente precisamos desenhar entre origem e destino.
                // Caminho foi construído como sequencia de ligações (origem -> proximo), então cada lig.Info tem Destino e Distancia.
                // Aqui vamos buscar a cidade anterior usando a lista: para simplificar, procuramos no arvore a cidade com lig.Info.Destino
                // e também a próxima no caminho. Em nosso armazenamento o caminho contém apenas destinos em ordem,
                // então desenharemos entre cada par consecutivo.

                // Percorrendo próximo para desenhar par a par
                atual = atual.Prox;
            }

            // alternativa: reconstruir rota em lista de nomes e desenhar entre pares
            List<string> nomes = new List<string>();
            atual = caminhoAtual.Primeiro;
            while (atual != null)
            {
                nomes.Add(atual.Info.Destino.Trim());
                atual = atual.Prox;
            }

            // se houver ao menos dois pontos, desenha entre pares
            for (int i = 0; i < nomes.Count - 1; i++)
            {
                Cidade a = new Cidade(nomes[i]);
                Cidade b = new Cidade(nomes[i + 1]);

                if (arvore.Existe(a) && arvore.Existe(b))
                {
                    Cidade ca = arvore.Atual.Info;
                    arvore.Existe(b);
                    Cidade cb = arvore.Atual.Info;

                    Point pa = ProporcionalParaPixel(ca);
                    Point pb = ProporcionalParaPixel(cb);
                    g.DrawLine(pen, pa, pb);
                }
            }

            pen.Dispose();
        }

        private void DesenharNos(Graphics g)
        {
            // desenha todos os nós em pré-ordem in-order para que apareçam por cima das arestas
            NoArvore<Cidade> no = arvore.Raiz;
            DesenharNosRec(no, g);
        }

        private void DesenharNosRec(NoArvore<Cidade> no, Graphics g)
        {
            if (no == null) return;

            DesenharNosRec(no.Esq, g);

            Point p = ProporcionalParaPixel(no.Info);
            Rectangle r = new Rectangle(p.X - RAIO_NO, p.Y - RAIO_NO, RAIO_NO * 2, RAIO_NO * 2);

            Brush b = Brushes.Red;
            Pen penNo = Pens.Black;

            // se a cidade está selecionada, highlight
            if (cidadeSelecionada != null && string.Equals(cidadeSelecionada.Nome.Trim(), no.Info.Nome.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                b = Brushes.Orange;
                penNo = Pens.Blue;
            }

            g.FillEllipse(b, r);
            g.DrawEllipse(penNo, r);

            // desenha nome pequeno ao lado
            string nome = no.Info.Nome.Trim();
            Font f = new Font("Arial", 8);
            SizeF tamanho = g.MeasureString(nome, f);
            g.DrawString(nome, f, Brushes.Black, p.X + RAIO_NO + 2, p.Y - (tamanho.Height / 2));
            f.Dispose();

            DesenharNosRec(no.Dir, g);
        }

        #endregion

        #region Conversão coordenadas

        private Point ProporcionalParaPixel(Cidade c)
        {
            int px = (int)(pbMapa.ClientSize.Width * c.X);
            int py = (int)(pbMapa.ClientSize.Height * c.Y);
            return new Point(px, py);
        }

        private Point ProporcionalParaPixel(double x, double y)
        {
            int px = (int)(pbMapa.ClientSize.Width * x);
            int py = (int)(pbMapa.ClientSize.Height * y);
            return new Point(px, py);
        }

        private void PixelParaProporcional(Point p, out double x, out double y)
        {
            if (pbMapa.ClientSize.Width == 0 || pbMapa.ClientSize.Height == 0)
            {
                x = 0;
                y = 0;
                return;
            }
            x = (double)p.X / (double)pbMapa.ClientSize.Width;
            y = (double)p.Y / (double)pbMapa.ClientSize.Height;
        }

        #endregion

        #region Interação mapa: clique / arraste / seleção

        private void PbMapa_MouseClick(object sender, MouseEventArgs e)
        {
            // seleciona cidade próxima ao clique
            Cidade encontrada = EncontrarCidadeProxima(e.Location, RAIO_NO + 4);
            if (encontrada != null)
            {
                cidadeSelecionada = encontrada;
                txtNomeCidade.Text = cidadeSelecionada.Nome.Trim();
                udX.Value = (decimal)cidadeSelecionada.X;
                udY.Value = (decimal)cidadeSelecionada.Y;
                AtualizarGridLigacoes(cidadeSelecionada);
                pbMapa.Invalidate();
            }
            else
            {
                // se clicar fora, desmarca
                cidadeSelecionada = null;
                pbMapa.Invalidate();
            }
        }

        private void PbMapa_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            Cidade encontrada = EncontrarCidadeProxima(e.Location, RAIO_NO + 4);
            if (encontrada != null)
            {
                cidadeSelecionada = encontrada;
                arrastando = true;

                // calcula offset (para manter cursor relativo)
                Point pCidade = ProporcionalParaPixel(cidadeSelecionada);
                pontoArrasteOffset = new Point(e.X - pCidade.X, e.Y - pCidade.Y);

                // atualiza controles
                txtNomeCidade.Text = cidadeSelecionada.Nome.Trim();
                udX.Value = (decimal)cidadeSelecionada.X;
                udY.Value = (decimal)cidadeSelecionada.Y;
                AtualizarGridLigacoes(cidadeSelecionada);

                pbMapa.Invalidate();
            }
        }

        private void PbMapa_MouseMove(object sender, MouseEventArgs e)
        {
            if (!arrastando || cidadeSelecionada == null) return;

            // nova posição proporcional a partir do mouse (ajustando offset)
            Point pMouse = new Point(e.X - pontoArrasteOffset.X, e.Y - pontoArrasteOffset.Y);
            double nx, ny;
            PixelParaProporcional(pMouse, out nx, out ny);

            // limita entre 0..1
            if (nx < 0) nx = 0;
            if (nx > 1) nx = 1;
            if (ny < 0) ny = 0;
            if (ny > 1) ny = 1;

            // atualiza cidade selecionada e controles
            cidadeSelecionada.X = nx;
            cidadeSelecionada.Y = ny;
            udX.Value = (decimal)nx;
            udY.Value = (decimal)ny;

            pbMapa.Invalidate();
        }

        private void PbMapa_MouseUp(object sender, MouseEventArgs e)
        {
            if (arrastando)
            {
                arrastando = false;
                // se quisermos, podemos salvar imediatamente, mas o fechamento já salva
                // pbMapa.Invalidate();
            }
        }

        private Cidade EncontrarCidadeProxima(Point p, int raio)
        {
            // percorre árvore e procura cidade com distância menor que raio
            Cidade achada = null;
            double melhorDist = double.MaxValue;

            EncontrarCidadeProximaRec(arvore.Raiz, p, ref achada, ref melhorDist, raio);
            return achada;
        }

        private void EncontrarCidadeProximaRec(NoArvore<Cidade> no, Point p, ref Cidade achada, ref double melhorDist, int raio)
        {
            if (no == null) return;

            EncontrarCidadeProximaRec(no.Esq, p, ref achada, ref melhorDist, raio);

            Point pp = ProporcionalParaPixel(no.Info);
            double dx = pp.X - p.X;
            double dy = pp.Y - p.Y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= raio && d < melhorDist)
            {
                melhorDist = d;
                achada = no.Info;
            }

            EncontrarCidadeProximaRec(no.Dir, p, ref achada, ref melhorDist, raio);
        }

        #endregion

        #region Botões: incluir / buscar / alterar / excluir cidades

        private void btnIncluirCidade_Click(object sender, EventArgs e)
        {
            try
            {
                string nome = txtNomeCidade.Text.Trim();
                double x = (double)udX.Value;
                double y = (double)udY.Value;

                Cidade nova = new Cidade(nome, x, y);

                if (arvore.IncluirNovoDado(nova))
                {
                    MessageBox.Show("Cidade incluída com sucesso!", "Inclusão");
                    pnlArvore.Refresh();

                    // Atualiza o ComboBox e o mapa
                    cbxCidadeDestino.Items.Clear();
                    PreencherComboCidades(arvore.Raiz);
                    pbMapa.Invalidate();
                }
                else
                    MessageBox.Show("Cidade já existente na árvore!", "Aviso");
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro ao incluir cidade:\n" + erro.Message, "Erro");
            }
        }

        private void btnBuscarCidade_Click(object sender, EventArgs e)
        {
            try
            {
                string nome = txtNomeCidade.Text.Trim();
                Cidade procurada = new Cidade(nome);

                if (arvore.Existe(procurada))
                {
                    procurada = arvore.Atual.Info;
                    udX.Value = (decimal)procurada.X;
                    udY.Value = (decimal)procurada.Y;

                    // Atualiza a grade de ligações visuais
                    AtualizarGridLigacoes(procurada);

                    // seleciona e desenha
                    cidadeSelecionada = procurada;
                    pbMapa.Invalidate();

                    MessageBox.Show("Cidade encontrada!", "Busca");
                }
                else
                {
                    MessageBox.Show("Cidade não encontrada!", "Aviso");
                    dgvLigacoes.Rows.Clear(); // limpa a tabela caso não ache
                }
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro na busca:\n" + erro.Message, "Erro");
            }
        }

        private void btnAlterarCidade_Click(object sender, EventArgs e)
        {
            try
            {
                string nome = txtNomeCidade.Text.Trim();
                Cidade procurada = new Cidade(nome);

                if (arvore.Existe(procurada))
                {
                    procurada = arvore.Atual.Info;
                    procurada.X = (double)udX.Value;
                    procurada.Y = (double)udY.Value;

                    MessageBox.Show("Dados alterados com sucesso!", "Alteração");
                    pnlArvore.Refresh();

                    // redesenha mapa com nova posição
                    pbMapa.Invalidate();
                }
                else
                    MessageBox.Show("Cidade não encontrada!", "Aviso");
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro ao alterar cidade:\n" + erro.Message, "Erro");
            }
        }

        private void btnExcluirCidade_Click(object sender, EventArgs e)
        {
            try
            {
                string nome = txtNomeCidade.Text.Trim();
                Cidade aExcluir = new Cidade(nome);

                if (arvore.Existe(aExcluir))
                {
                    aExcluir = arvore.Atual.Info;

                    if (aExcluir.Ligacoes.EstaVazia)
                    {
                        arvore.Excluir(aExcluir);
                        MessageBox.Show("Cidade excluída com sucesso!", "Exclusão");
                        pnlArvore.Refresh();

                        // Atualiza o ComboBox e mapa
                        cbxCidadeDestino.Items.Clear();
                        PreencherComboCidades(arvore.Raiz);
                        pbMapa.Invalidate();
                    }
                    else
                        MessageBox.Show("A cidade possui ligações! Exclua as rotas antes.", "Aviso");
                }
                else
                    MessageBox.Show("Cidade não encontrada!", "Aviso");
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro ao excluir cidade:\n" + erro.Message, "Erro");
            }
        }

        #endregion

        #region Botões: incluir / excluir caminho

        private void btnIncluirCaminho_Click(object sender, EventArgs e)
        {
            try
            {
                string nomeOrigem = txtNomeCidade.Text.Trim();
                string nomeDestino = txtNovoDestino.Text.Trim();
                int distancia = (int)numericUpDown1.Value;

                if (string.IsNullOrEmpty(nomeOrigem) || string.IsNullOrEmpty(nomeDestino))
                {
                    MessageBox.Show("Preencha o nome das duas cidades!");
                    return;
                }

                if (nomeOrigem.Equals(nomeDestino, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("As cidades devem ser diferentes!");
                    return;
                }

                // ===== GARANTE QUE AS CIDADES EXISTAM NA ÁRVORE =====
                Cidade origem = new Cidade(nomeOrigem);
                if (!arvore.Existe(origem))
                {
                    arvore.IncluirNovoDado(origem);
                    arvore.Existe(origem); // reposiciona ponteiro Atual
                }
                origem = arvore.Atual.Info;

                Cidade destino = new Cidade(nomeDestino);
                if (!arvore.Existe(destino))
                {
                    arvore.IncluirNovoDado(destino);
                    arvore.Existe(destino);
                }
                destino = arvore.Atual.Info;

                // ===== CRIA LIGAÇÕES BIDIRECIONAIS =====
                Ligacao ida = new Ligacao(nomeOrigem, nomeDestino, distancia);
                Ligacao volta = new Ligacao(nomeDestino, nomeOrigem, distancia);

                origem.Ligacoes.InserirEmOrdem(ida);
                destino.Ligacoes.InserirEmOrdem(volta);

                MessageBox.Show("Caminho incluído com sucesso!", "Inclusão");

                // Atualiza visualmente as ligações da cidade de origem e mapa
                AtualizarGridLigacoes(origem);
                pbMapa.Invalidate();
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro ao incluir caminho:\n" + erro.Message, "Erro");
            }
        }

        private void btnExcluirCaminho_Click(object sender, EventArgs e)
        {
            try
            {
                string nomeOrigem = txtNomeCidade.Text.Trim();
                string nomeDestino = txtNovoDestino.Text.Trim();

                if (nomeOrigem == "" || nomeDestino == "")
                {
                    MessageBox.Show("Preencha o nome das duas cidades!");
                    return;
                }

                if (nomeOrigem.Equals(nomeDestino, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("As cidades devem ser diferentes!");
                    return;
                }

                // ===== GARANTE QUE AS CIDADES EXISTAM =====
                Cidade origem = new Cidade(nomeOrigem);
                if (!arvore.Existe(origem))
                {
                    MessageBox.Show("Cidade de origem não encontrada!", "Aviso");
                    return;
                }
                origem = arvore.Atual.Info;

                Cidade destino = new Cidade(nomeDestino);
                if (!arvore.Existe(destino))
                {
                    MessageBox.Show("Cidade de destino não encontrada!", "Aviso");
                    return;
                }
                destino = arvore.Atual.Info;

                // ===== VERIFICA SE EXISTE A LIGAÇÃO =====
                Ligacao ligacaoIda = new Ligacao(nomeOrigem, nomeDestino, 0);
                Ligacao ligacaoVolta = new Ligacao(nomeDestino, nomeOrigem, 0);

                bool removidaOrigem = origem.Ligacoes.RemoverDado(ligacaoIda);
                bool removidaDestino = destino.Ligacoes.RemoverDado(ligacaoVolta);

                if (removidaOrigem || removidaDestino)
                {
                    MessageBox.Show("Caminho excluído com sucesso!", "Exclusão");
                    AtualizarGridLigacoes(origem);
                    pbMapa.Invalidate();
                }
                else
                {
                    MessageBox.Show("Não existe caminho entre essas cidades!", "Aviso");
                }
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro ao excluir caminho:\n" + erro.Message, "Erro");
            }
        }

        #endregion

        #region Buscar caminhos (BFS estilo professor, sem Dictionary)

        private void btnBuscarCaminho_Click(object sender, EventArgs e)
        {
            try
            {
                string nomeOrigem = txtNomeCidade.Text.Trim();
                string nomeDestino = cbxCidadeDestino.Text.Trim();

                if (string.IsNullOrEmpty(nomeOrigem) || string.IsNullOrEmpty(nomeDestino))
                {
                    MessageBox.Show("Preencha as duas cidades!", "Aviso");
                    return;
                }

                Cidade origemTemp = new Cidade(nomeOrigem);
                Cidade destinoTemp = new Cidade(nomeDestino);

                if (!arvore.Existe(origemTemp) || !arvore.Existe(destinoTemp))
                {
                    MessageBox.Show("Uma das cidades não foi encontrada!", "Aviso");
                    return;
                }

                // posiciona objetos finais
                arvore.Existe(origemTemp);
                Cidade origem = arvore.Atual.Info;
                arvore.Existe(destinoTemp);
                Cidade destino = arvore.Atual.Info;

                // estruturas do professor: fila, visitados, predecessores (em lista simples)
                FilaLista<Cidade> fila = new FilaLista<Cidade>();
                ListaSimples<Cidade> visitados = new ListaSimples<Cidade>();
                // ListaSimples para registrar predecessor: cada nó terá (cidadeAtual, predecessorNome)
                // Como sua ListaSimples armazena apenas um tipo, vamos usar ListaSimples<Ligacao> para guardar arestas
                // Porém para clareza, criaremos uma ListaSimples< string > de pares "cidade|pred"
                ListaSimples<string> predecessores = new ListaSimples<string>();

                fila.Enfileirar(origem);
                visitados.InserirAposFim(origem);
                predecessores.InserirAposFim(origem.Nome.Trim() + "|" + string.Empty); // início

                bool achou = false;

                while (!fila.EstaVazia && !achou)
                {
                    Cidade atual = fila.Retirar();

                    NoLista<Ligacao> lig = atual.Ligacoes.Primeiro;
                    while (lig != null)
                    {
                        string nomeProx = lig.Info.Destino.Trim();

                        // ignora auto-ligações
                        if (nomeProx.Equals(atual.Nome.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            lig = lig.Prox;
                            continue;
                        }

                        Cidade proximaTemp = new Cidade(nomeProx);
                        if (!visitados.ExisteDado(proximaTemp))
                        {
                            if (arvore.Existe(proximaTemp))
                            {
                                Cidade proxima = arvore.Atual.Info;
                                fila.Enfileirar(proxima);
                                visitados.InserirAposFim(proxima);
                                // registra predecessor como "nomeProxima|nomeAtual"
                                predecessores.InserirAposFim(proxima.Nome.Trim() + "|" + atual.Nome.Trim());

                                if (proxima.Nome.Trim().Equals(destino.Nome.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    achou = true;
                                    break;
                                }
                            }
                        }

                        lig = lig.Prox;
                    }
                }

                // resultado: reconstruir caminho a partir de predecessores (ListaSimples<string> onde cada Info é "cidade|pred")
                caminhoAtual = new ListaSimples<Ligacao>(); // limpa
                if (achou)
                {
                    // encontra o predecessor da cidade destino na lista
                    string atualNome = destino.Nome.Trim();
                    ListaSimples<string> reconstruir = new ListaSimples<string>();

                    // adiciona destino primeiro
                    reconstruir.InserirAposFim(atualNome + "|" + string.Empty);

                    // enquanto não chegarmos na raiz (predecessor vazio)
                    bool encontrouPred = true;
                    while (encontrouPred)
                    {
                        encontrouPred = false;
                        NoLista<string> p = predecessores.Primeiro;
                        while (p != null)
                        {
                            string[] partes = p.Info.Split('|');
                            if (partes.Length >= 2)
                            {
                                string cidade = partes[0].Trim();
                                string pred = partes[1].Trim();

                                if (cidade.Equals(atualNome, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!string.IsNullOrEmpty(pred))
                                    {
                                        reconstruir.InserirAposFim(pred + "|" + string.Empty);
                                        atualNome = pred;
                                        encontrouPred = true;
                                    }
                                    break;
                                }
                            }
                            p = p.Prox;
                        }
                    }

                    // reconstruir lista de nomes (reverter reconstruir)
                    List<string> nomesRota = new List<string>();
                    NoLista<string> pr = reconstruir.Primeiro;
                    while (pr != null)
                    {
                        string[] partes = pr.Info.Split('|');
                        if (partes.Length >= 1)
                            nomesRota.Add(partes[0].Trim());
                        pr = pr.Prox;
                    }

                    // nomesRota está do destino para origem; inverter para origem->destino
                    nomesRota.Reverse();

                    // preencher caminhoAtual com ligações entre pares
                    for (int i = 0; i < nomesRota.Count - 1; i++)
                    {
                        string a = nomesRota[i];
                        string b = nomesRota[i + 1];

                        // localizar cidade a
                        Cidade caTemp = new Cidade(a);
                        if (!arvore.Existe(caTemp)) continue;
                        Cidade ca = arvore.Atual.Info;

                        NoLista<Ligacao> la = ca.Ligacoes.Primeiro;
                        while (la != null)
                        {
                            if (la.Info.Destino.Trim().Equals(b, StringComparison.OrdinalIgnoreCase))
                            {
                                caminhoAtual.InserirAposFim(new Ligacao(la.Info.Destino, la.Info.Destino, la.Info.Distancia));
                                break;
                            }
                            la = la.Prox;
                        }
                    }

                    // mostra no grid e desenha rota
                    dgvRotas.Rows.Clear();
                    int total = 0;
                    NoLista<Ligacao> it = caminhoAtual.Primeiro;
                    while (it != null)
                    {
                        dgvRotas.Rows.Add(it.Info.Destino.Trim(), it.Info.Distancia);
                        total += it.Info.Distancia;
                        it = it.Prox;
                    }

                    lbDistanciaTotal.Text = "Distância total: " + total + " km";
                    pbMapa.Invalidate();
                }
                else
                {
                    MessageBox.Show("Não há caminho entre essas cidades!", "Busca");
                    lbDistanciaTotal.Text = "Distância total: 0 km";
                    dgvRotas.Rows.Clear();
                    caminhoAtual = new ListaSimples<Ligacao>();
                    pbMapa.Invalidate();
                }
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro na busca de caminhos:\n" + erro.Message, "Erro");
            }
        }

        #endregion

        #region Helpers existentes (GravarLigacoes, PreencherComboCidades, AtualizarGridLigacoes)

        private void GravarLigacoes(NoArvore<Cidade> no, StreamWriter gravador)
        {
            if (no == null)
                return;

            GravarLigacoes(no.Esq, gravador);

            // grava todas as ligações da cidade
            NoLista<Ligacao> atual = no.Info.Ligacoes.Primeiro;
            while (atual != null)
            {
                gravador.WriteLine(no.Info.Nome.Trim() + ";" +
                                   atual.Info.Destino.Trim() + ";" +
                                   atual.Info.Distancia);
                atual = atual.Prox;
            }

            GravarLigacoes(no.Dir, gravador);
        }

        private void PreencherComboCidades(NoArvore<Cidade> no)
        {
            if (no == null)
                return;

            PreencherComboCidades(no.Esq);

            string nome = no.Info.Nome.Trim();
            if (!cbxCidadeDestino.Items.Contains(nome))
                cbxCidadeDestino.Items.Add(nome);

            PreencherComboCidades(no.Dir);
        }

        private void AtualizarGridLigacoes(Cidade cidade)
        {
            dgvLigacoes.Rows.Clear();

            NoLista<Ligacao> atual = cidade.Ligacoes.Primeiro;
            while (atual != null)
            {
                dgvLigacoes.Rows.Add(atual.Info.Destino, atual.Info.Distancia);
                atual = atual.Prox;
            }
        }

        #endregion



        private void label2_Click(object sender, EventArgs e)
        {
            // deixado vazio propositalmente
        }

        private void tpCadastro_Click(object sender, EventArgs e)
        {
            // deixado vazio propositalmente
        }

        private void pnlArvore_Paint(object sender, PaintEventArgs e)
        {
            // substituído pelo pbMapa_Paint
        }
    }
}
