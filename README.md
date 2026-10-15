# ConversorCid

Utilitário que lê os arquivos oficiais de uma classificação de doenças (CID) e gera um CSV normalizado, com apenas as colunas
necessárias e os valores já tratados, pronto para ser importado por outro sistema.

| Perfil | Entrada | Saída |
|---|---|---|
| `cid11` | planilha `.xlsx` *SimpleTabulation ICD-11 MMS* da OMS (português) | `Cid11.csv` |
| `cid10` | pasta com os 4 CSVs do DATASUS (`CID-10-CAPITULOS`, `-GRUPOS`, `-CATEGORIAS`, `-SUBCATEGORIAS`) | `Cid10.csv` |

Os arquivos oficiais não têm o formato que um sistema consumidor costuma ler: o DATASUS publica quatro CSVs em Windows-1252, e a OMS publica
uma planilha com dezenas de colunas, títulos com prefixo de nível e booleanos como `True`/`False`. O conversor aplica sempre as mesmas regras,
avisa de tudo o que for estranho e se recusa a gravar o que o consumidor não conseguiria ler. O formato de saída é fixo (ver "Formato de saída").

Há duas formas de usar o conversor: a **janela** (`ConversorCidGui.exe`) e a **linha de comando** (`ConversorCid.exe`). As duas fazem a
mesma conversão, com as mesmas regras, porque as duas usam o `ConversorCid.Core`.

## Onde baixar os arquivos

Os arquivos oficiais **não fazem parte deste repositório**. Baixe-os nos sites abaixo e respeite os termos de uso de cada fonte (DATASUS e OMS).

| Tipo | Site | Como chegar ao arquivo |
|---|---|---|
| CID-10 | <http://www2.datasus.gov.br/cid10/V2008/cid10.htm> | Com o site aberto, em [Tabelas da CID-10](http://www2.datasus.gov.br/cid10/V2008/download.htm) baixe os "arquivos em formato CSV". Descompacte o `.zip` e informe a pasta. |
| CID-11 | <https://icd.who.int/browse/2026-01/mms/pt> | No menu "Info" → "Arquivo em planilha". |

Os mesmos endereços e dicas aparecem na janela (vêm do perfil, `IPerfilConversao.OndeBaixar`, no `Core`). O endereço do CID-11 leva à release
`2026-01` da OMS; se a OMS publicar outra, o endereço e o nome da release mudam no `PerfilCid11`.

## Janela

`ConversorCidGui.exe` é um executável único (cerca de 50 MB), que roda sem o .NET instalado: copie o arquivo e abra. Na primeira vez o
Windows pode mostrar o aviso do SmartScreen, porque o arquivo não é assinado (*Mais informações* → *Executar assim mesmo*).

1. **Tipo de arquivo**: CID-10 (pasta com os 4 CSVs do DATASUS) ou CID-11 (planilha `.xlsx` da OMS). Logo abaixo, **Onde baixar** mostra o
   endereço do site oficial do tipo escolhido (clique para abrir no navegador) e uma dica de como chegar ao arquivo.
2. **Entrada**: use *Procurar...* ou arraste o arquivo (ou a pasta) para a janela. Pode colar o caminho com aspas.
3. **Aba da planilha** (só no CID-11): a lista mostra todas as abas, **inclusive as ocultas**, marcadas como `(oculta)`. A escolha de partida é a
   primeira aba, que é a que a linha de comando lê quando nenhuma é indicada.
4. **Arquivo de saída**: já vem preenchido com um destino padrão, que não muda quando a entrada muda. O destino é a constante
   `PastaDeDestinoPadrao` em `src/ConversorCid.Gui/JanelaPrincipal.cs`: **ajuste-a ao seu ambiente**. Se a pasta não existir na máquina, o CSV é
   sugerido ao lado da entrada. *Salvar como...* escolhe outro lugar. Se o arquivo de saída já existir, a janela **pergunta antes de substituir**
   (com *Não*, nada é alterado).
