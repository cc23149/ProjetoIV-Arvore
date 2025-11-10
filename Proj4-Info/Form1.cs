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
        public Form1()
        {
            InitializeComponent();
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

                    // Atualiza o ComboBox
                    cbxCidadeDestino.Items.Clear();
                    PreencherComboCidades(arvore.Raiz);
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

        private void btnBuscarCaminho_Click(object sender, EventArgs e)
        {
            try
            {
                string nomeOrigem = txtNomeCidade.Text.Trim();
                string nomeDestino = cbxCidadeDestino.Text.Trim();

                if (nomeOrigem == "" || nomeDestino == "")
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

                // ===== Estruturas =====
                FilaLista<Cidade> fila = new FilaLista<Cidade>();
                ListaSimples<Cidade> visitados = new ListaSimples<Cidade>();
                ListaSimples<Ligacao> caminho = new ListaSimples<Ligacao>();

                fila.Enfileirar(origem);
                visitados.InserirAposFim(origem);

                bool achou = false;

                // ===== Busca em largura =====
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

                // ===== Exibição do resultado =====
                dgvRotas.Rows.Clear();
                int total = 0;

                if (achou)
                {
                    NoLista<Ligacao> lig = caminho.Primeiro;
                    while (lig != null)
                    {
                        dgvRotas.Rows.Add(lig.Info.Destino, lig.Info.Distancia);
                        total += lig.Info.Distancia;
                        lig = lig.Prox;
                    }

                    lbDistanciaTotal.Text = "Distância total: " + total + " km";
                }
                else
                {
                    MessageBox.Show("Não há caminho entre essas cidades!", "Busca");
                    lbDistanciaTotal.Text = "Distância total: 0 km";
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
