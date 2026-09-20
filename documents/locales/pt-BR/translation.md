# Diretrizes de tradução para português brasileiro

## Recursos e estrutura

O inglês é a única fonte original. Copie apenas as chaves e os valores de `locales/en-US/strings.yml` para a própria `strings.yml`. Antes de traduzir, mantenha somente esta `translation.md` permanente, que não é publicada. Preserve a primeira linha `### YamlMime:Resources`.

Navegação, ordem dos capítulos, âncoras, links, formatação, código e imagens são compartilhados fora de `locales/`. Não adicione artigos, TOC, templates, scripts ou configurações neste diretório.

As chaves descrevem funções e conceitos estáveis. Não as renomeie ao ajustar o texto; evite nomes ligados à redação, posição, numeração ou idioma. Traduza apenas os valores ingleses para um português brasileiro técnico, claro e natural.

Os valores são texto simples, sem HTML ou Markdown. Marcadores nomeados, como `{context}` ou `{link}`, inserem elementos compartilhados de código e links. É possível mudar a ordem, mas todos os nomes devem ser preservados. Não adicione nem remova marcadores. Todas as chaves inglesas são obrigatórias; chaves ausentes ou marcadores diferentes fazem o build falhar.

Use os comandos habituais do DocFX. Ao adicionar o dicionário completo, o idioma fica disponível automaticamente. Todos os idiomas compartilham os caminhos das páginas; `?lang=pt-BR` seleciona o idioma. As listas seguem a ordem dos códigos dos diretórios.

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## Terminologia

| Termo em inglês | Forma adotada |
| --- | --- |
| API / GPU / RHI | Preservar as siglas e explicá-las conforme o original |
| barrier | barreira |
| buffer | buffer; preservar `Buffer` |
| command buffer | buffer de comandos |
| command queue | fila de comandos |
| drawable | imagem atual disponível para apresentação; preservar `Drawable` |
| graphics context | contexto gráfico |
| pipeline | pipeline |
| readback | leitura de dados da GPU |
| render pass | passagem de renderização |
| resource | recurso |
| shader | shader |
| swap chain | cadeia de troca |
| texture | textura |
| timeline | linha do tempo |
| upload | envio de dados à GPU |