5. **Converter**: o resultado mostra as linhas lidas e gravadas e a lista de avisos (número da linha e mensagem). Na lista, `Ctrl+A` seleciona tudo
   e `Ctrl+C` copia. *Abrir pasta do arquivo* abre o Explorer com o CSV selecionado.

Um erro (arquivo que não é planilha, aba ou colunas que não servem ao perfil, arquivo em uso) aparece numa mensagem em português e nada é gravado.

## Uso

```
ConversorCid <perfil> <entrada> [saida.csv] [opções]
ConversorCid --perfis
```

`<entrada>` é o arquivo `.xlsx` (perfil de arquivo único) ou a **pasta** que contém os arquivos (perfil de vários).

| Opção | Efeito |
|---|---|
| `--aba <nome>` | Aba a converter (padrão: a primeira). Só vale para `.xlsx`. |
| `--sobrescrever` | Substitui o CSV se ele já existir. Sem a opção, o programa recusa e não altera nada. |
| `--todos-avisos` | Lista todos os avisos (por padrão, os 20 primeiros). |
| `--perfis` | Lista os perfis disponíveis. |

Sem `saida.csv`, o arquivo é gravado ao lado da planilha (ou dentro da pasta) com o nome do perfil: `Cid11.csv` ou `Cid10.csv`.

```
ConversorCid cid11 "C:\Downloads\Exemplo Cid11.xlsx" "C:\Downloads\Cid11.csv" --sobrescrever
ConversorCid cid10 "C:\Downloads\CID10-DATASUS"
```

Códigos de saída: `0` sucesso (mesmo com avisos) · `1` uso inválido · `2` planilha/arquivo inválido (formato, aba ou
colunas) · `3` erro de arquivo (não existe, já existe, em uso) · `4` erro inesperado.

A planilha pode estar aberta no Excel durante a conversão.

### Avisos

A conversão não falha por dado estranho; ela grava e avisa, com o número da linha do arquivo de entrada:
título vazio, campo que continha `|` (trocado por espaço), valor fora do domínio esperado, item sem pai ou fora de
qualquer capítulo (ignorado), código repetido (mantido o primeiro) e, de uma vez só, a quantidade de títulos que passam do tamanho
máximo esperado pelo consumidor e seriam truncados.

## Formato de saída (comum aos perfis)

Delimitador `|`, UTF-8 **com BOM**, quebra de linha CRLF (inclusive na última linha), sem aspas, cabeçalho na primeira
linha, mesma ordem de linhas do arquivo final. O consumidor lê o arquivo por posição (divisão por `|`), então nenhum campo pode
ter `|` nem quebra de linha: o programa os troca por espaço e se recusa a gravar um campo que ainda os tenha.

Evite abrir e salvar esses CSVs no Excel: ele regrava a codificação e pode tratar as aspas que aparecem em alguns
títulos do CID-10 como delimitadores.

## Perfil `cid11`

Entrada: planilha da OMS (a primeira linha é o cabeçalho).

**Colunas de saída** (nesta ordem): `Code`, `BlockId`, `Title`, `ClassKind`, `ChapterNo`, `isLeaf`,
`Grouping1` a `Grouping5`, `Intervalos Inicio`, `Intervalos Fim`. As demais colunas da planilha (URIs, `TitleEN`,
`DepthInKind`, `IsResidual`, `BrowserLink`, `Primary tabulation`, `CodingNote`, `Parent`, versão) não saem.

| Coluna | Tratamento |
|---|---|
| `Title` | Perde o prefixo de nível da OMS (`- - - Cólera` → `Cólera`). |
| `ChapterNo` | Perde o zero à esquerda (`01` → `1`); `V` e `X` ficam como estão. |
| `isLeaf` | `True`/`False` → `TRUE`/`FALSE`. |
| `Intervalos Inicio` / `Fim` | **Só nas linhas de capítulo**: menor e maior código de categoria do capítulo, sem contar as subdivisões com ponto (`1A00.0`). Ex.: capítulo 1 = `1A00` a `1H0Z`. Nas demais linhas ficam em branco. |
| Qualquer campo | O separador `\|`, quebras de linha e tabs viram espaço, e espaços repetidos viram um (`nocardiose\|A43.8` → `nocardiose A43.8`). Gera aviso. |

