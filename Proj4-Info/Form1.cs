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

        // estado para inserção via mapa
        private bool aguardandoCliqueNoMapa = false;
        private Cidade cidadePendente = null;

        // seleção / rota
        private string cidadeSelecionada = null;
        private ListaSimples<Ligacao> rotaEncontrada = null;

        // configuração visual
        private const int RAIO_PONTO = 6; // pixel
        private const int HIT_DIST = 8;   // tolerance in pixels for selecting a city

        public Form1()
        {
            InitializeComponent();

            // garante que eventos do picturebox estejam ligados (se não estiverem no designer)
            this.pbMapa.Paint += PbMapa_Paint;
            this.pbMapa.MouseClick += PbMapa_MouseClick;
        }

        private void tpCadastro_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

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

        private void pnlArvore_Paint(object sender, PaintEventArgs e)
        {
            arvore.Desenhar(pnlArvore);
        }

        // ----------------- MAPA: desenho e clique -----------------

        private void PbMapa_Paint(object sender, PaintEventArgs e)
        {
            DrawMap(e.Graphics);
        }

        private void PbMapa_MouseClick(object sender, MouseEventArgs e)
        {
            // se estamos aguardando um clique para inserir a cidade pendente
            if (aguardandoCliqueNoMapa && cidadePendente != null)
            {
                // calcula coordenadas proporcionais 0..1
                double nx = Math.Round((double)e.X / (double)pbMapa.Width, 4);
                double ny = Math.Round((double)e.Y / (double)pbMapa.Height, 4);

                cidadePendente.X = nx;
                cidadePendente.Y = ny;

                // tenta incluir respeitando a lógica da arvore
                bool inseriu = arvore.IncluirNovoDado(cidadePendente);
                if (!inseriu)
                {
                    MessageBox.Show("Cidade já existente na árvore!", "Aviso");
                }
                else
                {
                    // atualização visual / combos
                    cbxCidadeDestino.Items.Clear();
                    PreencherComboCidades(arvore.Raiz);
                    pnlArvore.Refresh();
                }

                // reset estado
                aguardandoCliqueNoMapa = false;
                cidadePendente = null;
                this.Cursor = Cursors.Default;
                pbMapa.Invalidate();
                return;
            }

            // caso contrário, trata clique de seleção de cidade
            Point clique = new Point(e.X, e.Y);
            string encontrada = EncontrarCidadePorClique(clique);
            if (encontrada != null)
            {
                // seleciona cidade
                cidadeSelecionada = encontrada;
                if (arvore.Existe(new Cidade(encontrada)))
                {
                    Cidade obj = arvore.Atual.Info;
                    txtNomeCidade.Text = obj.Nome.Trim();
                    udX.Value = (decimal)obj.X;
                    udY.Value = (decimal)obj.Y;
                    AtualizarGridLigacoes(obj);
                }
                pbMapa.Invalidate();
            }
        }

        private void DrawMap(Graphics g)
        {
            // fundo (imagem do PictureBox já está setada pelo designer)
            // desenha ligações em vermelho, rota em azul, pontos em preto/verde

            // anti-alias
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // pega todas as cidades
            List<Cidade> cidades = new List<Cidade>();
            ColetarCidades(arvore.Raiz, cidades);

            // primeiro: desenha ligações (vermelho)
            Pen penLig = new Pen(Color.Red, 2);
            foreach (Cidade c in cidades)
            {
                NoLista<Ligacao> atual = c.Ligacoes.Primeiro;
                while (atual != null)
                {
                    Cidade dest = FindCidadeByName(atual.Info.Destino);
                    if (dest != null)
                    {
                        Point p1 = ToPixel(c.X, c.Y);
                        Point p2 = ToPixel(dest.X, dest.Y);
                        g.DrawLine(penLig, p1, p2);
                    }
                    atual = atual.Prox;
                }
            }

            // se houver rotaEncontrada, desenha por cima em azul mais grosso
            if (rotaEncontrada != null && rotaEncontrada.Primeiro != null)
            {
                Pen penRota = new Pen(Color.Blue, 3);
                NoLista<Ligacao> r = rotaEncontrada.Primeiro;
                while (r != null)
                {
                    Cidade origem = FindCidadeByName(r.Info.Origem);
                    Cidade dest = FindCidadeByName(r.Info.Destino);
                    if (origem != null && dest != null)
                    {
                        Point p1 = ToPixel(origem.X, origem.Y);
                        Point p2 = ToPixel(dest.X, dest.Y);
                        g.DrawLine(penRota, p1, p2);
                    }
                    r = r.Prox;
                }
            }

            // desenha pontos (círculos) e nomes
            foreach (Cidade c in cidades)
            {
                Point p = ToPixel(c.X, c.Y);
                Rectangle rect = new Rectangle(p.X - RAIO_PONTO, p.Y - RAIO_PONTO, RAIO_PONTO * 2, RAIO_PONTO * 2);

                // cor do ponto: verde se selecionada
                Brush b = Brushes.Black;
                if (cidadeSelecionada != null && string.Equals(c.Nome.Trim(), cidadeSelecionada.Trim(), StringComparison.OrdinalIgnoreCase))
                    b = Brushes.LimeGreen;

                g.FillEllipse(b, rect);
                g.DrawEllipse(Pens.Black, rect);

                // nome (pequeno)
                g.DrawString(c.Nome.Trim(), this.Font, Brushes.Black, new PointF(p.X + RAIO_PONTO + 2, p.Y - RAIO_PONTO - 2));
            }
        }

        private Point ToPixel(double nx, double ny)
        {
            int x = (int)Math.Round(nx * pbMapa.Width);
            int y = (int)Math.Round(ny * pbMapa.Height);
            return new Point(x, y);
        }

        private void ColetarCidades(NoArvore<Cidade> no, List<Cidade> lista)
        {
            if (no == null)
                return;
            ColetarCidades(no.Esq, lista);
            lista.Add(no.Info);
            ColetarCidades(no.Dir, lista);
        }

        private Cidade FindCidadeByName(string nome)
        {
            if (string.IsNullOrEmpty(nome))
                return null;
            Cidade tmp = new Cidade(nome.Trim());
            if (arvore.Existe(tmp))
                return arvore.Atual.Info;
            return null;
        }

        private string EncontrarCidadePorClique(Point p)
        {
            // percorre cidades e verifica proximidade do clique
            List<Cidade> cidades = new List<Cidade>();
            ColetarCidades(arvore.Raiz, cidades);

            for (int i = 0; i < cidades.Count; i++)
            {
                Point cp = ToPixel(cidades[i].X, cidades[i].Y);
                double dx = cp.X - p.X;
                double dy = cp.Y - p.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist <= HIT_DIST)
                    return cidades[i].Nome.Trim();
            }
            return null;
        }

        // ----------------- Botões e operações já existentes (mantidos) -----------------

        private void btnIncluirCidade_Click(object sender, EventArgs e)
        {
            try
            {
                string nome = txtNomeCidade.Text.Trim();
                if (string.IsNullOrEmpty(nome))
                {
                    MessageBox.Show("Digite o nome da cidade antes de clicar Incluir!", "Aviso");
                    return;
                }

                // coloca em modo aguardando clique no mapa
                cidadePendente = new Cidade(nome);
                aguardandoCliqueNoMapa = true;
                this.Cursor = Cursors.Cross;
                MessageBox.Show("Clique no mapa para posicionar a cidade.", "Aguardando clique");
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

                    // seleciona no mapa
                    cidadeSelecionada = procurada.Nome.Trim();
                    pbMapa.Invalidate();

                    MessageBox.Show("Cidade encontrada!", "Busca");
                }
                else
                {
                    MessageBox.Show("Cidade não encontrada!", "Aviso");
                    dgvLigacoes.Rows.Clear(); // limpa a tabela caso não ache
                    cidadeSelecionada = null;
                    pbMapa.Invalidate();
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

                        // Atualiza o ComboBox
                        cbxCidadeDestino.Items.Clear();
                        PreencherComboCidades(arvore.Raiz);

                        cidadeSelecionada = null;
                        pbMapa.Invalidate();
                    }
                    else
                    {
                        DialogResult dr = MessageBox.Show(
                            "A cidade possui ligações. Deseja remover todas as ligações e excluir a cidade?",
                            "Confirmar exclusão",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (dr == DialogResult.Yes)
                        {
                            // remove ligações ida e volta
                            NoLista<Ligacao> atual = aExcluir.Ligacoes.Primeiro;
                            while (atual != null)
                            {
                                string destino = atual.Info.Destino.Trim();

                                // remove volta na cidade destino (se existir)
                                Cidade cidadeDestTmp = new Cidade(destino);
                                if (arvore.Existe(cidadeDestTmp))
                                {
                                    Cidade cd = arvore.Atual.Info;
                                    cd.Ligacoes.RemoverDado(new Ligacao(destino, aExcluir.Nome.Trim(), 0));
                                }

                                atual = atual.Prox;
                            }

                            // limpa ligações removendo cada uma individualmente
                            while (!aExcluir.Ligacoes.EstaVazia)
                            {
                                NoLista<Ligacao> lig = aExcluir.Ligacoes.Primeiro;
                                string destino = lig.Info.Destino.Trim();

                                // remove ida
                                aExcluir.Ligacoes.RemoverDado(lig.Info);

                                // remove volta no destino
                                Cidade tmp = new Cidade(destino);
                                if (arvore.Existe(tmp))
                                {
                                    Cidade cd = arvore.Atual.Info;
                                    cd.Ligacoes.RemoverDado(new Ligacao(destino, aExcluir.Nome.Trim(), 0));
                                }
                            }
                            arvore.Excluir(aExcluir);

                            MessageBox.Show("Cidade e suas ligações removidas.", "Exclusão");
                            pnlArvore.Refresh();
                            cbxCidadeDestino.Items.Clear();
                            PreencherComboCidades(arvore.Raiz);
                            cidadeSelecionada = null;
                            pbMapa.Invalidate();
                        }
                    }
                }
                else
                    MessageBox.Show("Cidade não encontrada!", "Aviso");
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro ao excluir cidade:\n" + erro.Message, "Erro");
            }
        }

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

                Cidade origem = new Cidade(nomeOrigem);
                Cidade destino = new Cidade(nomeDestino);

                if (!arvore.Existe(origem) || !arvore.Existe(destino))
                {
                    MessageBox.Show("Uma das cidades não foi encontrada!", "Aviso");
                    return;
                }

                arvore.Existe(origem);
                origem = arvore.Atual.Info;
                arvore.Existe(destino);
                destino = arvore.Atual.Info;

                // limpa rota anterior
                rotaEncontrada = new ListaSimples<Ligacao>();

                // ===== ESTRUTURAS DA BUSCA =====
                FilaLista<Cidade> fila = new FilaLista<Cidade>();
                ListaSimples<Cidade> visitados = new ListaSimples<Cidade>();
                ListaSimples<Ligacao> caminho = new ListaSimples<Ligacao>();

                fila.Enfileirar(origem);
                visitados.InserirAposFim(origem);

                bool achou = false;

                // ===== BUSCA EM LARGURA =====
                while (!fila.EstaVazia && !achou)
                {
                    Cidade atual = fila.Retirar();

                    NoLista<Ligacao> lig = atual.Ligacoes.Primeiro;
                    while (lig != null)
                    {
                        string nomeProx = lig.Info.Destino.Trim();

                        // ignora auto-ligações (Campinas->Campinas)
                        if (nomeProx.Equals(atual.Nome.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            lig = lig.Prox;
                            continue;
                        }

                        Cidade proxima = new Cidade(nomeProx);

                        if (!visitados.ExisteDado(proxima))
                        {
                            if (arvore.Existe(proxima))
                            {
                                proxima = arvore.Atual.Info;

                                fila.Enfileirar(proxima);
                                visitados.InserirAposFim(proxima);
                                caminho.InserirAposFim(new Ligacao(atual.Nome, proxima.Nome, lig.Info.Distancia));

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

                // ===== RESULTADOS =====
                dgvRotas.Rows.Clear();
                int total = 0;

                if (achou)
                {
                    // exibe o caminho guardado em "caminho"
                    NoLista<Ligacao> atual = caminho.Primeiro;
                    while (atual != null)
                    {
                        dgvRotas.Rows.Add(atual.Info.Destino.Trim(), atual.Info.Distancia);
                        total += atual.Info.Distancia;

                        // também copia para rotaEncontrada para desenhar no mapa
                        rotaEncontrada.InserirEmOrdem(new Ligacao(atual.Info.Origem, atual.Info.Destino, atual.Info.Distancia));

                        atual = atual.Prox;
                    }

                    lbDistanciaTotal.Text = "Distância total: " + total + " km";
                    pbMapa.Invalidate();
                }
                else
                {
                    MessageBox.Show("Não há caminho entre essas cidades!", "Busca");
                    lbDistanciaTotal.Text = "Distância total: 0 km";
                    rotaEncontrada = null;
                    pbMapa.Invalidate();
                }
            }
            catch (Exception erro)
            {
                MessageBox.Show("Erro na busca de caminhos:\n" + erro.Message, "Erro");
            }
        }

        private void btnIncluirCaminho_Click(object sender, EventArgs e)
        {
            try
            {
                string nomeOrigem = txtNomeCidade.Text.Trim();
                string nomeDestino = txtNovoDestino.Text.Trim();
                int distancia = (int)numericUpDown1.Value;

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

                // Atualiza visualmente as ligações da cidade de origem
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
    }
}
