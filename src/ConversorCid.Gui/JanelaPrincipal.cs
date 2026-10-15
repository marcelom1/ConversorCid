using System.Diagnostics;
using System.Globalization;
using System.Text;
using ConversorCid.Conversao;
using ConversorCid.Perfis;
using ConversorCid.Planilhas;

namespace ConversorCid.Gui;

/// <summary>
/// A janela do conversor: o mesmo que o console faz, com o tipo de arquivo, a entrada, a aba e a saída escolhidos na tela.
/// Não tem regra de conversão; tudo vem do <see cref="ConversorParaCsv"/> e dos perfis do Core.
/// </summary>
internal sealed class JanelaPrincipal : Form
{
    private const string Titulo = "Conversor de CID";

    // Onde o RH guarda os recursos Cid10.csv e Cid11.csv: é o destino sugerido (se a pasta existir nesta máquina).
    private const string PastaDeDestinoPadrao = @"D:\Benner\fontes\rh\prod\dotnet\Benner.RH.Medicina\Cids\Arquivos";

    private readonly ComboBox _cmbPerfil = new() { Name = "cmbPerfil", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label _lblDescricao = Texto("lblDescricao", linhas: 2, apagado: true);
    private readonly LinkLabel _lnkOrigem = Link("lnkOrigem", linhas: 1);
    private readonly LinkLabel _lnkDica = Link("lnkDica", linhas: 2, apagado: true);
    private Label? _lblOndeBaixar;
    private readonly TextBox _txtEntrada = new() { Name = "txtEntrada", Dock = DockStyle.Fill };
    private readonly Button _btnEntrada = Botao("btnEntrada", "Procurar…");
    private readonly Label _lblDicaEntrada = Texto("lblDicaEntrada", linhas: 2, apagado: true);
    private readonly ComboBox _cmbAba = new() { Name = "cmbAba", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox _txtSaida = new() { Name = "txtSaida", Dock = DockStyle.Fill };
    private readonly Button _btnSaida = Botao("btnSaida", "Salvar como…");
    private readonly Button _btnConverter = Botao("btnConverter", "Converter");
    private readonly TableLayoutPanel _formulario = new() { Name = "pnlFormulario", Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
    private readonly TextBox _txtResumo = new()
    {
        Name = "txtResumo", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = SystemColors.Window,
    };
    private readonly Label _lblAvisos = Texto("lblAvisos", linhas: 1);
    private readonly ListView _lvAvisos = new()
    {
        Name = "lvAvisos", View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = true, Dock = DockStyle.Fill,
    };
    private readonly Button _btnAbrirPasta = Botao("btnAbrirPasta", "Abrir pasta do arquivo");
    private readonly Label _lblStatus = Texto("lblStatus", linhas: 1);
    private readonly ProgressBar _progresso = new()
    {
        Name = "prgProgresso", Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Visible = false, Dock = DockStyle.Fill,
    };

    private string? _saidaConfirmada;      // caminho que o usuário já aceitou substituir no "Salvar como"
    private bool _saidaEditada;            // o usuário escolheu ou digitou a saída: a janela deixa de sugerir uma
    private bool _atualizandoSaida;
    private bool _ocupado;
    private string? _ultimoCsv;

    public JanelaPrincipal()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = Titulo;
        ClientSize = new Size(820, 760);
        MinimumSize = new Size(680, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AcceptButton = _btnConverter;

        MontarFormulario();
        MontarResultado(out var grupoResultado);
        var rodape = MontarRodape();

        var principal = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
        principal.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        principal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        principal.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        principal.Controls.Add(_formulario, 0, 0);
        principal.Controls.Add(grupoResultado, 0, 1);
        principal.Controls.Add(rodape, 0, 2);
        Controls.Add(principal);

        _cmbPerfil.Items.AddRange(CatalogoPerfis.Todos.Select(p => (object)new ItemPerfil(p)).ToArray());
        _cmbPerfil.SelectedIndex = 0;

        _cmbPerfil.SelectedIndexChanged += (_, _) => AoMudarPerfil();
        _txtEntrada.TextChanged += (_, _) => AoMudarEntrada();
        _txtSaida.TextChanged += (_, _) => AoEditarSaida();
        _btnEntrada.Click += (_, _) => ProcurarEntrada();
        _btnSaida.Click += (_, _) => EscolherSaida();
        _btnConverter.Click += async (_, _) => await ConverterAsync();
        _btnAbrirPasta.Click += (_, _) => AbrirPasta();
        _lnkOrigem.LinkClicked += AoClicarNoLink;
        _lnkDica.LinkClicked += AoClicarNoLink;
        _lvAvisos.KeyDown += AoTeclarNosAvisos;
        _lvAvisos.Resize += (_, _) => AjustarColunaDoAviso();

        HabilitarArraste(this);
        ResumeLayout(performLayout: true);
        AoMudarPerfil();
    }

    private IPerfilConversao? PerfilAtual => (_cmbPerfil.SelectedItem as ItemPerfil)?.Perfil;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Com a tela em escala alta, o tamanho de partida pode passar da área de trabalho: a janela encolhe para caber.
        var area = Screen.FromControl(this).WorkingArea;
        if (Width > area.Width || Height > area.Height)
        {
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            CenterToScreen();
        }
    }

    // ------------------------------------------------------------------------------------------------ montagem

    private void MontarFormulario()
    {
        _formulario.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _formulario.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _formulario.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var linha = 0;
        Linha(linha++, "Tipo de arquivo:", _cmbPerfil, null);
        Linha(linha++, null, _lblDescricao, null);
        _lblOndeBaixar = Linha(linha++, "Onde baixar:", _lnkOrigem, null);
        Linha(linha++, null, _lnkDica, null);
        Linha(linha++, "Entrada:", _txtEntrada, _btnEntrada);
        Linha(linha++, null, _lblDicaEntrada, null);
        Linha(linha++, "Aba da planilha:", _cmbAba, null);
        Linha(linha++, "Arquivo de saída:", _txtSaida, _btnSaida);

        _btnConverter.Anchor = AnchorStyles.Right;
        _btnConverter.Margin = new Padding(3, 10, 3, 6);
        _formulario.Controls.Add(_btnConverter, 1, linha);
        _formulario.SetColumnSpan(_btnConverter, 2);
        _formulario.RowCount = linha + 1;
        for (var i = 0; i < _formulario.RowCount; i++)
            _formulario.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    }

    private Label? Linha(int linha, string? rotulo, Control campo, Button? botao)
    {
        Label? rotuloCriado = null;
        if (rotulo is not null)
        {
            rotuloCriado = new Label { Text = rotulo, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 10, 3) };
            _formulario.Controls.Add(rotuloCriado, 0, linha);
        }

        campo.Margin = new Padding(3, 4, 3, 4);
        _formulario.Controls.Add(campo, 1, linha);

        if (botao is null)
            _formulario.SetColumnSpan(campo, 2);
        else
            _formulario.Controls.Add(botao, 2, linha);

        return rotuloCriado;
    }

    private void MontarResultado(out GroupBox grupo)
    {
        var interno = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8, 4, 8, 8) };
        interno.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        interno.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        interno.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _txtResumo.Font = new Font("Consolas", 9F);   // os números das linhas lidas e gravadas ficam alinhados
        _txtResumo.Margin = new Padding(3, 3, 3, 6);
        _lblAvisos.Margin = new Padding(3, 3, 3, 3);
        _lvAvisos.Columns.Add("Linha", 80, HorizontalAlignment.Right);
        _lvAvisos.Columns.Add("Aviso", 560);

