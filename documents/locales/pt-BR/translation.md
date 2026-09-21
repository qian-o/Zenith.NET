# Diretrizes de tradução para português brasileiro

## Recursos e estrutura

O [dicionário de recursos em inglês](../en-US/strings.yml) é a única fonte do texto. Consulte as regras do projeto no [guia de manutenção da documentação](../../maintenance.md). Para iniciar uma tradução, copie o arquivo inglês completo para `strings.yml` neste diretório. Preserve a primeira linha `### YamlMime:Resources` e a codificação UTF-8. Nas atualizações, incorpore as mudanças do original sem sobrescrever as traduções existentes. Este arquivo `translation.md` permanece no diretório e não é publicado no site.

Navegação, ordem dos capítulos, âncoras, links, formatação, código e imagens são mantidos fora de `locales/` e compartilhados entre idiomas. Não adicione artigos, TOC, templates, scripts ou configurações neste diretório.

Altere apenas os valores da `strings.yml` deste idioma. O conjunto de chaves deve corresponder exatamente ao inglês. Não adicione, remova ou renomeie chaves por conta própria, mesmo que os textos pareçam iguais ou ainda não tenham aparecido na interface. Se encontrar um problema no original, informe a chave e o problema aos responsáveis pelo projeto. Corrija primeiro a fonte inglesa e as páginas compartilhadas; depois, atualize as traduções.

## Estilo e contexto

Adote o tom de um manual técnico: preciso, sóbrio e acessível. Explique a relação entre as operações antes de apresentar suas restrições. Nos tutoriais, mantenha instruções claras e resultados verificáveis. Rigor não exige voz passiva nem frases longas.

Revise cada artigo na ordem de leitura, considerando os parágrafos, tabelas e código ao redor. As chaves delimitam o armazenamento, não unidades isoladas de tradução. Confira concordância, artigos e preposições dos links e trechos destacados na frase completa.

Títulos, rótulos, textos curtos visíveis da página inicial e legenda do rodapé não levam ponto final. Parágrafos, diagnósticos e instruções de acessibilidade mantêm a pontuação normal. Use um termo consagrado por conceito, sem acrescentar equivalentes ingleses entre parênteses de forma seletiva. Preserve identificadores de API, arquivos e configurações. Use pipeline no masculino.

Mantenha uma frase quando ela explicar a operação atual, uma escolha de projeto, um resultado verificável ou uma condição necessária. Elimine listas de funções não utilizadas, ressalvas genéricas e requisitos repetidos. Traduza títulos e frases separados pela formatação como um todo, na ordem natural do português. Os marcadores `{accent}` e `{lineBreak}` do título inicial podem ser movidos; o estilo visual não deve impor a ordem inglesa. A manutenção coordena a retirada de um trecho e de suas chaves no original, na marcação compartilhada e em todos os dicionários.

## Texto e marcadores

Traduza frases ou parágrafos completos para um português brasileiro técnico, claro e natural. Preserve condições, negações, unidades, etapas e restrições de tempo de vida. Não acrescente garantias técnicas, requisitos de plataforma, versões de pacotes ou notas de versão ausentes no original. Mantenha nomes de produtos, pacotes e arquivos, além dos identificadores C# e Slang, inclusive maiúsculas e minúsculas.

Cada valor deve ser uma string não vazia de texto simples, sem HTML ou Markdown. Respeite as aspas e os escapes do YAML; use aspas quando necessário para evitar que dois-pontos, cerquilhas ou valores parecidos com números sejam interpretados incorretamente. Preserve a ordem das chaves do inglês para facilitar a revisão.

Marcadores nomeados como `{context}` e `{link}` podem inserir código, links ou texto destacado das páginas compartilhadas. Marcadores de interface como `{name}`, `{query}` e `{count}` também podem receber texto em tempo de execução. Confira o uso na página ou interface correspondente, sem deduzir o conteúdo apenas pelo nome. Traduza também os rótulos de links e os textos destacados que tenham sua própria chave de recurso.

A ordem dos marcadores pode seguir a sintaxe do português. Para cada chave, preserve todos os nomes, suas maiúsculas e minúsculas e as chaves ASCII que delimitam o marcador. Não adicione, remova ou renomeie marcadores, nem os substitua por nomes de API, rótulos de links ou números de exemplo. Não divida chaves de recurso para recompor frases.

## Links de API e significado técnico

