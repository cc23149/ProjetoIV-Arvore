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
                // obtém o diretório onde o programa está sendo executado (bin\Debug)
                var pastaBase = Application.StartupPath;

                // sobe duas pastas (Debug -> bin -> Proj4-Info)
                var dir = new DirectoryInfo(pastaBase);
                var pastaProjeto = dir.Parent.Parent.FullName;

                // monta o caminho completo até a pasta "Dados"
                var pastaDados = Path.Combine(pastaProjeto, "Dados");

                // define os nomes dos arquivos dentro da pasta Dados
                var arqCidades = Path.Combine(pastaDados, "cidades.dat");
                var arqCaminhos = Path.Combine(pastaDados, "GrafoOnibusSaoPaulo.txt");


                // ====== Leitura das cidades ======
                if (File.Exists(arqCidades))
                {
                    arvore.LerArquivoDeRegistros(arqCidades);
                    MessageBox.Show("Cidades carregadas com sucesso!", "Leitura de Arquivo");
                }
                else
                    MessageBox.Show("Arquivo de cidades não encontrado!", "Aviso");


                // ====== Leitura dos caminhos ======
                if (File.Exists(arqCaminhos))
                {
                    using (var arqTxt = new FileStream(arqCaminhos, FileMode.Open))
                    using (var leitor = new StreamReader(arqTxt))
                    {
                        string linha;
                        while ((linha = leitor.ReadLine()) != null)
                        {
                            // Divide a linha nos três campos
                            var partes = linha.Split(';');

                            // ignora linhas vazias ou incompletas
                            if (partes.Length < 3)
                                continue;

                            var nomeOrigem = partes[0].Trim();
                            var nomeDestino = partes[1].Trim();
                            var distancia = int.Parse(partes[2].Trim());

                            // Cria a ligação
                            var lig = new Ligacao(nomeOrigem, nomeDestino, distancia);

                            // Localiza as cidades na árvore
                            var cidadeOrigem = new Cidade(nomeOrigem);
                            var cidadeDestino = new Cidade(nomeDestino);

                            if (arvore.Existe(cidadeOrigem))
                            {
                                cidadeOrigem = arvore.Atual.Info;
                                cidadeOrigem.Ligacoes.InserirEmOrdem(lig);
                            }

                            // como é bidirecional, adiciona a volta também
                            var volta = new Ligacao(nomeDestino, nomeOrigem, distancia);
                            if (arvore.Existe(cidadeDestino))
                            {
                                cidadeDestino = arvore.Atual.Info;
                                cidadeDestino.Ligacoes.InserirEmOrdem(volta);
                            }
                        }
                    }

                    MessageBox.Show("Caminhos carregados com sucesso!", "Leitura de Arquivo");
                }
                else
                    MessageBox.Show("Arquivo de caminhos não encontrado!", "Aviso");

                // ====== Desenho inicial da árvore ======
                pnlArvore.Refresh();

                // ====== Preenche o ComboBox com as cidades ======
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
                    MessageBox.Show("Cidade encontrada!", "Busca");
                }
                else
                    MessageBox.Show("Cidade não encontrada!", "Aviso");
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

                origem = arvore.Atual.Info;

                // ===== Estruturas da busca =====
                FilaLista<Cidade> fila = new FilaLista<Cidade>();
                ListaSimples<Cidade> visitados = new ListaSimples<Cidade>();
                ListaSimples<Ligacao> caminho = new ListaSimples<Ligacao>();

                fila.Enfileirar(origem);
                visitados.InserirAposFim(origem);

                bool achou = false;

                while (!fila.EstaVazia && !achou)
                {
                    Cidade atual = fila.Retirar();

                    // percorre as ligações da cidade atual
                    foreach (Ligacao lig in atual.Ligacoes.Listar())
                    {
                        Cidade proxima = new Cidade(lig.Destino);

                        // usa o método correto da ListaSimples: ExisteDado
                        if (!visitados.ExisteDado(proxima))
                        {
                            // localiza o objeto Cidade na árvore para obter as coordenadas e a lista de ligações
                            if (arvore.Existe(proxima))
                            {
                                proxima = arvore.Atual.Info;

                                fila.Enfileirar(proxima);
                                visitados.InserirAposFim(proxima);

                                // guarda a aresta percorrida (origem atual -> proxima)
                                caminho.InserirAposFim(new Ligacao(atual.Nome, proxima.Nome, lig.Distancia));

                                if (proxima.Nome.Trim() == destino.Nome.Trim())
                                {
                                    achou = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                // ===== Resultado =====
                dgvRotas.Rows.Clear();
                int total = 0;

                if (achou)
                {
                    foreach (Ligacao lig in caminho.Listar())
                    {
                        dgvRotas.Rows.Add(lig.Destino.Trim(), lig.Distancia);
                        total += lig.Distancia;
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

                if (nomeOrigem == nomeDestino)
                {
                    MessageBox.Show("As cidades devem ser diferentes!");
                    return;
                }

                Cidade origem = new Cidade(nomeOrigem);
                Cidade destino = new Cidade(nomeDestino);

                if (arvore.Existe(origem) && arvore.Existe(destino))
                {
                    origem = arvore.Atual.Info;

                    arvore.Existe(destino);
                    destino = arvore.Atual.Info;

                    Ligacao ida = new Ligacao(nomeOrigem, nomeDestino, distancia);
                    Ligacao volta = new Ligacao(nomeDestino, nomeOrigem, distancia);

                    origem.Ligacoes.InserirEmOrdem(ida);
                    destino.Ligacoes.InserirEmOrdem(volta);

                    MessageBox.Show("Caminho incluído com sucesso!", "Inclusão");

                    // Atualiza a grade de ligações visuais
                    AtualizarGridLigacoes(origem);
                }
                else
                {
                    MessageBox.Show("Uma das cidades não foi encontrada!", "Aviso");
                }
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

                Cidade origem = new Cidade(nomeOrigem);
                Cidade destino = new Cidade(nomeDestino);

                if (arvore.Existe(origem) && arvore.Existe(destino))
                {
                    origem = arvore.Atual.Info;
                    origem.Ligacoes.RemoverDado(new Ligacao(nomeOrigem, nomeDestino, 0));

                    arvore.Existe(destino);
                    destino = arvore.Atual.Info;
                    destino.Ligacoes.RemoverDado(new Ligacao(nomeDestino, nomeOrigem, 0));

                    MessageBox.Show("Caminho excluído com sucesso!", "Exclusão");

                    AtualizarGridLigacoes(origem);
                }
                else
                {
                    MessageBox.Show("Uma das cidades não foi encontrada!", "Aviso");
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
