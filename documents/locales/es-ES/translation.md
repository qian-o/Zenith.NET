# Criterios de traducción al español

## Recursos y estructura

El inglés es la única fuente original. Copiar únicamente las claves y valores de `locales/en-US/strings.yml` a la propia `strings.yml`. Hasta empezar la traducción, conservar solo esta `translation.md` permanente, que no se publica. Mantener la primera línea `### YamlMime:Resources`.

La navegación, el orden de capítulos, las anclas, los enlaces, el formato, el código y las imágenes se comparten fuera de `locales/`. No crear aquí artículos, TOC, plantillas, scripts ni configuraciones.

Las claves describen funciones y conceptos estables. No renombrarlas al retocar el texto; evitar nombres ligados a una frase, posición, numeración o idioma. Traducir únicamente los valores ingleses con un español técnico claro y natural.

Los valores son texto, no HTML ni Markdown. Marcadores con nombre como `{context}` o `{link}` insertan elementos compartidos de código y enlaces. Se pueden reordenar, pero deben conservarse todos sus nombres. No añadir ni eliminar marcadores. Deben estar presentes todas las claves inglesas; si faltan claves o cambian los marcadores, la compilación falla.

Se mantienen los comandos habituales de DocFX. El idioma se habilita al añadir el diccionario completo. Todas las lenguas usan las mismas rutas; `?lang=es-ES` selecciona el idioma. Las listas se ordenan por código de carpeta.

```sh
docfx documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

## Terminología

| Término inglés | Forma acordada |
| --- | --- |
| API / GPU / RHI | Conservar las siglas y explicarlas según el original |
| barrier | barrera |
| buffer | búfer; conservar `Buffer` |
| command buffer | búfer de comandos |
| command queue | cola de comandos |
| drawable | imagen actual disponible para presentación; conservar `Drawable` |
| graphics context | contexto gráfico |
| pipeline | canalización |
| readback | lectura de datos desde la GPU |
| render pass | pasada de renderizado |
| resource | recurso |
| shader | sombreador |
| swap chain | cadena de intercambio |
| texture | textura |
| timeline | línea de tiempo |
| upload | carga de datos a la GPU |
