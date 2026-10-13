# Resolución de problemas

**Idioma:** [Inglés](Troubleshooting)

## `dotnet restore` termina, pero indica que no se encontró ningún proyecto

La solución `KUKULCAN.SharedKernel.Example.slnx` actual contiene carpetas y elementos de solución, pero no declara proyectos de aplicación ni de pruebas. El log de CI observado indica `Unable to find a project to restore!`. Añade los archivos de proyecto previstos a la solución antes de interpretar como significativos los resultados de build/test.

## CI aparece en verde, pero no hay resultados de pruebas

Revisa los logs de restauración, compilación y pruebas, no solo la insignia global del workflow. Un comando verde ejecutado sobre una solución sin proyectos no verifica el comportamiento de la implementación. Consulta [CI y criterios de calidad](CI-y-Criterios-de-Calidad).

## El workflow de cobertura no muestra un porcentaje

La cobertura figura como N/A porque todavía no hay proyectos de producción/pruebas que medir ni un informe verificado. El workflow está configurado para recopilar cobertura de `[KUKULCAN.SharedKernel.Example.Web]*`; confirma que el ensamblado de producción futuro coincide con ese filtro y que las pruebas generan archivos `coverage.cobertura.xml` no vacíos. Consulta [Cobertura de código](Cobertura-de-Codigo).

## El workflow Reference Client no encuentra su proyecto

El workflow actual espera `Source/KUKULCAN.SharedKernel.Example.Web/KUKULCAN.SharedKernel.Example.Web.csproj`. Ese proyecto todavía no existe en el árbol del repositorio. Créalo en la ruta acordada o actualiza el workflow cuando se decida expresamente otra estructura.

## ¿Cómo debe declararse el soporte de un proveedor de base de datos?

No infieras soporte por una variable de entorno o una referencia de paquete. Exige pruebas contra una instancia real del proveedor, incluidas migraciones, restricciones, consultas, borrado lógico y conservación histórica. Usa credenciales de prueba y mantenlas fuera del control de versiones.

## Falla la publicación de la Wiki porque no se puede clonar el repositorio de la Wiki

El repositorio Git de la Wiki de GitHub suele poder clonarse después de crear una página inicial. Abre la [Wiki del repositorio](https://github.com/Kukulcan-Software-Designer/KUKULCAN.SharedKernel.Example/wiki), crea una página llamada `Home` y guarda un texto provisional. Después vuelve a ejecutar el script de publicación; reemplazará `Home.md` por su contenido completo y añadirá las páginas restantes. Consulta la [guía oficial de GitHub sobre páginas wiki](https://docs.github.com/en/communities/documenting-your-project-with-wikis/adding-or-editing-wiki-pages).

## Problemas con la autenticación o los secretos

Nunca pegues códigos temporales de dispositivo, tokens de acceso, contraseñas ni claves de firma en Issues, páginas wiki o chats. Usa `gh auth status` para comprobar la sesión local de CLI y `gh auth setup-git` para configurar Git con las credenciales existentes. Reautentícate mediante la página de autorización por dispositivo de GitHub cuando sea necesario.