Os links são mantidos nas páginas compartilhadas. Identificadores que designam uma API local no texto e nas tabelas levam ao tipo ou membro correspondente. Variáveis, nomes de parâmetros, tipos definidos pelo exemplo, nomes de pacotes e arquivos e símbolos externos sem uma página de referência local permanecem como texto de código. A mesma grafia pode designar símbolos diferentes. Blocos de código comuns do tutorial não recebem links automáticos. É permitido mover um marcador inteiro, mas não alterar links, código compartilhado ou sua formatação.

Se uma API não tiver uma entrada de referência gerada, o identificador permanece como texto de código na página compartilhada, com um link para o código-fonte claramente identificado por perto. Não o substitua por um link para outro tipo ou membro.

Preserve estas distinções:

- Gravar comandos, enviá-los, executá-los na GPU e concluí-los são etapas distintas.
- Uso de recursos, residência de memória ou intenção de acesso da CPU, layout de textura e formato de pixel são conceitos distintos.
- A propriedade indica a responsabilidade de liberar um recurso. Um handle não mantém o recurso vivo por si só; o envio de comandos também não transfere a propriedade.
- A conclusão na GPU nem sempre significa que os dados de leitura temporários já foram copiados para o destino de CPU da aplicação. Preserve as instruções sobre aguardar a fila responsável.
- Um drawable é a textura de saída do quadro atual, não necessariamente uma imagem da cadeia de troca. Distinga as visualizações de recursos dos controles de interface.

## Build e revisão antes da entrega

Execute os comandos na raiz do repositório, com o SDK do .NET exigido pelo projeto e o DocFX instalados. Em uma cópia nova do repositório, os metadados gerados em `documents/api/` ainda não existem. Primeiro execute o build completo, que inclui a extração da API:

```sh
docfx documents/docfx.json --warningsAsErrors
```

Se os metadados de API já existem e correspondem ao código-fonte atual, alterações apenas no dicionário podem usar o build normal e a prévia:

```sh
docfx build documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

Este idioma já está registrado. O dicionário completo fica disponível após um build bem-sucedido, sem alterar a configuração. Chaves ausentes ou extras, valores vazios ou de outro tipo e nomes de marcadores diferentes fazem o build falhar. O build não detecta traduções imprecisas nem trechos que continuem em inglês; a leitura e revisão humanas são necessárias.

Todos os idiomas compartilham os caminhos das páginas. `?lang=pt-BR` seleciona português brasileiro; a troca de idioma mantém o caminho e a âncora. Sem uma escolha explícita, são usadas, nesta ordem, a escolha manual salva, as preferências do navegador e o inglês. Se esta tradução ainda não estiver publicada, ou se o carregamento ou a validação dos recursos da página falhar, a página fica em inglês, sem misturar idiomas frase a frase. A lista de idiomas é ordenada por código.

Antes de entregar, confira as chaves e os marcadores. Revise a página inicial, a entrada de Learn, o tutorial do triângulo, os conceitos, os exemplos e a interface de API no desktop e com 320 pixels de largura. Leia o tutorial como iniciante e confira terminologia, botões, busca, cópia de código, links de API e a posição da seção após trocar de idioma. Identificadores, declarações e código continuam compartilhados; traduza apenas os textos fornecidos pelo dicionário. Mantenha dicionários de teste e arquivos temporários fora do repositório e não entregue traduções com texto de preenchimento.

## Terminologia

| Termo em inglês  | Forma adotada                                                                                               |
| ---------------- | ----------------------------------------------------------------------------------------------------------- |
| API / GPU / RHI  | Preservar as siglas e explicá-las conforme o original                                                       |
| barrier          | barreira                                                                                                    |
| buffer           | buffer; preservar `Buffer`                                                                                  |
| command buffer   | buffer de comandos                                                                                          |
| command queue    | fila de comandos                                                                                            |
| drawable         | textura de saída do quadro atual; preservar `Drawable`, sem presumir que seja uma imagem da cadeia de troca |
| graphics context | contexto gráfico                                                                                            |
| pipeline         | pipeline                                                                                                    |
| readback         | leitura de dados da GPU                                                                                     |
| render pass      | passagem de renderização                                                                                    |
| resource         | recurso                                                                                                     |
| shader           | shader                                                                                                      |
| swap chain       | cadeia de troca                                                                                             |
| texture          | textura                                                                                                     |
| timeline         | linha do tempo                                                                                              |
| upload           | envio de dados à GPU                                                                                        |
