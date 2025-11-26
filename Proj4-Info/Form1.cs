//Matheus Ferreira Fagundes - 23149
//Yasmin Victoria Lopes da Silva - 23581

using AgendaAlfabetica;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Globalization;
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
                // ===== Localização dos arquivos ============
                string pastaBase = Application.StartupPath;
                DirectoryInfo dir = new DirectoryInfo(pastaBase);
                string pastaProjeto = dir.Parent.Parent.FullName; // sobe duas pastas
                string pastaDados = Path.Combine(pastaProjeto, "Dados");

                string arqCidades = Path.Combine(pastaDados, "cidades.dat");
                string arqCaminhos = Path.Combine(pastaDados, "GrafoOnibusSaoPaulo.txt");

                // === Leitura das cidades ======
                if (File.Exists(arqCidades))
                {
                    arvore.LerArquivoDeRegistros(arqCidades);
                    MessageBox.Show("Cidades carregadas com sucesso!", "Leitura de Arquivo");
                }
                else
                {
                    MessageBox.Show("Arquivo de cidades não encontrado!", "Aviso");
                }

                // ====== Leitura dos caminhos  ====================
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

                            // normaliza nomes lidos do arquivo (mantendo sem acentos)
                            string nomeOrigem = NormalizarEntrada(partes[0]);
                            string nomeDestino = NormalizarEntrada(partes[1]);

                            // ignora auto-ligações
                            if (string.Equals(nomeOrigem, nomeDestino, StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (!int.TryParse(partes[2].Trim(), out int distancia))
                                continue;

                            Cidade procuraOrigem = new Cidade(nomeOrigem);
                            Cidade procuraDestino = new Cidade(nomeDestino);

                            // só adiciona se as cidades existirem (preserva integridade com cidades.dat)
                            if (arvore.Existe(procuraOrigem) && arvore.Existe(procuraDestino))
                            {
                                arvore.Existe(procuraOrigem);
                                Cidade cidadeOrigem = arvore.Atual.Info;

                                arvore.Existe(procuraDestino);
                                Cidade cidadeDestino = arvore.Atual.Info;

                                // garantimos que as ligações guardem nomes normalizados
                                Ligacao ida = new Ligacao(cidadeOrigem.Nome.Trim(), cidadeDestino.Nome.Trim(), distancia);
                                Ligacao volta = new Ligacao(cidadeDestino.Nome.Trim(), cidadeOrigem.Nome.Trim(), distancia);

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

                // ======== Atualiza interface ===========
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

        // -------------- MAPA -----------------

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
            // desenha ligações em vermelho, rota em azul, pontos em preto/verde

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // pega todas as cidades
            List<Cidade> cidades = new List<Cidade>();
            ColetarCidades(arvore.Raiz, cidades);

            // primeiro desenha ligações (vermelho)
            Pen penLig = new Pen(Color.Red, 1); // mais fino conforme pedido
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

            // se houver rotaEncontrada, desenha por cima em azul (mais marcante)
            if (rotaEncontrada != null && rotaEncontrada.Primeiro != null)
            {
                Pen penRota = new Pen(Color.Blue, 2);
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

            // desenha pontos e os nomes (pontos mais finos)
            foreach (Cidade c in cidades)
            {
                Point p = ToPixel(c.X, c.Y);
                Rectangle rect = new Rectangle(p.X - RAIO_PONTO, p.Y - RAIO_PONTO, RAIO_PONTO * 2, RAIO_PONTO * 2);

                // ponto fica verde se selecionada
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
            // buscamos pelo nome normalizado (remoção de acentos / titlecase)
            string n = NormalizarEntrada(nome);
            Cidade tmp = new Cidade(n);
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

        /// <summary>
        /// Normaliza uma entrada do usuário: remove acentos, trim, e Title Case (primeira letra de cada palavra maiúscula).
        /// Aplicamos essa normalização sempre que o usuário digita nomes e clica nos botões (B).
        /// </summary>
        private string NormalizarEntrada(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return "";

            texto = texto.Trim();

            // remove acentos
            string form = texto.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char ch in form)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            string semAcento = sb.ToString().Normalize(NormalizationForm.FormC);

            // coloca em minúsculas e Title Case por palavra (preserva espaços simples)
            semAcento = semAcento.ToLowerInvariant();
            string[] partes = semAcento.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < partes.Length; i++)
            {
                if (partes[i].Length > 0)
                    partes[i] = char.ToUpper(partes[i][0]) + partes[i].Substring(1);
            }

            return string.Join(" ", partes);
        }

        // ----------------- Botões e operações já existentes (mantidos) -----------------

        private void btnIncluirCidade_Click(object sender, EventArgs e)
        {
            try
            {
                string nome = txtNomeCidade.Text;
                if (string.IsNullOrEmpty(nome))
                {
                    MessageBox.Show("Digite o nome da cidade antes de clicar Incluir!", "Aviso");
                    return;
                }

                // normaliza só no uso (B)
                nome = NormalizarEntrada(nome);

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
                if (string.IsNullOrEmpty(nome))
                {
                    MessageBox.Show("Digite o nome da cidade!", "Aviso");
                    return;
                }

                nome = NormalizarEntrada(nome); // normaliza só no uso (B)
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
                if (string.IsNullOrEmpty(nome))
                {
                    MessageBox.Show("Digite o nome da cidade!", "Aviso");
                    return;
                }

                nome = NormalizarEntrada(nome); // normaliza só no uso (B)
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
                if (string.IsNullOrEmpty(nome))
                {
                    MessageBox.Show("Digite o nome da cidade!", "Aviso");
                    return;
                }

                nome = NormalizarEntrada(nome); // normaliza só no uso (B)
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
                                Cidade cidadeDestTmp = new Cidade(NormalizarEntrada(destino));
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
                                Cidade tmp = new Cidade(NormalizarEntrada(destino));
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

        /// <summary>
        /// Busca o caminho mais curto (menor soma de distâncias) entre origem e destino usando Dijkstra.
        /// Aplica normalização (B) nos nomes que vêm da UI.
        /// </summary>
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

                nomeOrigem = NormalizarEntrada(nomeOrigem); // normaliza só no uso (B)
                nomeDestino = NormalizarEntrada(nomeDestino);

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

                // coleta todas as cidades para o grafo
                List<Cidade> todas = new List<Cidade>();
                ColetarCidades(arvore.Raiz, todas);

                // inicializa distâncias e anteriores (Dijkstra)
                Dictionary<Cidade, double> dist = new Dictionary<Cidade, double>();
                Dictionary<Cidade, Cidade> anterior = new Dictionary<Cidade, Cidade>();
                List<Cidade> naoVisitados = new List<Cidade>();

                foreach (var c in todas)
                {
                    dist[c] = double.PositiveInfinity;
                    anterior[c] = null;
                    naoVisitados.Add(c);
                }

                dist[origem] = 0;

                while (naoVisitados.Count > 0)
                {
                    // seleciona o não visitado com menor distância (simples, sem heap)
                    Cidade u = null;
                    double best = double.PositiveInfinity;
                    foreach (var n in naoVisitados)
                    {
                        if (dist[n] < best)
                        {
                            best = dist[n];
                            u = n;
                        }
                    }

                    if (u == null)
                        break; // nós inacessíveis restantes

                    // se chegamos ao destino, paramos (ótimo)
                    if (u == destino)
                        break;

                    naoVisitados.Remove(u);

                    // relaxa arestas de u
                    NoLista<Ligacao> lig = u.Ligacoes.Primeiro;
                    while (lig != null)
                    {
                        // busca o objeto Cidade correspondente ao destino da ligação
                        string nomeViz = NormalizarEntrada(lig.Info.Destino);
                        Cidade v = FindCidadeByName(nomeViz);

                        if (v != null && naoVisitados.Contains(v))
                        {
                            double alt = dist[u] + lig.Info.Distancia;
                            if (alt < dist[v])
                            {
                                dist[v] = alt;
                                anterior[v] = u;
                            }
                        }

                        lig = lig.Prox;
                    }
                }

                // verifica se destino alcançado
                if (double.IsPositiveInfinity(dist[destino]))
                {
                    MessageBox.Show("Não há caminho entre essas cidades!", "Busca");
                    lbDistanciaTotal.Text = "Distância total: 0 km";
                    rotaEncontrada = null;
                    dgvRotas.Rows.Clear();
                    pbMapa.Invalidate();
                    return;
                }

                // reconstrói caminho do destino para origem
                List<Cidade> caminhoFinal = new List<Cidade>();
                Cidade passo = destino;
                while (passo != null)
                {
                    caminhoFinal.Add(passo);
                    passo = anterior[passo];
                }
                caminhoFinal.Reverse();

                // converte a lista de cidades em lista de ligações (com distâncias corretas)
                dgvRotas.Rows.Clear();
                rotaEncontrada = new ListaSimples<Ligacao>();
                int total = 0;
                for (int i = 0; i < caminhoFinal.Count - 1; i++)
                {
                    Cidade a = caminhoFinal[i];
                    Cidade b = caminhoFinal[i + 1];

                    // procura a ligação a -> b
                    NoLista<Ligacao> aux = a.Ligacoes.Primeiro;
                    Ligacao encontrada = null;
                    while (aux != null)
                    {
                        if (NormalizarEntrada(aux.Info.Destino) == NormalizarEntrada(b.Nome))
                        {
                            encontrada = aux.Info;
                            break;
                        }
                        aux = aux.Prox;
                    }

                    if (encontrada != null)
                    {
                        rotaEncontrada.InserirAposFim(new Ligacao(encontrada.Origem, encontrada.Destino, encontrada.Distancia));
                        dgvRotas.Rows.Add(b.Nome.Trim(), encontrada.Distancia);
                        total += encontrada.Distancia;
                    }
                }

                lbDistanciaTotal.Text = $"Distância total: {total} km";
                pbMapa.Invalidate();
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

                if (string.IsNullOrEmpty(nomeOrigem) || string.IsNullOrEmpty(nomeDestino))
                {
                    MessageBox.Show("Preencha o nome das duas cidades!");
                    return;
                }

                // normaliza só no uso (B)
                nomeOrigem = NormalizarEntrada(nomeOrigem);
                nomeDestino = NormalizarEntrada(nomeDestino);

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

                // ===== EXPANDE LIGAÇÕES DA CIDADE DE DESTINO PARA A ORIGEM (opcional) =====
                // (aqui mantive a ideia que você sugeriu: conectar origem às vizinhas de destino para "completar" grafo)
                NoLista<Ligacao> ligDest = destino.Ligacoes.Primeiro;
                while (ligDest != null)
                {
                    string nomeVizinha = NormalizarEntrada(ligDest.Info.Destino);

                    // evita auto-ligação e evita ligar a própria cidade que já recebeu ligação
                    if (!nomeVizinha.Equals(origem.Nome.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        Cidade vizinha = new Cidade(nomeVizinha);

                        if (arvore.Existe(vizinha))
                        {
                            vizinha = arvore.Atual.Info;

                            // Se origem ainda não tem ligação com a vizinha, cria
                            if (!origem.Ligacoes.ExisteDado(new Ligacao(origem.Nome.Trim(), vizinha.Nome.Trim(), ligDest.Info.Distancia)))
                                origem.Ligacoes.InserirEmOrdem(new Ligacao(origem.Nome.Trim(), vizinha.Nome.Trim(), ligDest.Info.Distancia));

                            // Se a vizinha não tem ligação com a origem, cria a volta
                            if (!vizinha.Ligacoes.ExisteDado(new Ligacao(vizinha.Nome.Trim(), origem.Nome.Trim(), ligDest.Info.Distancia)))
                                vizinha.Ligacoes.InserirEmOrdem(new Ligacao(vizinha.Nome.Trim(), origem.Nome.Trim(), ligDest.Info.Distancia));
                        }
                    }

                    ligDest = ligDest.Prox;
                }

                // ===== CRIA LIGAÇÕES BIDIRECIONAIS =====
                Ligacao ida = new Ligacao(origem.Nome.Trim(), destino.Nome.Trim(), distancia);
                Ligacao volta = new Ligacao(destino.Nome.Trim(), origem.Nome.Trim(), distancia);

                if (!origem.Ligacoes.ExisteDado(ida))
                    origem.Ligacoes.InserirEmOrdem(ida);

                if (!destino.Ligacoes.ExisteDado(volta))
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

                if (string.IsNullOrEmpty(nomeOrigem) || string.IsNullOrEmpty(nomeDestino))
                {
                    MessageBox.Show("Preencha o nome das duas cidades!");
                    return;
                }

                // normaliza só no uso (B)
                nomeOrigem = NormalizarEntrada(nomeOrigem);
                nomeDestino = NormalizarEntrada(nomeDestino);

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
                Ligacao ligacaoIda = new Ligacao(origem.Nome.Trim(), destino.Nome.Trim(), 0);
                Ligacao ligacaoVolta = new Ligacao(destino.Nome.Trim(), origem.Nome.Trim(), 0);

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