        interno.Controls.Add(_txtResumo, 0, 0);
        interno.Controls.Add(_lblAvisos, 0, 1);
        interno.Controls.Add(_lvAvisos, 0, 2);

        grupo = new GroupBox { Text = "Resultado", Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 6) };
        grupo.Controls.Add(interno);
    }

    private TableLayoutPanel MontarRodape()
    {
        var rodape = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1 };
        rodape.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rodape.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rodape.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        rodape.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _btnAbrirPasta.Enabled = false;
        _lblStatus.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _lblStatus.AutoSize = false;
        _lblStatus.Height = 20;
        _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        _lblStatus.Margin = new Padding(10, 3, 3, 3);
        _progresso.Margin = new Padding(3, 8, 3, 8);

        rodape.Controls.Add(_btnAbrirPasta, 0, 0);
        rodape.Controls.Add(_lblStatus, 1, 0);
        rodape.Controls.Add(_progresso, 2, 0);
        return rodape;
    }

    private static Label Texto(string nome, int linhas, bool apagado = false) => new()
    {
        Name = nome,
        AutoSize = false,
        Dock = DockStyle.Fill,
        Height = 18 * linhas + 2,
        ForeColor = apagado ? SystemColors.GrayText : SystemColors.ControlText,
    };

    private static LinkLabel Link(string nome, int linhas, bool apagado = false) => new()
    {
        Name = nome,
        AutoSize = false,
        Dock = DockStyle.Fill,
        Height = 18 * linhas + 2,
        ForeColor = apagado ? SystemColors.GrayText : SystemColors.ControlText,
        LinkColor = SystemColors.HotTrack,
        ActiveLinkColor = SystemColors.HotTrack,
        VisitedLinkColor = SystemColors.HotTrack,
    };

    private static Button Botao(string nome, string texto) => new()
    {
        Name = nome, Text = texto, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(120, 30), Margin = new Padding(3, 3, 3, 3),
    };

    // ------------------------------------------------------------------------------------------------ o que muda com o perfil e a entrada

    private void AoMudarPerfil()
    {
        var perfil = PerfilAtual;
        if (perfil is null)
            return;

        _lblDescricao.Text = perfil.Descricao;
        _lblDicaEntrada.Text = perfil.EntradaEhArquivo()
            ? $"Arquivo: {perfil.Entradas[0].Descricao}. Também dá para arrastar o arquivo para esta janela."
            : $"Pasta que contém: {string.Join(", ", perfil.Entradas.Select(e => e.NomeDoArquivo))}. Também dá para arrastar a pasta para esta janela.";

        AtualizarOrigem(perfil.OndeBaixar);

        _txtEntrada.Text = string.Empty;
        _saidaEditada = false;
        _saidaConfirmada = null;
        DefinirSaida(SaidaSugerida());
        LimparResultado();
        AtualizarAbas();
        AtualizarEstado(perfil.EntradaEhArquivo() ? "Escolha o arquivo de entrada." : "Escolha a pasta de entrada.");
    }

    private void AoMudarEntrada()
    {
        var problemaDasAbas = AtualizarAbas();
        if (!_saidaEditada)
            DefinirSaida(SaidaSugerida());

        // O que está errado com a entrada, se houver: some do status assim que o caminho passa a existir.
        var problema = ProblemaDaEntrada() ?? problemaDasAbas;
        AtualizarEstado(problema);
        if (problema is null && _btnConverter.Enabled)
            _lblStatus.Text = "Pronto para converter.";
    }

    private string? ProblemaDaEntrada()
    {
        var perfil = PerfilAtual;
        var entrada = Caminho(_txtEntrada);
        if (perfil is null || entrada.Length == 0)
            return null;

        if (perfil.EntradaEhArquivo())
            return File.Exists(entrada) ? null : "O arquivo não foi encontrado.";

        return Directory.Exists(entrada) ? null : "A pasta não foi encontrada.";
    }

    private void AoEditarSaida()
    {
        if (!_atualizandoSaida)
        {
            _saidaEditada = _txtSaida.Text.Length > 0;
            _saidaConfirmada = null;
        }

        AtualizarEstado(null);
    }

    /// <summary>Preenche a lista de abas. Devolve o motivo quando a planilha não pôde ser lida; nulo quando está tudo certo.</summary>
    private string? AtualizarAbas()
    {
        _cmbAba.Items.Clear();
        var perfil = PerfilAtual;

        if (perfil is null || !perfil.EntradaEhPlanilha())
        {
            _cmbAba.Items.Add("(não se aplica a este tipo de arquivo)");
            _cmbAba.SelectedIndex = 0;
            _cmbAba.Enabled = false;
            return null;
        }

        var caminho = Caminho(_txtEntrada);
        if (!File.Exists(caminho))
        {
            _cmbAba.Items.Add("(escolha o arquivo primeiro)");
            _cmbAba.SelectedIndex = 0;
            _cmbAba.Enabled = false;
            return null;   // se o texto não está vazio, ProblemaDaEntrada já diz que o arquivo não existe
        }

        try
        {
            foreach (var aba in AbasXlsx.Listar(caminho))
                _cmbAba.Items.Add(new ItemAba(aba));

            if (_cmbAba.Items.Count == 0)
                throw new PlanilhaInvalidaException("A planilha não tem nenhuma aba.");

            // A primeira aba é a que a conversão lê quando nenhuma é indicada: é a escolha de partida.
            _cmbAba.SelectedIndex = 0;
            _cmbAba.Enabled = true;
            return null;
        }
        catch (Exception ex) when (ex is PlanilhaInvalidaException or IOException or UnauthorizedAccessException)
        {
            _cmbAba.Items.Clear();
            _cmbAba.Items.Add("(não foi possível listar as abas)");
            _cmbAba.SelectedIndex = 0;
            _cmbAba.Enabled = false;
            return ex.Message;
        }
    }

    /// <summary>
    /// A pasta de recursos do RH, com o nome do arquivo do perfil, quando essa pasta existe nesta máquina (o destino não muda com a
    /// entrada). Sem ela, o CSV fica ao lado da entrada, como no console.
    /// </summary>
    private string SaidaSugerida()
    {
        var perfil = PerfilAtual;
        if (perfil is null)
            return string.Empty;

        if (Directory.Exists(PastaDeDestinoPadrao))
            return Path.Combine(PastaDeDestinoPadrao, perfil.ArquivoSaidaPadrao);

        var entrada = Caminho(_txtEntrada);
        if (entrada.Length == 0)
            return string.Empty;

        try
        {
            return ConversorParaCsv.SaidaPadrao(entrada, perfil);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty; // o caminho digitado ainda não é válido
        }
    }

    private void DefinirSaida(string caminho)
    {
        _atualizandoSaida = true;
        try
        {
            _txtSaida.Text = caminho;
        }
        finally
        {
            _atualizandoSaida = false;
        }
    }

    private void AtualizarEstado(string? mensagem)
    {
        _btnConverter.Enabled = !_ocupado && PerfilAtual is not null && Caminho(_txtEntrada).Length > 0 && Caminho(_txtSaida).Length > 0;
        if (mensagem is not null)
            _lblStatus.Text = mensagem;
    }

    /// <summary>O texto do campo sem espaços nem aspas (quem cola um caminho do Explorer costuma trazer as aspas).</summary>
    private static string Caminho(TextBox campo) => campo.Text.Trim().Trim('"').Trim();

    // ------------------------------------------------------------------------------------------------ de onde baixar

    /// <summary>Mostra o link do site oficial e a dica do tipo escolhido; os trechos da dica que também são links viram links.</summary>
    private void AtualizarOrigem(OndeBaixar? onde)
    {
        var visivel = onde is not null;
        _lblOndeBaixar!.Visible = visivel;
        _lnkOrigem.Visible = visivel;
        _lnkDica.Visible = visivel;
        _lnkOrigem.Links.Clear();
        _lnkDica.Links.Clear();

        if (onde is null)
        {
            _lnkOrigem.Text = string.Empty;
            _lnkDica.Text = string.Empty;
            return;
        }

        _lnkOrigem.Text = onde.Url;
        _lnkOrigem.Links.Add(0, onde.Url.Length, onde.Url);

        _lnkDica.Text = onde.Dica;
        foreach (var link in onde.LinksDaDica)
        {
            var inicio = onde.Dica.IndexOf(link.Texto, StringComparison.Ordinal);
            if (inicio >= 0)
                _lnkDica.Links.Add(inicio, link.Texto.Length, link.Url);
        }
    }

    private void AoClicarNoLink(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link?.LinkData is not string url)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, $"Não foi possível abrir o navegador. Copie o endereço e abra-o à mão:\n\n{url}", Titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ------------------------------------------------------------------------------------------------ escolher arquivos

    private void ProcurarEntrada()
    {
        var perfil = PerfilAtual;
        if (perfil is null)
            return;

        var atual = Caminho(_txtEntrada);
        var pastaInicial = Directory.Exists(atual) ? atual : File.Exists(atual) ? Path.GetDirectoryName(atual) : null;

        if (perfil.EntradaEhArquivo())
        {
            using var dialogo = new OpenFileDialog
            {
                Title = "Escolha o arquivo de entrada",
                Filter = perfil.EntradaEhPlanilha() ? "Planilhas do Excel (*.xlsx)|*.xlsx|Todos os arquivos (*.*)|*.*" : "Todos os arquivos (*.*)|*.*",
                CheckFileExists = true,
                InitialDirectory = pastaInicial ?? string.Empty,
            };
            if (dialogo.ShowDialog(this) == DialogResult.OK)
                _txtEntrada.Text = dialogo.FileName;
        }
        else
        {
            using var dialogo = new FolderBrowserDialog
            {
                Description = $"Escolha a pasta com os arquivos de entrada ({perfil.Nome.ToUpperInvariant()})",
                UseDescriptionForTitle = true,
                InitialDirectory = pastaInicial ?? string.Empty,
            };
            if (dialogo.ShowDialog(this) == DialogResult.OK)
                _txtEntrada.Text = dialogo.SelectedPath;
        }
    }

    private void EscolherSaida()
    {
        var atual = Caminho(_txtSaida);
        using var dialogo = new SaveFileDialog
        {
            Title = "Onde gravar o CSV",
            Filter = "Arquivo CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
            AddExtension = true,
            DefaultExt = "csv",
            OverwritePrompt = true,
            FileName = atual.Length > 0 ? Path.GetFileName(atual) : (PerfilAtual?.ArquivoSaidaPadrao ?? "saida.csv"),
            InitialDirectory = atual.Length > 0 && Path.GetDirectoryName(atual) is { Length: > 0 } pasta && Directory.Exists(pasta) ? pasta : string.Empty,
        };

        if (dialogo.ShowDialog(this) != DialogResult.OK)
            return;

        _txtSaida.Text = dialogo.FileName;
        _saidaEditada = true;
        _saidaConfirmada = dialogo.FileName; // o próprio diálogo já perguntou se pode substituir
    }

    private void HabilitarArraste(Control raiz)
    {
        raiz.AllowDrop = true;
        raiz.DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true && !_ocupado ? DragDropEffects.Copy : DragDropEffects.None;
        raiz.DragDrop += (_, e) =>
        {
            if (!_ocupado && e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } itens)
                _txtEntrada.Text = itens[0];
        };

        foreach (Control filho in raiz.Controls)
            HabilitarArraste(filho);
    }

    // ------------------------------------------------------------------------------------------------ converter

    private async Task ConverterAsync()
    {
        var perfil = PerfilAtual;
        if (perfil is null)
            return;

        var entrada = Caminho(_txtEntrada);
        var saida = Caminho(_txtSaida);
        var aba = (_cmbAba.SelectedItem as ItemAba)?.Aba.Nome;

        if (File.Exists(saida) && !string.Equals(saida, _saidaConfirmada, StringComparison.OrdinalIgnoreCase))
        {
            var resposta = MessageBox.Show(
                this, $"O arquivo de saída já existe:\n\n{saida}\n\nDeseja substituí-lo?", Titulo,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (resposta != DialogResult.Yes)
            {
                _lblStatus.Text = "Cancelado: o arquivo de saída não foi alterado.";
                return;
            }
        }

        var sobrescrever = File.Exists(saida);   // chegou até aqui: o usuário aceitou substituir, ou o arquivo não existe

        DefinirOcupado(true);
        LimparResultado();
        var relogio = Stopwatch.StartNew();
        try
        {
            var resultado = await Task.Run(() => new ConversorParaCsv().Converter(entrada, saida, perfil, aba, sobrescrever));
            MostrarResultado(resultado, relogio.Elapsed);
        }
        catch (Exception ex)
        {
            MostrarErro(ex);
        }
        finally
        {
            DefinirOcupado(false);
        }
    }

    private void DefinirOcupado(bool ocupado)
    {
        _ocupado = ocupado;
        _formulario.Enabled = !ocupado;
        _cmbPerfil.Enabled = !ocupado;
        _progresso.Visible = ocupado;
        UseWaitCursor = ocupado;
        if (ocupado)
            _lblStatus.Text = "Convertendo…";
        AtualizarEstado(null);
    }

    private void LimparResultado()
    {
        _txtResumo.Clear();
        _txtResumo.ForeColor = SystemColors.WindowText;
        _lvAvisos.Items.Clear();
        _lblAvisos.Text = "Avisos";
        _btnAbrirPasta.Enabled = false;
        _ultimoCsv = null;
    }

    private void MostrarResultado(ResultadoConversao resultado, TimeSpan duracao)
    {
        var texto = new StringBuilder();
        texto.AppendLine("Conversão concluída.");
        foreach (var entrada in resultado.Entradas)
            texto.AppendLine($"Lido:     {Milhar(entrada.Linhas),8} linhas  ({entrada.Origem})");
        texto.AppendLine($"Gravado:  {Milhar(resultado.LinhasGravadas),8} linhas");
        texto.Append($"Arquivo:  {resultado.CaminhoSaida}");
        _txtResumo.Text = texto.ToString();
        _txtResumo.ForeColor = SystemColors.WindowText;

        _lvAvisos.BeginUpdate();
        try
        {
            _lvAvisos.Items.AddRange(resultado.Avisos
                .Select(a => new ListViewItem([a.Linha > 0 ? a.Linha.ToString(CultureInfo.InvariantCulture) : "—", a.Mensagem]))
                .ToArray());
        }
        finally
        {
            _lvAvisos.EndUpdate();
        }

        AjustarColunaDoAviso();
        _lblAvisos.Text = resultado.Avisos.Count == 0
            ? "Nenhum aviso."
            : $"Avisos ({Milhar(resultado.Avisos.Count)}). Selecione e use Ctrl+C para copiar.";

        _ultimoCsv = resultado.CaminhoSaida;
        _btnAbrirPasta.Enabled = _ultimoCsv is not null;
        _lblStatus.Text = $"Concluído em {duracao.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',')} s.";
    }

    private void MostrarErro(Exception ex)
    {
        var mensagem = ex switch
        {
            PlanilhaInvalidaException or IOException => ex.Message,
            UnauthorizedAccessException => $"Sem permissão de acesso ao arquivo: {ex.Message}",
            ArgumentException => $"Caminho inválido: {ex.Message}",
            _ => $"Erro inesperado: {ex}",
        };

        _txtResumo.Text = $"Não foi possível converter.{Environment.NewLine}{mensagem}";
        _txtResumo.ForeColor = Color.Firebrick;
        _lblStatus.Text = "Falhou.";
        MessageBox.Show(this, mensagem, Titulo, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    // ------------------------------------------------------------------------------------------------ avisos e pasta

    private void AjustarColunaDoAviso()
    {
        if (_lvAvisos.Columns.Count == 2)
            _lvAvisos.Columns[1].Width = Math.Max(200, _lvAvisos.ClientSize.Width - _lvAvisos.Columns[0].Width - 4);
    }

    private void AoTeclarNosAvisos(object? sender, KeyEventArgs e)
    {
        if (!e.Control)
            return;

        if (e.KeyCode == Keys.A)
        {
            foreach (ListViewItem item in _lvAvisos.Items)
                item.Selected = true;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.C && _lvAvisos.SelectedItems.Count > 0)
        {
            var linhas = _lvAvisos.SelectedItems.Cast<ListViewItem>()
                .Select(i => i.SubItems[0].Text == "—" ? i.SubItems[1].Text : $"Linha {i.SubItems[0].Text}: {i.SubItems[1].Text}");
            Clipboard.SetText(string.Join(Environment.NewLine, linhas));
            e.Handled = true;
        }
    }

    private void AbrirPasta()
    {
        if (_ultimoCsv is null || !File.Exists(_ultimoCsv))
            return;

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_ultimoCsv}\"") { UseShellExecute = true });
    }

    /// <summary>Milhar com ponto (34.676), como se escreve em português, sem depender da cultura do Windows.</summary>
    private static string Milhar(int valor) => valor.ToString("#,##0", CultureInfo.InvariantCulture).Replace(',', '.');

    // ------------------------------------------------------------------------------------------------ itens das listas

    private sealed record ItemPerfil(IPerfilConversao Perfil)
    {
        // "CID-11 (OMS): planilha ..." aparece na lista só até os dois pontos; a descrição inteira vai no texto abaixo.
        public override string ToString() => Perfil.Descricao.Split(':')[0];
    }

    private sealed record ItemAba(AbaDaPlanilha Aba)
    {
        public override string ToString() => Aba.Oculta ? $"{Aba.Nome}   (oculta)" : Aba.Nome;
    }
}
