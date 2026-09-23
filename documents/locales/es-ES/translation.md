# Criterios de traducción al español

## Recursos y estructura

El [diccionario de recursos en inglés](../en-US/strings.yml) es la única fuente del texto. Las normas del proyecto están en la [guía de mantenimiento de la documentación](../../maintenance.md). Para iniciar una traducción, copie el archivo inglés completo como `strings.yml` en este directorio. Conserve la primera línea `### YamlMime:Resources` y la codificación UTF-8. En futuras revisiones, incorpore los cambios del original sin sobrescribir las traducciones existentes. Este archivo `translation.md` se conserva permanentemente y no se publica en el sitio.

La navegación, el orden de los capítulos, las anclas, los enlaces, el formato, el código y las imágenes se mantienen fuera de `locales/` y se comparten entre idiomas. No cree aquí artículos, TOC, plantillas, scripts ni configuraciones.

Modifique únicamente los valores de la propia `strings.yml`. El conjunto de claves debe coincidir exactamente con el inglés. No añada, elimine ni renombre claves por iniciativa propia, aunque parezcan repetidas o correspondan a textos de interfaz que aún no haya visto. Si encuentra un error en el original, comunique la clave y el problema a los responsables del proyecto. Primero se corrigen la fuente inglesa y las páginas compartidas; después, las traducciones.

## Estilo y contexto

Emplear un estilo de manual técnico: preciso, sobrio y comprensible. Explicar la relación entre las operaciones antes de sus restricciones. En los tutoriales, mantener instrucciones concretas y resultados comprobables. El rigor no requiere pasivas ni frases innecesariamente largas.

Revisar cada artículo en su orden de lectura, junto con los párrafos, tablas y código que lo rodean. Las claves son límites de almacenamiento, no unidades de traducción aisladas. Comprobar las concordancias y preposiciones de los enlaces y fragmentos destacados dentro de la frase completa.

Los títulos, etiquetas, textos breves visibles de la portada y leyenda del pie no llevan punto final. Los párrafos, diagnósticos e instrucciones de accesibilidad conservan la puntuación normal. Utilizar un término asentado por concepto, sin añadir de forma selectiva equivalentes ingleses entre paréntesis. Conservar los identificadores de API, archivos y ajustes.

Conserve una frase si explica la operación actual, una decisión de diseño, un resultado comprobable o una condición necesaria. Elimine listas de funciones que no se usan, advertencias genéricas y requisitos repetidos. Traduzca los títulos y las frases divididas por el formato como una unidad, con el orden natural del español. Los marcadores `{accent}` y `{lineBreak}` del título de portada pueden moverse; el diseño no debe imponer la sintaxis inglesa. La eliminación de un pasaje y sus claves se coordina en el original, el marcado común y todos los diccionarios.

## Texto y marcadores

Traduzca frases o párrafos completos con un español técnico claro y natural. Conserve las condiciones, negaciones, unidades, pasos y restricciones de vida útil. No añada garantías técnicas, requisitos de plataforma, versiones de paquetes ni notas de versiones ausentes del original. Mantenga los nombres de productos, paquetes y archivos, así como los identificadores de C# y Slang, incluidas sus mayúsculas y minúsculas.

Cada valor debe ser una cadena no vacía de texto sin HTML ni Markdown. Respete las comillas y los escapes de YAML; utilice comillas cuando sean necesarias para evitar que los dos puntos, las almohadillas o los valores parecidos a números se interpreten incorrectamente. Mantenga el orden de las claves del inglés para facilitar la revisión.

Los marcadores con nombre, como `{context}` y `{link}`, pueden insertar código, enlaces o texto destacado de las páginas compartidas. Los marcadores de interfaz, como `{name}`, `{query}` y `{count}`, también pueden recibir texto durante la ejecución. Compruebe su uso en la página o interfaz correspondiente; no deduzca el contenido solo por el nombre. Traduzca también las etiquetas de enlace y los textos destacados que tengan su propia clave de recurso.

Puede reordenar los marcadores según la sintaxis española. En cada clave, conserve todos los nombres, sus mayúsculas y minúsculas y las llaves ASCII. No añada, elimine ni renombre marcadores, ni los sustituya por nombres de API, etiquetas de enlace o números de ejemplo. No divida las claves para recomponer frases.

## Enlaces de API y significado técnico

Los enlaces se mantienen en las páginas compartidas. Los identificadores que designan una API local en el texto y las tablas enlazan con su tipo o miembro. Las variables, los parámetros, los tipos definidos por el ejemplo, los nombres de paquetes y archivos y los símbolos externos sin referencia local permanecen como texto de código. Una misma grafía puede designar símbolos distintos. Los bloques de código habituales del tutorial no reciben enlaces automáticos. Puede mover un marcador completo, pero no cambiar enlaces, código compartido ni su formato.