As regras foram extraídas de um CSV de referência já em uso pelo sistema consumidor e conferidas contra uma planilha da release 2026-Jan-17 da
OMS: cabeçalho, BOM, CRLF e as 13 colunas coincidem, e 12 das 13 colunas são idênticas nas linhas em comum. O que difere são revisões de
tradução entre releases da OMS.

## Perfil `cid10`

Entrada: a pasta com os quatro CSVs do DATASUS (Windows-1252, `;`, uma `;` sobrando no fim de cada linha). Os nomes são
procurados sem diferenciar maiúsculas de minúsculas. Os arquivos `CID-O-*` (morfologia das neoplasias) podem ficar na
mesma pasta: o perfil não os lê, porque o formato de saída não os comporta.

**Saída**: o molde do CID-11. As 13 primeiras colunas têm o mesmo nome, posição e sentido do `cid11`, e
`Classification` e `Reference` vêm no fim:

```
Code|BlockId|Title|ClassKind|ChapterNo|isLeaf|Grouping1..5|Intervalos Inicio|Intervalos Fim|Classification|Reference
```

| `ClassKind` | Origem | Linhas | `Code` / `BlockId` | `isLeaf` |
|---|---|---|---|---|
| `chapter` | `CAPITULOS` | 22 | vazios; `Intervalos` = `CATINIC`/`CATFIM` | FALSE |
| `block` | `GRUPOS` | 275 | `BlockId` = faixa (`A00-A09`); `Intervalos` = a faixa | FALSE |
| `category` | `CATEGORIAS` | 2.045 | `Code` = 3 caracteres (`A00`) | TRUE só se não tem subdivisão |
| `subcategory` | `SUBCATEGORIAS` | 12.188 | `Code` = 4 caracteres (`A000`) | TRUE |

- **Ordem**: pré-ordem, como no CID-11. O capítulo, depois seus grupos, categorias e subcategorias em ordem de código,
  com cada grupo antes do que contém (`C00-C97`, `C00-C75`, `C00-C14`, `C00`, `C000`…). Os capítulos saem em ordem numérica
  (o 22, `U04-U99`, vem depois do 21).
- **`Grouping1..5`**: as faixas que contêm a linha, da mais externa para a mais interna. O DATASUS aninha até 3 níveis.
- **Categoria sem subdivisão** (263) aparece em `CATEGORIAS` e em `SUBCATEGORIAS`, com o mesmo código e o mesmo título.
  Sai **uma** linha, `category` com `isLeaf` TRUE, e o consumidor a trata como CID (a folha), como no CID-11. Por isso
  são 14.530 linhas gravadas para 14.793 lidas.
- **`Title`** do capítulo perde o prefixo (`Capítulo III  - Doenças do sangue` → `Doenças do sangue`).
- **`Classification`** (`+` etiologia, `*` manifestação) e **`Reference`** (o código par, `J99.8*`) vêm do DATASUS e ficam em colunas separadas,
  para o consumidor decidir como usá-las.
- Ficam fora `RESTRSEXO`, `CAUSAOBITO`, `EXCLUIDOS` e `DESCRABREV`, que não fazem parte do formato de saída.

## Estrutura

```
src/ConversorCid.Core      Lógica, sem interface: leitores (xlsx, CSV), perfis, escrita do CSV
src/ConversorCid.Console   Linha de comando (assembly ConversorCid.exe)
src/ConversorCid.Gui       Janela em Windows Forms (assembly ConversorCidGui.exe)
tests/ConversorCid.Core.Tests   xunit; gera .xlsx reais em memória (ClosedXML) e CSVs em Windows-1252
```