Si una API no tiene una entrada de referencia generada, su identificador permanece como texto de código en la página compartida, con un enlace al código fuente claramente identificado cerca. No lo sustituya por un enlace a otro tipo o miembro.

Conserve estas distinciones:

- Grabar comandos, enviarlos, ejecutarlos en la GPU y completarlos son pasos distintos.
- El uso de un recurso, la residencia de memoria o intención de acceso de la CPU, el layout de una textura y su formato de píxel son conceptos distintos.
- La propiedad indica quién debe liberar el recurso. Un handle no mantiene el recurso vivo por sí solo; enviar comandos tampoco transfiere su propiedad.
- La finalización en la GPU no siempre implica que los datos de lectura temporal ya se hayan copiado al destino de CPU de la aplicación. Conserve las indicaciones sobre esperar a la cola responsable.
- Un drawable es la textura de salida del fotograma actual y no necesariamente una imagen de la cadena de intercambio. Distinga las vistas de recursos de los controles de interfaz.

## Compilación y revisión de entrega

Ejecute los comandos desde la raíz del repositorio, con el SDK de .NET requerido por el proyecto y DocFX instalados. En una copia nueva del repositorio aún no existen los metadatos generados en `documents/api/`. Primero ejecute la compilación completa, incluida la extracción de la API:

```sh
docfx documents/docfx.json --warningsAsErrors
```

Si los metadatos de API ya existen y corresponden al código fuente actual, para cambios limitados al diccionario puede usar la compilación normal y la vista previa:

```sh
docfx build documents/docfx.json --warningsAsErrors
docfx serve documents/_site --hostname 127.0.0.1 --port 8080
```

Este idioma ya está registrado. El diccionario completo queda disponible tras una compilación correcta, sin cambiar la configuración. La compilación falla si faltan o sobran claves, hay valores vacíos o de otro tipo, o cambian los nombres de los marcadores. No detecta traducciones inexactas ni texto que siga en inglés; es necesaria una revisión humana.

Todos los idiomas comparten las rutas. `?lang=es-ES` selecciona español; el cambio de idioma conserva la ruta y el ancla. Sin selección explícita, se consultan primero la selección manual guardada, después las preferencias del navegador y, por último, el inglés. Si esta traducción no está publicada, o falla la carga o validación de los recursos de la página, se muestra la página en inglés, sin mezclar idiomas frase a frase. La lista de idiomas se ordena por código.

Antes de entregar, compruebe las claves y los marcadores. Revise la página de inicio, la entrada de Learn, el tutorial del triángulo, los conceptos, los ejemplos y la interfaz de API en escritorio y a 320 píxeles de ancho. Lea el tutorial como principiante y compruebe terminología, botones, búsqueda, copia de código, enlaces de API y la posición del apartado tras cambiar de idioma. Los identificadores, las declaraciones y el código siguen siendo compartidos; solo se traducen los textos del diccionario. Mantenga los diccionarios de prueba y archivos temporales fuera del repositorio y no entregue traducciones provisionales de relleno.

## Terminología

| Término inglés   | Forma acordada                                                                                                          |
| ---------------- | ----------------------------------------------------------------------------------------------------------------------- |
| API / GPU / RHI  | Conservar las siglas y explicarlas según el original                                                                    |
| barrier          | barrera                                                                                                                 |
| buffer           | búfer; conservar `Buffer`                                                                                               |
| command buffer   | búfer de comandos                                                                                                       |
| command queue    | cola de comandos                                                                                                        |
| drawable         | textura de salida del fotograma actual; conservar `Drawable`, sin asumir que sea una imagen de la cadena de intercambio |
| graphics context | contexto gráfico                                                                                                        |
| graphics API     | API gráfica; DirectX 12, Metal 4 o Vulkan 1.4                                                                           |
| implementation   | implementación de la API gráfica; para diferencias técnicas entre implementaciones                                      |
| pipeline         | canalización                                                                                                            |
| readback         | lectura de datos desde la GPU                                                                                           |
| render pass      | pasada de renderizado                                                                                                   |
| resource         | recurso                                                                                                                 |
| shader           | sombreador                                                                                                              |
| swap chain       | cadena de intercambio                                                                                                   |
| texture          | textura                                                                                                                 |
| timeline         | línea de tiempo                                                                                                         |
| upload           | carga de datos a la GPU                                                                                                 |