O console e a janela são só interfaces sobre o `Core`, que não tem nenhuma regra de tela. A janela não tem testes automáticos próprios (o que ela
decide vem do `Core`, que tem: `AbasXlsx`, `ConversorParaCsv.SaidaPadrao` e `PerfilConversaoExtensoes`); foi conferida abrindo o executável e
convertendo de verdade pela automação de interface do Windows. O ponto de entrada da conversão é
`ConversorCid.Conversao.ConversorParaCsv`:

```csharp
var perfil = CatalogoPerfis.Buscar("cid10")!;
var resultado = new ConversorParaCsv().Converter(@"C:\...\CID10-DATASUS", @"C:\...\Cid10.csv", perfil, sobrescrever: true);
// resultado.Entradas (o que foi lido), resultado.LinhasGravadas, resultado.Avisos
```

Há também as sobrecargas com `Stream` (sem tocar o disco) e `CatalogoPerfis.Todos` para popular uma lista de tipos.
Erros esperados são `PlanilhaInvalidaException` (mensagem pronta para o usuário), `IOException` e `FileNotFoundException`.

## Como adicionar um perfil

1. Crie `src/ConversorCid.Core/Perfis/PerfilXxx.cs` implementando `IPerfilConversao`: as `Entradas` (cada uma com seu
   leitor, colunas exigidas e, se o perfil lê uma pasta, o nome do arquivo), as `ColunasSaida`, o `ArquivoSaidaPadrao` e o
   `Converter`. Os tratamentos comuns estão em `Transformacoes` e `LeituraDeCampos`.
2. Acrescente o perfil em `CatalogoPerfis.Todos`.
3. Escreva os testes seguindo `PerfilCid11Tests` / `PerfilCid10Tests` (dados em memória) e um caso ponta a ponta em
   `ConversorParaCsvTests`.

Nada mais muda: o console e a janela enxergam o perfil novo pelo catálogo.

## Build e testes

Requer o SDK do .NET 10.

```
dotnet build
dotnet test
dotnet publish src/ConversorCid.Gui -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o publish
dotnet publish src/ConversorCid.Console -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

O primeiro publish gera `publish\ConversorCidGui.exe` (a janela, ~50 MB) e o segundo `publish\ConversorCid.exe` (a linha de comando, ~70 MB). Os dois rodam sem
o .NET instalado, porque levam o runtime junto. Para um executável pequeno em máquinas que já têm o .NET 10, troque `--self-contained`
por `--no-self-contained`. A janela só existe no Windows (Windows Forms).

> Em máquinas com as variáveis `MSBuildExtensionsPath`/`MSBuildSDKsPath` apontando para um MSBuild antigo,
> o restore falha com *"Propriedade solicitada 'OutputPath' ausente"*. Limpe as duas variáveis na sessão antes do build.

## Limitações

- O xlsx é lido só em `.xlsx` (o `.xls` antigo é recusado com a orientação de salvar como `.xlsx`).
- Em planilha, a primeira linha precisa ser o cabeçalho, com os nomes de coluna exatamente como no perfil.
- Em CSV, não há tratamento de aspas (nos arquivos do DATASUS elas são caracteres do texto), e uma linha com mais campos
  que o cabeçalho é recusada.
- A CID-O (morfologia das neoplasias) não é convertida.
- A janela converte um arquivo de cada vez e não guarda as escolhas entre execuções.
- **Regra do título do CID-11:** o conversor tira só o prefixo de nível (`- - `). Fluxos manuais costumam tirar também todo `- ` (hífen e
  espaço) no meio do título, o que estraga travessões legítimos (`PTH - hormônio da paratireoide`) e transforma `pós- procedimento` em
  `pósprocedimento`. A regra atual preserva os travessões; os poucos títulos que já vêm com o artefato da tradução da OMS (`pós- procedimento`)
  continuam com ele.

## Licença

[MIT](LICENSE).
